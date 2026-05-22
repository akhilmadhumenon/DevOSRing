using System;
using Loupedeck;

namespace DevOSRing.Core.Hosting;

/// <summary>
/// Forwards log calls to the host-supplied <see cref="PluginLogFile"/>. Each plugin
/// installs its own instance from <c>this.Log</c> at plugin construction time.
/// </summary>
public static class PluginLog
{
    private static PluginLogFile? _log;

    public static void Init(PluginLogFile log)
    {
        if (log is null) throw new ArgumentNullException(nameof(log));
        _log = log;
    }

    public static void Verbose(string text) => _log?.Verbose(text);
    public static void Verbose(Exception ex, string text) => _log?.Verbose(ex, text);

    public static void Info(string text) => _log?.Info(text);
    public static void Info(Exception ex, string text) => _log?.Info(ex, text);

    public static void Warning(string text) => _log?.Warning(text);
    public static void Warning(Exception ex, string text) => _log?.Warning(ex, text);

    public static void Error(string text) => _log?.Error(text);
    public static void Error(Exception ex, string text) => _log?.Error(ex, text);
}
