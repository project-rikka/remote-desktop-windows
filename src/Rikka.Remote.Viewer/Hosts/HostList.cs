using System.Text.Json;
using Rikka.Remote.Quic;

namespace Rikka.Remote.Viewer.Hosts;

internal sealed class HostList
{
    private readonly List<Host> hosts;

    private HostList(List<Host> hosts)
    {
        this.hosts = hosts;
    }

    public static HostList Empty => new([]);

    public static HostList Load(string path)
    {
        if (!File.Exists(path))
        {
            return Empty;
        }

        var stored = JsonSerializer.Deserialize(File.ReadAllBytes(path), HostsJsonContext.Default.ListStoredHost)
            ?? throw new InvalidDataException($"{path} holds no list of desktops");
        return new HostList([.. stored.Select(Restore)]);
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var part = Path.ChangeExtension(path, ".part");
        var stored = hosts.Select(host => new StoredHost(host.Address, host.Name, host.Pin?.ToHex(), host.Used)).ToList();
        using (var file = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(file, stored, HostsJsonContext.Default.ListStoredHost);
            file.Flush(flushToDisk: true);
        }

        File.Move(part, path, overwrite: true);
    }

    public IReadOnlyList<Host> List() =>
        [.. hosts.OrderByDescending(host => host.Used).ThenBy(host => host.Name, StringComparer.Ordinal)];

    public Host? Get(string address) => hosts.Find(host => host.Address == address);

    public void Remember(Host host)
    {
        var index = hosts.FindIndex(known => known.Address == host.Address);
        if (index >= 0)
        {
            hosts[index] = host;
        }
        else
        {
            hosts.Add(host);
        }
    }

    public Host? Forget(string address)
    {
        var index = hosts.FindIndex(host => host.Address == address);
        if (index < 0)
        {
            return null;
        }

        var forgotten = hosts[index];
        hosts.RemoveAt(index);
        return forgotten;
    }

    private static Host Restore(StoredHost stored)
    {
        Pin? pin = null;
        if (stored.Pin is not null && !Pin.TryParse(stored.Pin, out pin))
        {
            throw new InvalidDataException($"{stored.Pin} is not a fingerprint");
        }

        return new Host(stored.Address, stored.Name, pin, stored.Used);
    }
}
