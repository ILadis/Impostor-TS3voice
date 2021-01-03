using Impostor.Api.Events;
using Impostor.Api.Events.Meeting;
using Impostor.TS3mod.ServerQuery;
using Impostor.TS3mod.ServerQuery.Executables;
using Impostor.TS3mod.Plugin.Configuration;

using Microsoft.Extensions.Logging;

namespace Impostor.TS3mod.Plugin.Listeners
{
    public class GameEventListener : IEventListener
    {
        private ILogger<ServerPlugin> logger;

        private IConfiguration config;

        private Executor executor;

        public GameEventListener(ILogger<ServerPlugin> logger, IConfiguration config, Executor executor)
        {
            this.logger = logger;
            this.config = config;
            this.executor = executor;
        }

        [EventListener]
        public async void OnGameCreated(IGameCreatedEvent evt)
        {
            var code = evt.Game.Code;
            logger.LogInformation($"[{code}] Game created, preparing voice channel.");

            // TODO do not login every time a game is created
            var login = new Login { ServerId = config.ServerId, Username = config.Username, Password = config.Password };
            executor.Schedule(login);

            var create = new CreateChannel { ChannelOrder = config.ChannelOrder, ChannelName = $"Impostor {code}" };
            var id = await executor.Schedule(create);

            logger.LogInformation($"[{code}] Voice channel created: id={id}");
        }

        [EventListener]
        public void OnPlayerJoined(IGamePlayerJoinedEvent evt)
        {
            var code = evt.Game.Code;
            var players = evt.Game.Players;

            logger.LogInformation($"[{code}] Player joined, moving clients into voice channel.");

            foreach (var player in players)
            {
                // must use p.Client here since p.Character is not available yet
                var name = player.Client.Name;
                logger.LogInformation($"Moving client: {name}");

                var move = new MoveClient { ChannelName = $"Impostor {code}", Nickname = name };
                executor.Schedule(move);

                var unmute = new UnmuteClient { Nickname = name };
                executor.Schedule(unmute);
            }
        }

        [EventListener]
        public void OnGameStarted(IGameStartedEvent evt)
        {
            var code = evt.Game.Code;
            logger.LogInformation($"[{code}] Game started, muting all clients.");

            var mute = new MuteAll { ChannelName = $"Impostor {code}" };
            executor.Schedule(mute);
        }

        [EventListener]
        public void OnMeetingStarted(IMeetingStartedEvent evt)
        {
            var code = evt.Game.Code;
            var players = evt.Game.Players;

            logger.LogInformation($"[{code}] Meeting has started, unmuting alive clients.");

            foreach (var player in players)
            {
                var name = player.Character.PlayerInfo.PlayerName;
                var dead = player.Character.PlayerInfo.IsDead;

                if (!dead)
                {
                    logger.LogInformation($"Unmuting client: {name}");

                    var unmute = new UnmuteClient { Nickname = name };
                    executor.Schedule(unmute);
                }
            }
        }

        [EventListener]
        public void OnMeetingEnded(IMeetingEndedEvent evt)
        {
            var code = evt.Game.Code;
            logger.LogInformation($"[{code}] Meeting has ended, muting all clients.");

            var mute = new MuteAll { ChannelName = $"Impostor {code}" };
            executor.Schedule(mute);
        }

        [EventListener]
        public void OnGameEnded(IGameEndedEvent evt)
        {
            var code = evt.Game.Code;
            logger.LogInformation($"[{code}] Game has ended, unmuting all clients.");

            var mute = new UnmuteAll { ChannelName = $"Impostor {code}" };
            executor.Schedule(mute);
        }

        [EventListener]
        public async void OnGameDestroyed(IGameDestroyedEvent evt)
        {
            var code = evt.Game.Code;
            logger.LogInformation($"[{code}] Game destroyed, deleting voice channel.");

            var delete = new DeleteChannel { ChannelName = $"Impostor {code}" };
            var id = await executor.Schedule(delete);

            logger.LogInformation($"[{code}] Voice channel deleted: id={id}");
        }
    }
}