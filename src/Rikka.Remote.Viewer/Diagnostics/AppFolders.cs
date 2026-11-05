namespace Rikka.Remote.Viewer.Diagnostics;

internal static class AppFolders
{
    public static string Data { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Rikka", "RemoteViewer");

    public static string Logs { get; } = Path.Combine(Data, "logs");
}
