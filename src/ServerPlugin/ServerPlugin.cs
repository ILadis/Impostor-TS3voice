using System;
using System.Threading.Tasks;

using Impostor.Api.Plugins;
using Impostor.Api.Events.Managers;
using Impostor.TS3mod.ServerQuery;
using Impostor.TS3mod.Plugin.Listeners;
using Impostor.TS3mod.Plugin.Configuration;

using Microsoft.Extensions.Logging;

namespace Impostor.TS3mod.Plugin
{
    [ImpostorPlugin(
        package: "de.ladis.impostor.plugin",
        name: "ImpostorTS3mod",
        author: "ladis",
        version: "1.0.0")]
    public class ServerPlugin : PluginBase
    {
        private ILogger<ServerPlugin> logger;

        private IEventManager eventManager;

        private Executor executor;

        private IDisposable listener;

        public ServerPlugin(ILogger<ServerPlugin> logger, IEventManager eventManager)
        {
            this.logger = logger;
            this.eventManager = eventManager;
        }

        public override ValueTask EnableAsync()
        {
            logger.LogInformation("Plugin is being enabled.");

            var client = new Client();
            executor = new Executor();
            executor.Start(client).ContinueWith((_) => logger.LogInformation("Executor stopped."));

            var config = EnvironmentConfiguration.Instance;
            listener = eventManager.RegisterListener(new GameEventListener(logger, config, executor));

            return default;
        }

        public override ValueTask DisableAsync()
        {
            logger.LogInformation("Plugin is being disabled.");

            listener.Dispose();
            executor.Stop();

            return default;
        }
    }
}