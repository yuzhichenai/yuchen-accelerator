namespace GameAccelerator.Core.Hosts;

public class HostsEntry
{
    public string IpAddress { get; set; } = "127.0.0.1";
    public string Hostname { get; set; } = "";
    public string Comment { get; set; } = "";
    public bool IsActive { get; set; } = true;

    public override string ToString()
    {
        var line = $"{IpAddress} {Hostname}";
        if (!string.IsNullOrEmpty(Comment))
            line += $" # {Comment}";
        return line;
    }

    public static HostsEntry? Parse(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
            return null;

        var commentIdx = line.IndexOf('#');
        var comment = commentIdx >= 0 ? line[(commentIdx + 1)..].Trim() : "";
        var effective = commentIdx >= 0 ? line[..commentIdx] : line;

        var parts = effective.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return null;

        return new HostsEntry
        {
            IpAddress = parts[0],
            Hostname = parts[1],
            Comment = comment,
            IsActive = true
        };
    }
}
