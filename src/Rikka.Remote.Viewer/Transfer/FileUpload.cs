using Microsoft.Win32.SafeHandles;
using Rikka.Remote.Quic;
using Rikka.Remote.Wire;

namespace Rikka.Remote.Viewer.Transfer;

internal static class FileUpload
{
    private const int Chunk = 256 * 1024;

    public static async Task SendAsync(QuicConnection connection, uint id, string path, CancellationToken cancellation)
    {
        using var file = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.SequentialScan);
        var size = RandomAccess.GetLength(file);
        var header = new FileHeader(id, Path.GetFileName(path), (ulong)size);
        var stream = connection.OpenSendStream(StreamKind.File);
        try
        {
            var bytes = new byte[header.EncodedLength];
            header.Encode(bytes);
            if (!stream.Send(bytes))
            {
                throw new QuicException("the desktop is gone");
            }

            var offset = 0L;
            while (offset < size)
            {
                var at = offset;
                var written = await stream.WriteAsync(Chunk, destination => Read(file, destination, at), cancellation).ConfigureAwait(false);
                if (written == 0)
                {
                    throw new IOException($"{path} became shorter while it was being sent");
                }

                offset += written;
            }

            await stream.FinishAsync(cancellation).ConfigureAwait(false);
        }
        catch
        {
            stream.Abort();
            throw;
        }
    }

    private static int Read(SafeFileHandle file, Span<byte> destination, long offset) => RandomAccess.Read(file, destination, offset);
}
