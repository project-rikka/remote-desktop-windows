using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dxgi;

namespace Rikka.Remote.Viewer.Interop;

[Guid("63AAD0B8-7C24-40FF-85A8-640D944CC325")]
internal unsafe struct ISwapChainPanelNative : IComIID
{
    private static readonly Guid Iid = new("63AAD0B8-7C24-40FF-85A8-640D944CC325");

    private readonly void** vtable;

    public static ref readonly Guid Guid => ref Iid;

    public HRESULT SetSwapChain(IDXGISwapChain* swapChain)
    {
        fixed (ISwapChainPanelNative* self = &this)
        {
            return ((delegate* unmanaged[Stdcall]<ISwapChainPanelNative*, IDXGISwapChain*, HRESULT>)vtable[3])(self, swapChain);
        }
    }
}
