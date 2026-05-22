using System;
using DevOSRing.Core.Hosting;
using Loupedeck;

namespace Loupedeck.GitCommitPushPlugin;

public class GitCommitPushPlugin : Plugin
{
    public override bool UsesApplicationApiOnly => true;
    public override bool HasNoApplication => true;

    public GitCommitPushPlugin()
    {
        PluginLog.Init(this.Log);
        PluginResources.Init(this.Assembly);
    }

    public override void Load() { }
    public override void Unload() { }
}
