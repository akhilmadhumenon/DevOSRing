using System;
using System.IO;
using System.Runtime.InteropServices;

namespace DevOSRing.Core.Process;

/// <summary>
/// Opens a script in a visible terminal window. Optional; only used by the legacy
/// "demo" code-paths when the user explicitly opts in. Cross-platform.
/// </summary>
public static class TerminalLauncher
{
    public static void Open(string scriptPath)
    {
        if (string.IsNullOrWhiteSpace(scriptPath))
            throw new ArgumentException("scriptPath required", nameof(scriptPath));
        if (!File.Exists(scriptPath))
            throw new FileNotFoundException("Script not found", scriptPath);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            EnsureExecutable(scriptPath);
            System.Diagnostics.Process.Start("/usr/bin/open", new[] { "-a", "Terminal", scriptPath });
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c start \"DevOS\" \"{scriptPath}\"",
                UseShellExecute = true,
            });
        }
        else
        {
            EnsureExecutable(scriptPath);
            System.Diagnostics.Process.Start("/usr/bin/xdg-open", scriptPath);
        }
    }

    private static void EnsureExecutable(string path)
    {
        try
        {
            System.Diagnostics.Process.Start("/bin/chmod", $"+x \"{path}\"")?.WaitForExit(2000);
        }
        catch
        {
            /* best-effort; if chmod fails the user will see a "permission denied" in their terminal */
        }
    }
}
