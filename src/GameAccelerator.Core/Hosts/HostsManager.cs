using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GameAccelerator.Core.Hosts;

public class HostsManager
{
    private const string HostsPath = @"C:\Windows\System32\drivers\etc\hosts";
    private const string StartMarker = "# GA-START";
    private const string EndMarker = "# GA-END";

    private readonly string _backupDir;

    public HostsManager(string backupDir)
    {
        _backupDir = backupDir;
    }

    public bool IsAdmin()
    {
        try
        {
            // Try to open hosts for writing
            using var fs = File.Open(HostsPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public async Task<bool> ApplyEntriesAsync(List<HostsEntry> entries)
    {
        try
        {
            await BackupAsync();
            var currentLines = (await File.ReadAllLinesAsync(HostsPath)).ToList();

            // Remove old GA entries
            RemoveGaSection(currentLines);

            // Add new GA entries
            currentLines.Add(StartMarker);
            foreach (var entry in entries)
            {
                if (entry.IsActive)
                    currentLines.Add(entry.ToString());
            }
            currentLines.Add(EndMarker);

            await File.WriteAllLinesAsync(HostsPath, currentLines);
            await FlushDnsAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> RemoveEntriesAsync()
    {
        try
        {
            var currentLines = (await File.ReadAllLinesAsync(HostsPath)).ToList();
            RemoveGaSection(currentLines);
            await File.WriteAllLinesAsync(HostsPath, currentLines);
            await FlushDnsAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public Task BackupAsync()
    {
        Directory.CreateDirectory(_backupDir);
        var backupPath = Path.Combine(_backupDir, $"hosts.backup.{DateTime.Now:yyyyMMddHHmmss}");
        try
        {
            File.Copy(HostsPath, backupPath, true);
        }
        catch { }
        return Task.CompletedTask;
    }

    public async Task<List<HostsEntry>> GetCurrentGaEntriesAsync()
    {
        var entries = new List<HostsEntry>();
        try
        {
            var lines = await File.ReadAllLinesAsync(HostsPath);
            bool inGaSection = false;

            foreach (var line in lines)
            {
                if (line.Trim() == StartMarker) { inGaSection = true; continue; }
                if (line.Trim() == EndMarker) { inGaSection = false; continue; }
                if (inGaSection)
                {
                    var entry = HostsEntry.Parse(line);
                    if (entry != null) entries.Add(entry);
                }
            }
        }
        catch { }
        return entries;
    }

    private static void RemoveGaSection(List<string> lines)
    {
        bool inGaSection = false;
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            if (lines[i].Trim() == EndMarker) inGaSection = true;
            if (lines[i].Trim() == StartMarker) { lines.RemoveAt(i); inGaSection = false; continue; }
            if (inGaSection) lines.RemoveAt(i);
        }
    }

    private static async Task FlushDnsAsync()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ipconfig",
                Arguments = "/flushdns",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            using var proc = Process.Start(psi);
            await (proc?.WaitForExitAsync() ?? Task.CompletedTask);
        }
        catch { }
    }
}
