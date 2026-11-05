namespace Rikka.Remote.Viewer.Video;

internal static class ShaderCode
{
    public static byte[] Load(string name)
    {
        using var stream = typeof(ShaderCode).Assembly.GetManifestResourceStream($"Shaders.{name}")
            ?? throw new InvalidOperationException($"the shader {name} is missing from the build");
        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }
}
