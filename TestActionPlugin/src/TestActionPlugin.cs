using System;
using DevOSRing.Core.Hosting;
using Loupedeck;

namespace Loupedeck.TestActionPlugin;

public class TestActionPlugin : Plugin
{
    public override bool UsesApplicationApiOnly => true;
    public override bool HasNoApplication => true;

    public TestActionPlugin()
    {
        PluginLog.Init(this.Log);
        PluginResources.Init(this.Assembly);
    }

    public override void Load() { }
    public override void Unload() { }
}
