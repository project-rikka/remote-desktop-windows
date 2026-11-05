using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Rikka.Remote.Viewer.Diagnostics;

namespace Rikka.Remote.Viewer.Video.Ffmpeg;

internal static unsafe class LibavRuntime
{
    private const int LineBytes = 1024;
    private const int ErrorBytes = 256;
    private static readonly Lazy<bool> Ready = new(Initialize, LazyThreadSafetyMode.ExecutionAndPublication);

    public static void EnsureReady() => _ = Ready.Value;

    public static string Describe(int error)
    {
        var buffer = stackalloc byte[ErrorBytes];
        return Libav.DescribeError(error, buffer, ErrorBytes) < 0 ? $"error {error}" : Marshal.PtrToStringUTF8((nint)buffer) ?? $"error {error}";
    }

    private static bool Initialize()
    {
        var codec = Libav.CodecVersion() >> 16;
        var util = Libav.UtilVersion() >> 16;
        if (codec != Libav.CodecMajor || util != Libav.UtilMajor)
        {
            throw new InvalidOperationException($"FFmpeg libavcodec {codec} and libavutil {util} are not the pinned {Libav.CodecMajor} and {Libav.UtilMajor}");
        }

        Libav.SetLogLevel(Libav.LogWarning);
        Libav.SetLogCallback(&OnLog);
        return true;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnLog(void* context, int level, byte* format, nint arguments)
    {
        if (level > Libav.LogWarning)
        {
            return;
        }

        var line = stackalloc byte[LineBytes];
        var prefix = 1;
        if (Libav.FormatLogLine(context, level, format, arguments, line, LineBytes, &prefix) < 0)
        {
            return;
        }

        var text = Marshal.PtrToStringUTF8((nint)line)?.TrimEnd();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (level <= Libav.LogError)
        {
            Log.Warn($"ffmpeg: {text}");
        }
        else
        {
            Log.Debug($"ffmpeg: {text}");
        }
    }
}
