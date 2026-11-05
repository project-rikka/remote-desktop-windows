using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Rikka.Remote.Viewer.Interop;
using Rikka.Remote.Viewer.Threading;
using Windows.Win32.Media.Audio;

namespace Rikka.Remote.Viewer.Sound;

internal sealed unsafe class DefaultDeviceWatcher : IDisposable
{
    private const int Methods = 8;
    private const int NoInterface = unchecked((int)0x80004002);

    private static readonly void** Table = CreateTable();
    private static readonly Guid UnknownIid = new("00000000-0000-0000-C000-000000000046");

    private readonly IMMDeviceEnumerator* enumerator;
    private readonly Win32Event changed;
    private GCHandle self;
    private NotificationObject* native;

    public DefaultDeviceWatcher(IMMDeviceEnumerator* enumerator, Win32Event changed)
    {
        this.enumerator = enumerator;
        this.changed = changed;
        self = GCHandle.Alloc(this);
        native = (NotificationObject*)NativeMemory.Alloc((nuint)sizeof(NotificationObject));
        native->Table = Table;
        native->Owner = GCHandle.ToIntPtr(self);
        ComObjects.Check(enumerator->RegisterEndpointNotificationCallback((IMMNotificationClient*)native), "cannot follow the default sound output");
    }

    public void Dispose()
    {
        if (native is null)
        {
            return;
        }

        enumerator->UnregisterEndpointNotificationCallback((IMMNotificationClient*)native);
        NativeMemory.Free(native);
        native = null;
        self.Free();
    }

    private static void** CreateTable()
    {
        var table = (void**)NativeMemory.Alloc((nuint)(Methods * sizeof(void*)));
        table[0] = (delegate* unmanaged[Stdcall]<NotificationObject*, Guid*, void**, int>)&QueryInterface;
        table[1] = (delegate* unmanaged[Stdcall]<NotificationObject*, uint>)&AddRef;
        table[2] = (delegate* unmanaged[Stdcall]<NotificationObject*, uint>)&Release;
        table[3] = (delegate* unmanaged[Stdcall]<NotificationObject*, char*, uint, int>)&OnDeviceStateChanged;
        table[4] = (delegate* unmanaged[Stdcall]<NotificationObject*, char*, int>)&OnDeviceAdded;
        table[5] = (delegate* unmanaged[Stdcall]<NotificationObject*, char*, int>)&OnDeviceRemoved;
        table[6] = (delegate* unmanaged[Stdcall]<NotificationObject*, EDataFlow, ERole, char*, int>)&OnDefaultDeviceChanged;
        table[7] = (delegate* unmanaged[Stdcall]<NotificationObject*, char*, void*, int>)&OnPropertyValueChanged;
        return table;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int QueryInterface(NotificationObject* self, Guid* iid, void** result)
    {
        if (*iid == UnknownIid || *iid == ComObjects.Iid<IMMNotificationClient>())
        {
            *result = self;
            return 0;
        }

        *result = null;
        return NoInterface;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint AddRef(NotificationObject* self) => 1;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static uint Release(NotificationObject* self) => 1;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int OnDeviceStateChanged(NotificationObject* self, char* device, uint state) => 0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int OnDeviceAdded(NotificationObject* self, char* device) => 0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int OnDeviceRemoved(NotificationObject* self, char* device) => 0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int OnDefaultDeviceChanged(NotificationObject* self, EDataFlow flow, ERole role, char* device)
    {
        if (flow == EDataFlow.eRender && role == ERole.eConsole)
        {
            ((DefaultDeviceWatcher)GCHandle.FromIntPtr(self->Owner).Target!).changed.Set();
        }

        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int OnPropertyValueChanged(NotificationObject* self, char* device, void* key) => 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct NotificationObject
    {
        public void** Table;
        public nint Owner;
    }
}
