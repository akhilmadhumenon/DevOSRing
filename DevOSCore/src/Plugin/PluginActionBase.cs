using System;
using System.Threading;
using System.Threading.Tasks;
using Loupedeck;

namespace DevOSRing.Core.Hosting;

/// <summary>
/// Base class for all DevOSRing actions. Wraps the synchronous Loupedeck
/// <see cref="PluginDynamicCommand"/> lifecycle with:
/// <list type="bullet">
///   <item>Async execution (<see cref="ExecuteAsync"/>).</item>
///   <item>Debouncing: presses while a run is in-flight are ignored.</item>
///   <item>State machine (<see cref="ActionState"/>) that drives the button image.</item>
///   <item>Structured error handling that surfaces failures via <see cref="PluginLog"/>.</item>
///   <item>Per-run cancellation token honoured if the user re-presses after a long press.</item>
/// </list>
/// </summary>
public abstract class PluginActionBase : PluginDynamicCommand
{
    private static readonly TimeSpan DefaultRunTimeout = TimeSpan.FromMinutes(5);

    private int _busyFlag;
    private ActionState _state = ActionState.Idle;
    private string _statusText = string.Empty;
    private CancellationTokenSource? _cts;

    protected PluginActionBase(string displayName, string description, string groupName)
        : base(displayName, description, groupName)
    {
    }

    /// <summary>Time-budget for a single press; default 5 min.</summary>
    protected virtual TimeSpan RunTimeout => DefaultRunTimeout;

    /// <summary>Subclasses implement the real work here.</summary>
    protected abstract Task<ActionOutcome> ExecuteAsync(CancellationToken ct);

    /// <summary>
    /// Loupedeck calls this on a worker thread. We treat it as a fire-and-forget
    /// async entry-point and never block the dispatcher.
    /// </summary>
    protected override void RunCommand(string actionParameter)
    {
        if (Interlocked.Exchange(ref _busyFlag, 1) == 1)
        {
            PluginLog.Info($"[{this.DisplayName}] press ignored (already running)");
            return;
        }

        _cts = new CancellationTokenSource(RunTimeout);
        SetState(ActionState.Busy, "Running...");

        _ = Task.Run(async () =>
        {
            try
            {
                var outcome = await ExecuteAsync(_cts.Token).ConfigureAwait(false);
                SetState(outcome.Success ? ActionState.Success : ActionState.Error,
                         outcome.Message ?? (outcome.Success ? "Done" : "Failed"));
            }
            catch (OperationCanceledException)
            {
                PluginLog.Warning($"[{this.DisplayName}] cancelled / timed out");
                SetState(ActionState.Error, "Timed out");
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"[{this.DisplayName}] unhandled error: {ex.Message}");
                SetState(ActionState.Error, Truncate(ex.Message, 32));
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                Interlocked.Exchange(ref _busyFlag, 0);
                _ = Task.Delay(TimeSpan.FromSeconds(3)).ContinueWith(_ =>
                {
                    if (_busyFlag == 0) SetState(ActionState.Idle, string.Empty);
                });
            }
        });
    }

    protected void SetState(ActionState state, string statusText)
    {
        _state = state;
        _statusText = statusText ?? string.Empty;
        this.ActionImageChanged();
    }

    protected ActionState CurrentState => _state;
    protected string CurrentStatusText => _statusText;

    protected override BitmapImage? GetCommandImage(string actionParameter, PluginImageSize imageSize) =>
        PluginImageRenderer.Render(this.DisplayName, _state, _statusText, imageSize);

    protected override string GetCommandDisplayName(string actionParameter, PluginImageSize imageSize) =>
        string.IsNullOrEmpty(_statusText)
            ? this.DisplayName
            : $"{this.DisplayName}{Environment.NewLine}{_statusText}";

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s.Substring(0, max - 1) + "…");
}

public enum ActionState
{
    Idle,
    Busy,
    Success,
    Error
}

/// <summary>Result returned by <see cref="PluginActionBase.ExecuteAsync"/>.</summary>
public readonly record struct ActionOutcome(bool Success, string? Message)
{
    public static ActionOutcome Ok(string? message = null) => new(true, message);
    public static ActionOutcome Fail(string message) => new(false, message);
}
