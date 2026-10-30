using System.Diagnostics;
using System.Text;

namespace NovaNet.Services;

/// <summary>Runs external tools (netsh, PowerShell, ipconfig) without flashing a console window.</summary>
public static class Sh
{
    public static async Task<(int Code, string Output)> RunAsync(string file, string args, int timeoutMs = 60000)
    {
        var psi = new ProcessStartInfo(file, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        try
        {
            using var p = Process.Start(psi)!;
            var o = p.StandardOutput.ReadToEndAsync();
            var e = p.StandardError.ReadToEndAsync();
            using var cts = new CancellationTokenSource(timeoutMs);
            try { await p.WaitForExitAsync(cts.Token); }
            catch (OperationCanceledException)
            {
                try { p.Kill(true); } catch { }
                return (-1, "timeout");
            }
            return (p.ExitCode, ((await o) + (await e)).Trim());
        }
        catch (Exception ex)
        {
            return (-1, ex.Message);
        }
    }

    public static Task<(int Code, string Output)> Netsh(string args) => RunAsync("netsh.exe", args);

    /// <summary>Runs a PowerShell script (encoded, so quoting never breaks).</summary>
    public static Task<(int Code, string Output)> PS(string script, int timeoutMs = 90000)
    {
        var b64 = Convert.ToBase64String(Encoding.Unicode.GetBytes("$ProgressPreference='SilentlyContinue';" + script));
        return RunAsync("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + b64, timeoutMs);
    }
}
