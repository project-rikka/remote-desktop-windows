using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Rikka.Remote.Viewer.Diagnostics;
using Rikka.Remote.Viewer.Threading;
using Rikka.Remote.Wire;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Rikka.Remote.Viewer.Input;

internal sealed unsafe class InputThread : IDisposable
{
    private const uint Drain = PInvoke.WM_APP + 1;
    private const uint Arm = PInvoke.WM_APP + 2;
    private const uint Disarm = PInvoke.WM_APP + 3;
    private const uint Lock = PInvoke.WM_APP + 4;
    private const uint Unlock = PInvoke.WM_APP + 5;
    private const uint Release = PInvoke.WM_APP + 6;
    private const uint SecureAttention = PInvoke.WM_APP + 7;
    private const uint Quit = PInvoke.WM_APP + 8;
    private const int Swallow = 1;
    private const int Keys = 256;
    private const int Buttons = 5;
    private const int QueueCapacity = 1024;
    private const ushort GenericDesktop = 0x01;
    private const ushort Mouse = 0x02;
    private const string WindowClass = "RikkaRemoteViewerInput";

    private static InputThread? instance;

    private readonly SingleConsumerRing<HookEvent> events = new(QueueCapacity);
    private readonly bool[] pressed = new bool[Keys];
    private readonly bool[] consumed = new bool[Keys];
    private readonly bool[] held = new bool[Buttons];
    private readonly Action<Escape> onEscape;
    private readonly Action onSessionChange;
    private readonly ManualResetEventSlim started = new();
    private readonly Thread thread;
    private HWND target;
    private HWND window;
    private HHOOK keyboardHook;
    private HHOOK mouseHook;
    private uint threadId;
    private volatile IInputSink? sink;
    private volatile PointerArea? area;
    private volatile bool armed;
    private volatile bool locked;
    private int buttonsHeld;
    private bool drainPosted;

    public InputThread(Action<Escape> onEscape, Action onSessionChange)
    {
        this.onEscape = onEscape;
        this.onSessionChange = onSessionChange;
        instance = this;
        thread = new Thread(Run) { Name = "rikka-remote-viewer-input", IsBackground = true, Priority = ThreadPriority.Highest };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        started.Wait();
    }

    public void Attach(HWND streamWindow, IInputSink? input, PointerArea? pointer)
    {
        target = streamWindow;
        sink = input;
        area = pointer;
    }

    public void SetArmed(bool on) => Post(on ? Arm : Disarm);

    public void SetLocked(bool on) => Post(on ? Lock : Unlock);

    public void ReleaseAll() => Post(Release);

    public void SendSecureAttention() => Post(SecureAttention);

    public void Dispose()
    {
        Post(Quit);
        thread.Join();
        started.Dispose();
        instance = null;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT OnKeyboard(int code, WPARAM message, LPARAM data)
    {
        var self = instance!;
        if (code >= 0 && self.Capturing())
        {
            var key = (KBDLLHOOKSTRUCT*)data.Value;
            self.Enqueue(new HookEvent((uint)message.Value, key->vkCode, key->scanCode, (uint)key->flags, 0, 0, 0));
            return new LRESULT(Swallow);
        }

        return PInvoke.CallNextHookEx(default, code, message, data);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT OnMouse(int code, WPARAM message, LPARAM data)
    {
        var self = instance!;
        if (code >= 0 && self.Capturing() && self.area is { } pointer)
        {
            var mouse = (MSLLHOOKSTRUCT*)data.Value;
            var kind = (uint)message.Value;
            if (self.locked)
            {
                if (kind != PInvoke.WM_MOUSEMOVE)
                {
                    self.Enqueue(new HookEvent(kind, 0, 0, mouse->flags, mouse->pt.X, mouse->pt.Y, mouse->mouseData));
                }

                return new LRESULT(Swallow);
            }

            var inside = self.buttonsHeld > 0 || pointer.Contains(mouse->pt);
            if (inside)
            {
                self.Track(kind);
                self.Enqueue(new HookEvent(kind, 0, 0, mouse->flags, mouse->pt.X, mouse->pt.Y, mouse->mouseData));
                if (kind != PInvoke.WM_MOUSEMOVE)
                {
                    return new LRESULT(Swallow);
                }
            }
        }

        return PInvoke.CallNextHookEx(default, code, message, data);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT OnWindowMessage(HWND window, uint message, WPARAM wParam, LPARAM lParam)
    {
        var self = instance!;
        if (message == PInvoke.WM_INPUT)
        {
            self.RawInput((HRAWINPUT)lParam.Value);
            return default;
        }

        if (message == PInvoke.WM_WTSSESSION_CHANGE)
        {
            self.ReleaseEverything();
            self.onSessionChange();
            return default;
        }

        return PInvoke.DefWindowProc(window, message, wParam, lParam);
    }

    private bool Capturing() => armed && !target.IsNull && PInvoke.GetForegroundWindow() == target;

    private void Track(uint message)
    {
        if (message is PInvoke.WM_LBUTTONDOWN or PInvoke.WM_RBUTTONDOWN or PInvoke.WM_MBUTTONDOWN or PInvoke.WM_XBUTTONDOWN)
        {
            buttonsHeld++;
        }
        else if (message is PInvoke.WM_LBUTTONUP or PInvoke.WM_RBUTTONUP or PInvoke.WM_MBUTTONUP or PInvoke.WM_XBUTTONUP && buttonsHeld > 0)
        {
            buttonsHeld--;
        }
    }

    private void Enqueue(in HookEvent hookEvent)
    {
        if (events.TryEnqueue(hookEvent) && !drainPosted)
        {
            drainPosted = true;
            PInvoke.PostThreadMessage(threadId, Drain, default, default);
        }
    }

    private void Post(uint message) => PInvoke.PostThreadMessage(threadId, message, default, default);

    private void Run()
    {
        Mmcss.Join(Mmcss.Games);
        threadId = PInvoke.GetCurrentThreadId();
        CreateWindow();
        MSG message;
        PInvoke.PeekMessage(&message, default, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_NOREMOVE);
        started.Set();
        while (PInvoke.GetMessage(&message, default, 0, 0) > 0)
        {
            if (message.hwnd.IsNull && Handle(message.message))
            {
                break;
            }

            PInvoke.TranslateMessage(&message);
            PInvoke.DispatchMessage(&message);
        }

        Uninstall();
        PInvoke.WTSUnRegisterSessionNotification(window);
        PInvoke.DestroyWindow(window);
    }

    private bool Handle(uint message)
    {
        switch (message)
        {
            case Drain:
                drainPosted = false;
                while (events.TryDequeue(out var hookEvent))
                {
                    Process(hookEvent);
                }

                break;
            case Arm:
                armed = true;
                Install();
                break;
            case Disarm:
                armed = false;
                Uninstall();
                ReleaseEverything();
                break;
            case Lock:
                StartLock();
                break;
            case Unlock:
                StopLock();
                break;
            case Release:
                ReleaseEverything();
                break;
            case SecureAttention:
                SendKeys([EvdevKey.LeftCtrl, EvdevKey.LeftAlt, EvdevKey.Delete]);
                break;
            case Quit:
                return true;
        }

        return false;
    }

    private void CreateWindow()
    {
        fixed (char* name = WindowClass)
        {
            var windowClass = new WNDCLASSEXW
            {
                cbSize = (uint)sizeof(WNDCLASSEXW),
                lpfnWndProc = &OnWindowMessage,
                hInstance = PInvoke.GetModuleHandle((char*)null),
                lpszClassName = name,
            };
            if (PInvoke.RegisterClassEx(&windowClass) == 0)
            {
                throw new InvalidOperationException($"cannot register the input window: {(WIN32_ERROR)Marshal.GetLastPInvokeError()}");
            }

            window = PInvoke.CreateWindowEx(0, name, name, 0, 0, 0, 0, 0, HWND.HWND_MESSAGE, default, windowClass.hInstance, null);
        }

        if (window.IsNull)
        {
            throw new InvalidOperationException($"cannot create the input window: {(WIN32_ERROR)Marshal.GetLastPInvokeError()}");
        }

        if (!PInvoke.WTSRegisterSessionNotification(window, PInvoke.NOTIFY_FOR_THIS_SESSION))
        {
            Log.Warn($"cannot follow session changes: {(WIN32_ERROR)Marshal.GetLastPInvokeError()}");
        }
    }

    private void Install()
    {
        var module = PInvoke.GetModuleHandle((char*)null);
        if (keyboardHook.IsNull)
        {
            keyboardHook = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_KEYBOARD_LL, &OnKeyboard, module, 0);
        }

        if (mouseHook.IsNull)
        {
            mouseHook = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_MOUSE_LL, &OnMouse, module, 0);
        }

        if (keyboardHook.IsNull || mouseHook.IsNull)
        {
            Log.Error($"cannot capture input: {(WIN32_ERROR)Marshal.GetLastPInvokeError()}");
        }
    }

    private void Uninstall()
    {
        if (!keyboardHook.IsNull)
        {
            PInvoke.UnhookWindowsHookEx(keyboardHook);
            keyboardHook = default;
        }

        if (!mouseHook.IsNull)
        {
            PInvoke.UnhookWindowsHookEx(mouseHook);
            mouseHook = default;
        }

        StopLock();
        buttonsHeld = 0;
    }

    private void Process(in HookEvent hookEvent)
    {
        if (sink is not { } output)
        {
            return;
        }

        switch (hookEvent.Message)
        {
            case PInvoke.WM_KEYDOWN or PInvoke.WM_SYSKEYDOWN or PInvoke.WM_KEYUP or PInvoke.WM_SYSKEYUP:
                Key(output, hookEvent);
                break;
            case PInvoke.WM_MOUSEMOVE:
                if (area is { } pointer)
                {
                    output.Move(pointer.Map(new System.Drawing.Point(hookEvent.X, hookEvent.Y)));
                }

                break;
            case PInvoke.WM_MOUSEWHEEL:
                output.Send(InputEvent.ForWheel(0, -(short)(hookEvent.MouseData >> 16)));
                break;
            case PInvoke.WM_MOUSEHWHEEL:
                output.Send(InputEvent.ForWheel((short)(hookEvent.MouseData >> 16), 0));
                break;
            default:
                Button(output, hookEvent);
                break;
        }
    }

    private void Key(IInputSink output, in HookEvent hookEvent)
    {
        var down = hookEvent.Message is PInvoke.WM_KEYDOWN or PInvoke.WM_SYSKEYDOWN;
        var extended = (hookEvent.Flags & (uint)KBDLLHOOKSTRUCT_FLAGS.LLKHF_EXTENDED) != 0;
        if (KeyTranslator.Translate(hookEvent.VirtualKey, hookEvent.Scan, extended) is not { } code || code >= Keys)
        {
            return;
        }

        if (!down && consumed[code])
        {
            consumed[code] = false;
            return;
        }

        if (down && Chord(code) is { } escape)
        {
            consumed[code] = true;
            onEscape(escape);
            return;
        }

        if (pressed[code] == down)
        {
            return;
        }

        pressed[code] = down;
        output.Send(InputEvent.ForKey(code, down));
    }

    private Escape? Chord(ushort code)
    {
        var control = pressed[EvdevKey.LeftCtrl] || pressed[EvdevKey.RightCtrl];
        var alt = pressed[EvdevKey.LeftAlt] || pressed[EvdevKey.RightAlt];
        var shift = pressed[EvdevKey.LeftShift] || pressed[EvdevKey.RightShift];
        if (!(control && alt && shift))
        {
            return null;
        }

        return code switch
        {
            EvdevKey.M => Escape.PointerLock,
            EvdevKey.F => Escape.Fullscreen,
            EvdevKey.S => Escape.Statistics,
            EvdevKey.Q => Escape.Leave,
            _ => null,
        };
    }

    private void Button(IInputSink output, in HookEvent hookEvent)
    {
        var (button, down) = hookEvent.Message switch
        {
            PInvoke.WM_LBUTTONDOWN => (Wire.Button.Primary, true),
            PInvoke.WM_LBUTTONUP => (Wire.Button.Primary, false),
            PInvoke.WM_RBUTTONDOWN => (Wire.Button.Secondary, true),
            PInvoke.WM_RBUTTONUP => (Wire.Button.Secondary, false),
            PInvoke.WM_MBUTTONDOWN => (Wire.Button.Middle, true),
            PInvoke.WM_MBUTTONUP => (Wire.Button.Middle, false),
            PInvoke.WM_XBUTTONDOWN => ((hookEvent.MouseData >> 16) == PInvoke.XBUTTON1 ? Wire.Button.Back : Wire.Button.Forward, true),
            PInvoke.WM_XBUTTONUP => ((hookEvent.MouseData >> 16) == PInvoke.XBUTTON1 ? Wire.Button.Back : Wire.Button.Forward, false),
            _ => ((Button?)null, false),
        };
        if (button is not { } which || area is not { } pointer || held[(int)which] == down)
        {
            return;
        }

        held[(int)which] = down;
        var at = locked ? pointer.RemotePosition() : pointer.Map(new System.Drawing.Point(hookEvent.X, hookEvent.Y));
        output.Send(InputEvent.ForButton(which, down, at));
    }

    private void RawInput(HRAWINPUT handle)
    {
        if (!locked || sink is not { } output || area is not { } pointer)
        {
            return;
        }

        RAWINPUT raw;
        var size = (uint)sizeof(RAWINPUT);
        if (PInvoke.GetRawInputData(handle, RAW_INPUT_DATA_COMMAND_FLAGS.RID_INPUT, &raw, &size, (uint)sizeof(RAWINPUTHEADER)) == unchecked((uint)-1))
        {
            return;
        }

        if (raw.header.dwType != (uint)RID_DEVICE_INFO_TYPE.RIM_TYPEMOUSE || (raw.data.mouse.usFlags & MOUSE_STATE.MOUSE_MOVE_ABSOLUTE) != 0)
        {
            return;
        }

        var dx = raw.data.mouse.lLastX;
        var dy = raw.data.mouse.lLastY;
        if (dx == 0 && dy == 0)
        {
            return;
        }

        var scale = (float)pointer.RemoteScale();
        output.Send(InputEvent.ForRelative(dx * scale, dy * scale));
    }

    private void StartLock()
    {
        if (locked || area is not { } pointer)
        {
            return;
        }

        var device = new RAWINPUTDEVICE { usUsagePage = GenericDesktop, usUsage = Mouse, dwFlags = RAWINPUTDEVICE_FLAGS.RIDEV_INPUTSINK, hwndTarget = window };
        if (!PInvoke.RegisterRawInputDevices(&device, 1, (uint)sizeof(RAWINPUTDEVICE)))
        {
            Log.Error($"cannot read the mouse: {(WIN32_ERROR)Marshal.GetLastPInvokeError()}");
            return;
        }

        var clip = pointer.PictureOnScreen();
        PInvoke.ClipCursor(&clip);
        locked = true;
    }

    private void StopLock()
    {
        if (!locked)
        {
            return;
        }

        locked = false;
        var device = new RAWINPUTDEVICE { usUsagePage = GenericDesktop, usUsage = Mouse, dwFlags = RAWINPUTDEVICE_FLAGS.RIDEV_REMOVE };
        PInvoke.RegisterRawInputDevices(&device, 1, (uint)sizeof(RAWINPUTDEVICE));
        PInvoke.ClipCursor((RECT*)null);
    }

    private void ReleaseEverything()
    {
        Array.Clear(pressed);
        Array.Clear(consumed);
        Array.Clear(held);
        buttonsHeld = 0;
        sink?.Send(InputEvent.ReleaseAll);
    }

    private void SendKeys(ReadOnlySpan<ushort> keys)
    {
        if (sink is not { } output)
        {
            return;
        }

        foreach (var key in keys)
        {
            output.Send(InputEvent.ForKey(key, true));
        }

        for (var index = keys.Length - 1; index >= 0; index--)
        {
            output.Send(InputEvent.ForKey(keys[index], false));
        }
    }
}
