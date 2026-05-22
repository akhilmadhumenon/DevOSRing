using System;
using Loupedeck;

namespace Loupedeck.AIRefactorPlugin;

/// <summary>
/// DevOSRing plugins are API-only and not bound to a single host app; this stub
/// satisfies the Loupedeck SDK requirement of having a <see cref="ClientApplication"/>.
/// </summary>
public class AIRefactorApplication : ClientApplication
{
    protected override string GetProcessName() => "";
    protected override string GetBundleName() => "";
    public override ClientApplicationStatus GetApplicationStatus() => ClientApplicationStatus.Unknown;
}
