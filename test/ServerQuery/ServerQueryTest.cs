using Xunit;

using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

using Impostor.TS3mod.Test;

namespace Impostor.TS3mod.ServerQuery
{
    public class ClientTest
    {
        [Fact]
        public async Task Listen_ServerRespondsWithCommands_ReturnsCommands()
        {
            // arrange
            var response = ""
                + "TS3\r\n"
                + "Welcome to the TeamSpeak 3 ServerQuery interface.\r\n"
                + "channelcreate channel_name=test channel_order=1 channel_needed_talk_power=200 channel_flag_permanent=0\r\n"
                + "cid=21\r\n"
                + "error id=0 msg=ok\r\n";

            var server = new ServerStream { Response = response };

            // act
            var client = new Client((_, __) => Task.FromResult<Stream>(server));
            await client.Connect();

            var listener = client.Listen();
            var commands = await ReceiveExact(listener, 3);

            client.Disconnect();

            // assert
            Assert.Equal("channelcreate", commands[0].Name);
            Assert.Equal("21", commands[1].GetParameter("cid"));
            Assert.Equal("error", commands[2].Name);
            Assert.Equal("ok", commands[2].GetParameter("msg"));
        }

        private async Task<List<Command>> ReceiveExact(IAsyncEnumerable<Command> listener, int count)
        {
            var commands = new List<Command>();
            await foreach (var command in listener)
            {
                commands.Add(command);
                if (--count <= 0) break;
            }
            return commands;
        }
    }

    public class CommandTest
    {
        [Fact]
        public void Build_CommandWithoutParametersOrOptions_ReturnsCommand()
        {
            // act
            var command = new Command { Name = "clientlist" };

            // assert
            Assert.Equal("clientlist", command.Name);
            Assert.Equal("clientlist\n", command.ToString());
        }

        [Fact]
        public void Build_CommandWithOptions_ReturnsCommand()
        {
            // act
            var command = new Command { Name = "clientlist" };
            command.SetOption("uid");
            command.SetOption("away");

            // assert
            Assert.Equal("clientlist", command.Name);
            Assert.True(command.HasOption("uid"));
            Assert.True(command.HasOption("away"));
            Assert.False(command.HasOption("groups"));
            Assert.Equal("clientlist -uid -away\n", command.ToString());
        }

        [Fact]
        public void Build_CommandWithParameters_ReturnsCommand()
        {
            // act
            var command = new Command { Name = "channelmove" };
            command.AddParameter("cid", "16");
            command.AddParameter("cpid", "1");
            command.AddParameter("order", "0");

            // assert
            Assert.Equal("channelmove", command.Name);
            Assert.Equal("16", command.GetParameter("cid"));
            Assert.Equal("1", command.GetParameter("cpid"));
            Assert.Equal("0", command.GetParameter("order"));
            Assert.Equal("channelmove cid=16 cpid=1 order=0\n", command.ToString());
        }
    }

    public class ParserTest
    {
        [Fact]
        public void Parse_CommandWithoutParametersOrOptions_Parsed()
        {
            // arrange
            var parser = new Parser();

            // act
            var commands = parser.Parse("login\n").GetEnumerator();

            // assert
            Assert.True(commands.MoveNext());
            Assert.Equal("login", commands.Current.Name);
            Assert.False(commands.MoveNext());
        }

        [Fact]
        public void Parse_MalformedCommand_FormatExceptionThrown()
        {
            // arrange
            var parser = new Parser();

            // act & assert
            Assert.Throws<FormatException>(() => parser.Parse("clientmöve\n").GetEnumerator().MoveNext());
        }

        [Fact]
        public void Parse_CommandWithParameters_Parsed()
        {
            // arrange
            var parser = new Parser();

            // act
            var commands = parser.Parse("clientmove clid=5 cid=1\n").GetEnumerator();

            // assert
            Assert.True(commands.MoveNext());
            Assert.Equal("clientmove", commands.Current.Name);
            Assert.Equal("5", commands.Current.GetParameter("clid"));
            Assert.Equal("1", commands.Current.GetParameter("cid"));
            Assert.False(commands.MoveNext());
        }

        [Fact]
        public void Parse_ParametersOnly_Parsed()
        {
            // arrange
            var parser = new Parser();

            // act
            var commands = parser.Parse("cid=21\n").GetEnumerator();

            // assert
            Assert.True(commands.MoveNext());
            Assert.Null(commands.Current.Name);
            Assert.Equal("21", commands.Current.GetParameter("cid"));
            Assert.False(commands.MoveNext());
        }

        [Fact]
        public void Parse_CommandWithEscapedParameters_ParsedAndUnescaped()
        {
            // arrange
            var parser = new Parser();

            // act
            var commands = parser.Parse("sendtextmessage targetmode=2 msg=hello\\sworld\\p\\p\n").GetEnumerator();

            // assert
            Assert.True(commands.MoveNext());
            Assert.Equal("sendtextmessage", commands.Current.Name);
            Assert.Equal("2", commands.Current.GetParameter("targetmode"));
            Assert.Equal("hello world||", commands.Current.GetParameter("msg"));
            Assert.False(commands.MoveNext());
        }

        [Fact]
        public void Parse_CommandWithOptions_Parsed()
        {
            // arrange
            var parser = new Parser();

            // act
            var commands = parser.Parse("clientlist -uid -away\n").GetEnumerator();

            // assert
            Assert.True(commands.MoveNext());
            Assert.Equal("clientlist", commands.Current.Name);
            Assert.True(commands.Current.HasOption("uid"));
            Assert.True(commands.Current.HasOption("away"));
            Assert.False(commands.Current.HasOption("group"));
            Assert.False(commands.MoveNext());
        }

        [Theory]
        [InlineData("command key1=value1 key2=value2 -option1 -option2\n")]
        [InlineData("key1=value1 command -option1 key2=value2 -option2\n")]
        [InlineData("-option2 key1=value1 -option1 key2=value2 command\n")]
        public void Parse_CommandWithParametersAndOptions_Parsed(string line)
        {
            // arrange
            var parser = new Parser();

            // act
            var commands = parser.Parse(line).GetEnumerator();;

            // assert
            Assert.True(commands.MoveNext());
            Assert.Equal("command", commands.Current.Name);
            Assert.Equal("value1", commands.Current.GetParameter("key1"));
            Assert.Equal("value2", commands.Current.GetParameter("key2"));
            Assert.True(commands.Current.HasOption("option1"));
            Assert.True(commands.Current.HasOption("option2"));
            Assert.False(commands.MoveNext());
        }

        [Fact]
        public void Parse_CommandWithListItems_Parse()
        {
            // arrange
            var parser = new Parser();

            // act
            var commands = parser.Parse("channeldelperm cid=16 permid=17276|permid=21415\n").GetEnumerator();

            // assert
            Assert.True(commands.MoveNext());
            Assert.Equal("channeldelperm", commands.Current.Name);
            Assert.Equal("16", commands.Current.GetParameter("cid"));
            Assert.Equal("17276", commands.Current.GetParameter("permid"));
            Assert.True(commands.MoveNext());
            Assert.Equal("21415", commands.Current.GetParameter("permid"));
            Assert.False(commands.MoveNext());
        }

        [Fact]
        public void Parse_CommandWithListItemsOnly_Parse()
        {
            // arrange
            var parser = new Parser();

            // act
            var commands = parser.Parse("cid=15 channel_name=Channel\\s1|cid=16 channel_name=Channel\\s2\n").GetEnumerator();

            // assert
            Assert.True(commands.MoveNext());
            Assert.Null(commands.Current.Name);
            Assert.Equal("15", commands.Current.GetParameter("cid"));
            Assert.True(commands.MoveNext());
            Assert.Equal("16", commands.Current.GetParameter("cid"));
            Assert.False(commands.MoveNext());
        }
    }
}