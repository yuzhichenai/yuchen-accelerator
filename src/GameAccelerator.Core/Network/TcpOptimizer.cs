namespace GameAccelerator.Core.Network;

public class TcpOptimizer
{
    public bool IsAdmin()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    public bool Apply()
    {
        if (!IsAdmin()) return false;

        try
        {
            // Apply TCP optimization via registry
            const string TcpipParams = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters";

            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(TcpipParams, true);
            if (key == null) return false;

            // Enable CTCP (Compound TCP) - good for high-latency networks
            key.SetValue("CongestionControlProvider", "ctcp");

            // TCP Auto-tuning
            key.SetValue("TCPAutoTuning", "normal");

            return true;
        }
        catch
        {
            return false;
        }
    }
}
