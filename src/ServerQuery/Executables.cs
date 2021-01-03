using System.Threading.Tasks;
using System.Collections.Generic;

namespace Impostor.TS3mod.ServerQuery.Executables
{
    public class Version : IExecutable
    {
        public async Task Execute(Session session)
        {
            var command = new Command { Name = "version" };
            await session.ExecuteVerbose(command);
        }
    }

    public class Login : IExecutable
    {
        public string Username { get; set; }

        public string Password { get; set; }

        public string ServerId { get; set; }

        public async Task Execute(Session session)
        {
            var command1 = new Command { Name = "login" };
            command1.AddParameter("client_login_name", Username);
            command1.AddParameter("client_login_password", Password);

            await session.ExecuteVerbose(command1);

            var command2 = new Command { Name = "use" };
            command2.AddParameter("sid", ServerId);

            await session.ExecuteVerbose(command2);
        }
    }

    public class FindChannel : IExecutable<string>
    {
        public string ChannelName { get; set; }

        public async Task<string> Execute(Session session)
        {
            var command = new Command { Name = "channelfind" };
            command.AddParameter("pattern", ChannelName);

            var responses = await session.ExecuteVerbose(command);

            foreach (var response in responses)
            {
                if (response.TryGetParameter("cid", out var channelId))
                {
                    return channelId;
                }
            }

            return "-1";
        }
    }

    public class CreateChannel : IExecutable<string>
    {
        public string ChannelName { get; set; }

        public string ChannelOrder { get; set; }

        public async Task<string> Execute(Session session)
        {
            var command = new Command { Name = "channelcreate" };
            command.AddParameter("channel_name", ChannelName);
            command.AddParameter("channel_order", ChannelOrder);
            command.AddParameter("channel_needed_talk_power", "200");
            command.AddParameter("channel_flag_permanent", "1");

            var responses = await session.ExecuteVerbose(command);

            foreach (var response in responses)
            {
                if (response.TryGetParameter("cid", out var channelId))
                {
                    return channelId;
                }
            }

            return "-1";
        }
    }

    public class DeleteChannel : IExecutable<string>
    {
        public string ChannelName { get; set; }

        public async Task<string> Execute(Session session)
        {
            var channelId = await new FindChannel { ChannelName = ChannelName }.Execute(session);

            var command = new Command { Name = "channeldelete" };
            command.AddParameter("cid", channelId);
            command.AddParameter("force", "1");

            await session.ExecuteVerbose(command);

            return channelId;
        }
    }

    public class SelfClientId : IExecutable<string>
    {
        public async Task<string> Execute(Session session)
        {
            var command = new Command { Name = "whoami" };
            var responses = await session.ExecuteVerbose(command);

            foreach (var response in responses)
            {
                if (response.TryGetParameter("client_id", out var clientId))
                {
                    return clientId;
                }
            }

            return "-1";
        }
    }

    public class FindClients : IExecutable<string[]>
    {
        public string ByChannelId { get; set; }

        public string ByNickname { get; set; }

        public async Task<string[]> Execute(Session session)
        {
            var command = new Command { Name = "clientlist" };
            var responses = await session.ExecuteVerbose(command);

            var clientIds = new List<string>();

            foreach (var response in responses)
            {
                var channelId = response.GetParameter("cid");
                var clientId = response.GetParameter("clid");
                var nickname = response.GetParameter("client_nickname");

                if (channelId == ByChannelId)
                {
                    clientIds.Add(clientId);
                }

                else if (nickname == ByNickname)
                {
                    clientIds.Add(clientId);
                }
            }

            return clientIds.ToArray();
        }
    }

    public class MoveClient : IExecutable
    {
        public string Nickname { get; set; }

        public string ChannelName { get; set; }

        public async Task Execute(Session session)
        {
            var clientIds = await new FindClients { ByNickname = Nickname }.Execute(session);
            var channelId = await new FindChannel { ChannelName = ChannelName }.Execute(session);

            foreach (var clientId in clientIds)
            {
                var command = new Command { Name = "clientmove" };
                command.AddParameter("clid", clientId);
                command.AddParameter("cid", channelId);
        
                await session.ExecuteVerbose(command);
            }
        }
    }

    public class SetTalker : IExecutable
    {
        public string ClientId { get; set; }

        public bool IsTalker { get; set; }

        public async Task Execute(Session session)
        {
            var command = new Command { Name = "clientedit" };
            command.AddParameter("clid", ClientId);
            command.AddParameter("client_is_talker", IsTalker ? "1" : "0");

            await session.ExecuteVerbose(command);
        }
    }

    public class MuteClient : IExecutable
    {
        public string Nickname { get; set; }

        protected bool isTalker = false;

        public async Task Execute(Session session)
        {
            var clientIds = await new FindClients { ByNickname = Nickname }.Execute(session);

            foreach (var clientId in clientIds)
            {
                await new SetTalker { ClientId = clientId, IsTalker = isTalker }.Execute(session);
            }
        }
    }

    public class UnmuteClient : MuteClient
    {
        public UnmuteClient() => isTalker = true;
    }

    public class MuteAll : IExecutable
    {
        public string ChannelName { get; set; }

        protected bool isTalker = false;

        public async Task Execute(Session session)
        {
            var channelId = await new FindChannel { ChannelName = ChannelName }.Execute(session);
            var clientIds = await new FindClients { ByChannelId = channelId }.Execute(session);

            foreach (var clientId in clientIds)
            {
                await new SetTalker { ClientId = clientId, IsTalker = isTalker }.Execute(session);
            }
        }
    }

    public class UnmuteAll : MuteAll
    {
        public UnmuteAll() => isTalker = true;
    }

    internal static class Extensions
    {
        // TODO use logger provided to server plugin instead
        public static async Task<Command[]> ExecuteVerbose(this Session session, Command command)
        {
            System.Console.Write($"> {command}  ");
            var responses = await session.Exchange(command);
            System.Console.WriteLine(Command.IsSuccessful(responses) ? "success!" : "failed!");
            return responses;
        }
    }
}