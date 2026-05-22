using System;
using DevOSRing.Core.Hosting;
using DevOSRing.Core.Settings;
using DevOSRing.Core.Llm;
using Loupedeck;

namespace Loupedeck.AIRefactorPlugin;

public class AIRefactorPlugin : Plugin
{
    public override bool UsesApplicationApiOnly => true;
    public override bool HasNoApplication => true;

    public AIRefactorPlugin()
    {
        PluginLog.Init(this.Log);
        PluginResources.Init(this.Assembly);
    }

    public override void Load()
    {
        var settings = LlmSettings.Load(this);
        DevOSRing.Core.Hosting.PluginLog.Info(
            $"[AIRefactor] LLM configured: {settings.IsConfigured} (endpoint={settings.Endpoint}, model={settings.Model})");
    }

    public override void Unload() { }
}
