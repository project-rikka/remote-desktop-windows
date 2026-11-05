using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Rikka.Remote.Viewer.Diagnostics;
using Rikka.Remote.Viewer.Interop;
using Windows.Win32;
using Windows.Win32.Graphics.Direct3D;
using Windows.Win32.Graphics.Direct3D10;
using Windows.Win32.Graphics.Direct3D11;

namespace Rikka.Remote.Viewer.Video;

internal sealed unsafe class D3DDevice : IDisposable
{
    private readonly Lock contextLock = new();
    private GCHandle self;
    private ID3D11Device* device;
    private ID3D11DeviceContext* context;

    private D3DDevice(ID3D11Device* device, ID3D11DeviceContext* context, bool hardware)
    {
        this.device = device;
        this.context = context;
        IsHardware = hardware;
        self = GCHandle.Alloc(this);
        var multithread = ComObjects.Query<ID3D10Multithread>(device);
        multithread->SetMultithreadProtected(true);
        ComObjects.Release(ref multithread);
    }

    public ID3D11Device* Device => device;

    public ID3D11DeviceContext* Context => context;

    public bool IsHardware { get; }

    public void* LockContext => (void*)GCHandle.ToIntPtr(self);

    public static delegate* unmanaged[Cdecl]<void*, void> LockFunction => &OnLock;

    public static delegate* unmanaged[Cdecl]<void*, void> UnlockFunction => &OnUnlock;

    public static D3DDevice Create()
    {
        var levels = stackalloc D3D_FEATURE_LEVEL[] { D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_1, D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_0 };
        var flags = D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_BGRA_SUPPORT | D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_VIDEO_SUPPORT;
        ID3D11Device* created;
        ID3D11DeviceContext* immediate;
        var result = PInvoke.D3D11CreateDevice(null, D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_HARDWARE, default, flags, levels, 2, PInvoke.D3D11_SDK_VERSION, &created, null, &immediate);
        if (result.Succeeded)
        {
            return new D3DDevice(created, immediate, hardware: true);
        }

        Log.Warn($"no hardware Direct3D 11 device: 0x{(uint)result.Value:x8}");
        result = PInvoke.D3D11CreateDevice(null, D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_WARP, default, D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_BGRA_SUPPORT, levels, 2, PInvoke.D3D11_SDK_VERSION, &created, null, &immediate);
        ComObjects.Check(result, "cannot create a Direct3D 11 device");
        return new D3DDevice(created, immediate, hardware: false);
    }

    public Lock.Scope Enter() => contextLock.EnterScope();

    public void Dispose()
    {
        ComObjects.Release(ref context);
        ComObjects.Release(ref device);
        if (self.IsAllocated)
        {
            self.Free();
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnLock(void* owner) => ((D3DDevice)GCHandle.FromIntPtr((nint)owner).Target!).contextLock.Enter();

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnUnlock(void* owner) => ((D3DDevice)GCHandle.FromIntPtr((nint)owner).Target!).contextLock.Exit();
}
