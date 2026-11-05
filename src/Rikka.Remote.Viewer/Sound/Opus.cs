using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Rikka.Remote.Viewer.Sound;

internal static unsafe partial class Opus
{
    public const int Ok = 0;

    private const string Library = "opus.dll";

    [LibraryImport(Library, EntryPoint = "opus_decoder_create")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void* CreateDecoder(int sampleRate, int channels, int* error);

    [LibraryImport(Library, EntryPoint = "opus_decoder_destroy")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void DestroyDecoder(void* decoder);

    [LibraryImport(Library, EntryPoint = "opus_decode_float")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int DecodeFloat(void* decoder, byte* data, int length, float* pcm, int frameSize, int decodeFec);
}
