using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;

namespace Rikka.Remote.Viewer.Interop;

internal static unsafe class ComObjects
{
    public static T* Query<T>(void* unknown)
        where T : unmanaged, IComIID
    {
        void* result;
        var iid = T.Guid;
        ((IUnknown*)unknown)->QueryInterface(&iid, &result).ThrowOnFailure();
        return (T*)result;
    }

    public static T* TryQuery<T>(void* unknown)
        where T : unmanaged, IComIID
    {
        void* result;
        var iid = T.Guid;
        return ((IUnknown*)unknown)->QueryInterface(&iid, &result).Succeeded ? (T*)result : null;
    }

    public static void Release<T>(ref T* pointer)
        where T : unmanaged
    {
        if (pointer is not null)
        {
            ((IUnknown*)pointer)->Release();
            pointer = null;
        }
    }

    public static Guid Iid<T>()
        where T : unmanaged, IComIID => T.Guid;

    public static void Check(HRESULT result, string what)
    {
        if (result.Failed)
        {
            throw new InvalidOperationException($"{what}: 0x{(uint)result.Value:x8}");
        }
    }
}
