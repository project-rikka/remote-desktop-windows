using System.Globalization;
using System.Text;

namespace Rikka.Remote.Viewer.Diagnostics;

internal static class Log
{
    private const string FileName = "viewer.log";
    private const string PreviousFileName = "viewer.1.log";

    private static readonly Lock Gate = new();
    private static StreamWriter? writer;

    public static void Open()
    {
        var folder = AppFolders.Logs;
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, FileName);
            if (File.Exists(path))
            {
                File.Move(path, Path.Combine(folder, PreviousFileName), overwrite: true);
            }

            writer = Create(path);
        }
        catch (IOException)
        {
            writer = Create(Path.Combine(folder, string.Create(CultureInfo.InvariantCulture, $"viewer-{Environment.ProcessId}.log")));
        }
    }

    public static void Debug(string message) => Write("debug", message);

    public static void Info(string message) => Write("info", message);

    public static void Warn(string message) => Write("warn", message);

    public static void Error(string message) => Write("error", message);

    private static StreamWriter Create(string path) =>
        new(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };

    private static void Write(string level, string message)
    {
        var line = string.Create(CultureInfo.InvariantCulture, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level} [{Environment.CurrentManagedThreadId}] {message}");
        System.Diagnostics.Debug.WriteLine(line);
        lock (Gate)
        {
            writer?.WriteLine(line);
        }
    }
}
