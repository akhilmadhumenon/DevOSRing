using System;
using DevOSRing.Core.Hosting;
using Loupedeck;

namespace Loupedeck.ReviewActionPlugin;

public class ReviewActionPlugin : Plugin
{
    public override bool UsesApplicationApiOnly => true;
    public override bool HasNoApplication => true;

    public ReviewActionPlugin()
    {
        PluginLog.Init(this.Log);
        PluginResources.Init(this.Assembly);
    }

    public override void Load() { }
    public override void Unload() { }
}
