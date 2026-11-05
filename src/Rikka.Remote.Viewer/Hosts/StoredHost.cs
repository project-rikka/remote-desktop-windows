using System.Text.Json.Serialization;

namespace Rikka.Remote.Viewer.Hosts;

internal sealed record StoredHost(
    [property: JsonPropertyName("address")] string Address,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("pin")] string? Pin,
    [property: JsonPropertyName("used")] ulong Used);
