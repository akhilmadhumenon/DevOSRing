using Loupedeck;

namespace Loupedeck.GitCommitPushPlugin;

public class GitCommitPushApplication : ClientApplication
{
    protected override string GetProcessName() => "";
    protected override string GetBundleName() => "";
    public override ClientApplicationStatus GetApplicationStatus() => ClientApplicationStatus.Unknown;
}
