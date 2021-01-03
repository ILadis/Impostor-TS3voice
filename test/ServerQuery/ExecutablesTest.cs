using Xunit;

using System.IO;
using System.Threading.Tasks;

using Impostor.TS3mod.ServerQuery;
using Impostor.TS3mod.ServerQuery.Executables;
using Impostor.TS3mod.Test;

namespace Impostor.TS3mod.Plugin.Listeners
{
    public class GameEventHandlerTest
    {
        [Fact]
        public async Task Execute_LoginExecutableSuccessful_SendsCommands()
        {
            // arrange
            var server = SetupServer(out var executor, out var session);
            var exec = new Login { ServerId = "1", Username = "serveradmin", Password = "123456" };

            server.Response = ""
                + "TS3\r\n"
                + "Welcome to the TeamSpeak 3 ServerQuery interface.\r\n"
                + "error id=0 msg=ok\r\n"
                + "error id=0 msg=ok\r\n";

            // act
            executor.Schedule(exec);
            executor.Stop();
            await session;

            // assert
            Assert.Equal(""
                + "login client_login_name=serveradmin client_login_password=123456\n"
                + "use sid=1\n"
                + "", server.Receive);
        }

        [Fact]
        public async Task Execute_FindChannelExecutableSuccessful_SendsCommandAndReturnsFirstChannelId()
        {
            // arrange
            var server = SetupServer(out var executor, out var session);
            var exec = new FindChannel { ChannelName = "Test Channel" };

            server.Response = ""
                + "TS3\r\n"
                + "Welcome to the TeamSpeak 3 ServerQuery interface.\r\n"
                + "cid=15 channel_name=Test\\sChannel\\s1|"
                + "cid=16 channel_name=Test\\sChannel\\s12|"
                + "cid=17 channel_name=Test\\sChannel\\s123\r\n"
                + "error id=0 msg=ok\r\n";

            // act
            var channelId = await executor.Schedule(exec);
            executor.Stop();
            await session;

            // assert
            Assert.Equal("15", channelId);
            Assert.Equal("channelfind pattern=Test\\sChannel\n", server.Receive);
        }

        [Fact]
        public async Task Execute_FindChannelExecutableErroneous_ReturnsError()
        {
            // arrange
            var server = SetupServer(out var executor, out var session);
            var exec = new FindChannel { ChannelName = "Test Channel" };

            server.Response = ""
                + "TS3\r\n"
                + "Welcome to the TeamSpeak 3 ServerQuery interface.\r\n"
                + "error id=768 msg=invalid\\schannelID\r\n";

            // act
            var channelId = await executor.Schedule(exec);
            executor.Stop();
            await session;

            // assert
            Assert.Equal("-1", channelId);
        }

        [Fact]
        public async Task Execute_CreateChannelExecutableSuccessful_SendsCommandAndReturnsCreatedChannelId()
        {
            // arrange
            var server = SetupServer(out var executor, out var session);
            var exec = new CreateChannel { ChannelOrder = "1", ChannelName = "Test Channel 123" };

            server.Response = ""
                + "TS3\r\n"
                + "Welcome to the TeamSpeak 3 ServerQuery interface.\r\n"
                + "cid=21\r\n"
                + "error id=0 msg=ok\r\n";

            // act
            var channelId = await executor.Schedule(exec);
            executor.Stop();
            await session;

            // assert
            Assert.Equal("21", channelId);
            Assert.Equal(""
                + "channelcreate channel_name=Test\\sChannel\\s123 channel_order=1 channel_needed_talk_power=200 channel_flag_permanent=1\n"
                + "", server.Receive);
        }

        [Fact]
        public async Task Execute_CreateChannelExecutableErroneous_ReturnsError()
        {
            // arrange
            var server = SetupServer(out var executor, out var session);
            var exec = new CreateChannel { ChannelOrder = "1", ChannelName = "Test Channel 123" };

            server.Response = ""
                + "TS3\r\n"
                + "Welcome to the TeamSpeak 3 ServerQuery interface.\r\n"
                + "error id=678 msg=insufficient\\sclient\\spermissions\r\n";

            // act
            var channelId = await executor.Schedule(exec);
            executor.Stop();
            await session;

            // assert
            Assert.Equal("-1", channelId);
        }

        [Fact]
        public async Task Execute_DeleteChannelExecutableSuccessful_SendsCommandAndReturnsDeletedChannelId()
        {
            // arrange
            var server = SetupServer(out var executor, out var session);
            var exec = new DeleteChannel { ChannelName = "Test Channel 123" };

            server.Response = ""
                + "TS3\r\n"
                + "Welcome to the TeamSpeak 3 ServerQuery interface.\r\n"
                + "cid=34\r\n"
                + "error id=0 msg=ok\r\n"
                + "error id=0 msg=ok\r\n";

            // act
            var channelId = await executor.Schedule(exec);
            executor.Stop();
            await session;

            // assert
            Assert.Equal("34", channelId);
            Assert.Equal(""
                + "channelfind pattern=Test\\sChannel\\s123\n"
                + "channeldelete cid=34 force=1\n"
                + "", server.Receive);
        }

        [Theory]
        [InlineData("6", new string[] { "90", "88" })]
        [InlineData("1", new string[] { "93" })]
        [InlineData("3", new string[] { })]
        public async Task Execute_FindClientsByChannelIdExecutableSuccessful_SendsCommandAndReturnsFoundClientIds(string channelId, string[] expectedIds)
        {
            // arrange
            var server = SetupServer(out var executor, out var session);
            var exec = new FindClients { ByChannelId = channelId };

            server.Response = ""
                + "TS3\r\n"
                + "Welcome to the TeamSpeak 3 ServerQuery interface.\r\n"
                + "clid=93 cid=1 client_database_id=1 client_nickname=serveradmin client_type=1|"
                + "clid=90 cid=6 client_database_id=247 client_nickname=User\\s1 client_type=0|"
                + "clid=88 cid=6 client_database_id=2 client_nickname=User\\s2 client_type=0\r\n"
                + "error id=0 msg=ok\r\n"
                + "error id=0 msg=ok\r\n";

            // act
            var clientIds = await executor.Schedule(exec);
            executor.Stop();
            await session;

            // assert
            Assert.Equal(expectedIds, clientIds);
            Assert.Equal("clientlist\n", server.Receive);
        }

        [Theory]
        [InlineData("User 1", new string[] { "90" })]
        [InlineData("User 2", new string[] { "88" })]
        [InlineData("User", new string[] { })]
        public async Task Execute_FindClientsByNicknameExecutableSuccessful_SendsCommandAndReturnsFoundClientIds(string nickname, string[] expectedIds)
        {
            // arrange
            var server = SetupServer(out var executor, out var session);
            var exec = new FindClients { ByNickname = nickname };

            server.Response = ""
                + "TS3\r\n"
                + "Welcome to the TeamSpeak 3 ServerQuery interface.\r\n"
                + "clid=93 cid=1 client_database_id=1 client_nickname=serveradmin client_type=1|"
                + "clid=90 cid=6 client_database_id=247 client_nickname=User\\s1 client_type=0|"
                + "clid=88 cid=6 client_database_id=2 client_nickname=User\\s2 client_type=0\r\n"
                + "error id=0 msg=ok\r\n"
                + "error id=0 msg=ok\r\n";

            // act
            var clientIds = await executor.Schedule(exec);
            executor.Stop();
            await session;

            // assert
            Assert.Equal(expectedIds, clientIds);
            Assert.Equal("clientlist\n", server.Receive);
        }

        [Fact]
        public async Task Execute_MoveClientExecutableSuccessful_SendsCommands()
        {
            // arrange
            var server = SetupServer(out var executor, out var session);
            var exec = new MoveClient { Nickname = "User 1", ChannelName = "Test Channel 12" };

            server.Response = ""
                + "TS3\r\n"
                + "Welcome to the TeamSpeak 3 ServerQuery interface.\r\n"
                + "clid=93 cid=1 client_database_id=1 client_nickname=serveradmin client_type=1|"
                + "clid=90 cid=6 client_database_id=247 client_nickname=User\\s1 client_type=0|"
                + "clid=88 cid=6 client_database_id=2 client_nickname=User\\s2 client_type=0\r\n"
                + "error id=0 msg=ok\r\n"
                + "cid=16 channel_name=Test\\sChannel\\s12|"
                + "cid=17 channel_name=Test\\sChannel\\s123\r\n"
                + "error id=0 msg=ok\r\n"
                + "error id=0 msg=ok\r\n";

            // act
            executor.Schedule(exec);
            executor.Stop();
            await session;

            // assert
            Assert.Equal(""
                + "clientlist\n"
                + "channelfind pattern=Test\\sChannel\\s12\n"
                + "clientmove clid=90 cid=16\n"
                + "", server.Receive);
        }

        [Theory]
        [InlineData(true, "1")]
        [InlineData(false, "0")]
        public async Task Execute_SetTalkerExecutableSuccessful_SendsCommands(bool talker, string expectedFlag)
        {
            // arrange
            var server = SetupServer(out var executor, out var session);
            var exec = new SetTalker { ClientId = "12", IsTalker = talker };

            server.Response = ""
                + "TS3\r\n"
                + "Welcome to the TeamSpeak 3 ServerQuery interface.\r\n"
                + "error id=0 msg=ok\r\n";

            // act
            executor.Schedule(exec);
            executor.Stop();
            await session;

            // assert
            Assert.Equal($"clientedit clid=12 client_is_talker={expectedFlag}\n", server.Receive);
        }

        [Fact]
        public async Task Execute_MuteClientExecutableSuccessful_SendsCommands()
        {
            // arrange
            var server = SetupServer(out var executor, out var session);
            var exec = new MuteClient { Nickname = "User 2" };

            server.Response = ""
                + "TS3\r\n"
                + "Welcome to the TeamSpeak 3 ServerQuery interface.\r\n"
                + "clid=93 cid=1 client_database_id=1 client_nickname=serveradmin client_type=1|"
                + "clid=90 cid=6 client_database_id=247 client_nickname=User\\s1 client_type=0|"
                + "clid=88 cid=6 client_database_id=2 client_nickname=User\\s2 client_type=0\r\n"
                + "error id=0 msg=ok\r\n"
                + "error id=0 msg=ok\r\n";

            // act
            executor.Schedule(exec);
            executor.Stop();
            await session;

            // assert
            Assert.Equal(""
                + "clientlist\n"
                + "clientedit clid=88 client_is_talker=0\n"
                + "", server.Receive);
        }

        [Fact]
        public async Task Execute_MuteAllExecutableSuccessful_SendsCommands()
        {
            // arrange
            var server = SetupServer(out var executor, out var session);
            var exec = new MuteAll { ChannelName = "Test Channel 12" };

            server.Response = ""
                + "TS3\r\n"
                + "Welcome to the TeamSpeak 3 ServerQuery interface.\r\n"
                + "cid=6 channel_name=Test\\sChannel\\s12|"
                + "cid=7 channel_name=Test\\sChannel\\s123\r\n"
                + "error id=0 msg=ok\r\n"
                + "clid=93 cid=1 client_database_id=1 client_nickname=serveradmin client_type=1|"
                + "clid=90 cid=6 client_database_id=247 client_nickname=User\\s1 client_type=0|"
                + "clid=88 cid=6 client_database_id=2 client_nickname=User\\s2 client_type=0\r\n"
                + "error id=0 msg=ok\r\n"
                + "error id=0 msg=ok\r\n"
                + "error id=0 msg=ok\r\n";

            // act
            executor.Schedule(exec);
            executor.Stop();
            await session;

            // assert
            Assert.Equal(""
                + "channelfind pattern=Test\\sChannel\\s12\n"
                + "clientlist\n"
                + "clientedit clid=90 client_is_talker=0\n"
                + "clientedit clid=88 client_is_talker=0\n"
                + "", server.Receive);
        }

        private ServerStream SetupServer(out Executor executor, out Task session)
        {
            var server = new ServerStream();
            var client = new Client((_, __) => Task.FromResult<Stream>(server));

            executor = new Executor();
            session = executor.Start(client);

            return server;
        }
    }
}