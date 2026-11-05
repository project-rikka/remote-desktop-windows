using System.Text.Json.Serialization;

namespace Rikka.Remote.Viewer.Hosts;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(List<StoredHost>))]
internal sealed partial class HostsJsonContext : JsonSerializerContext;
