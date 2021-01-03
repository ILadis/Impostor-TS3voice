using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;

using Impostor.TS3mod.ServerQuery.Executables;

namespace Impostor.TS3mod.ServerQuery
{
    public interface IExecutable<T> : IExecutable
    {
        public new Task<T> Execute(Session session);

        async Task IExecutable.Execute(Session session) => await Execute(session);
    }

    public interface IExecutable
    {
        public Task Execute(Session session);
    }

    public class Executor
    {
        private delegate Task Execution(Session session);

        private static Execution Terminate = (_) => Task.CompletedTask;

        private BlockingCollection<Execution> queue = new BlockingCollection<Execution>(10);

        public async Task Start(Client client)
        {
            var session = await Session.Create(client);
            using (session)
            {
                var cancel = new CancellationTokenSource();
                var keepAlive = KeepAlive(cancel.Token);

                while (true)
                {
                    var exec = await Task.Run(() => queue.Take());
                    if (exec == Terminate) break;
                    await exec(session);
                }

                cancel.Cancel();
                await keepAlive;
            }
        }

        private async Task KeepAlive(CancellationToken token)
        {
            var exec = new Executables.Version();
            while (true)
            {
                try
                {
                    await Task.Delay(5000, token);
                    queue.Add((session) => exec.Execute(session));
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        public void Stop() => queue.Add(Terminate);

        public void Schedule(IExecutable exec) => queue.Add((session) => exec.Execute(session));

        public Task<T> Schedule<T>(IExecutable<T> exec)
        {
            var result = new TaskCompletionSource<T>();
            queue.Add((session) => RunWithResult(exec, session, result));
            return result.Task;
        }

        public async Task RunWithResult<T>(IExecutable<T> exec, Session session, TaskCompletionSource<T> result)
        {
            try
            {
                result.TrySetResult(await exec.Execute(session));
            }
            catch (Exception exception)
            {
                result.TrySetException(exception);
            }
        }
    }

    public class Session : IDisposable
    {
        private Client client;

        private IAsyncEnumerator<Command> listener;

        public Session(Client client)
        {
            this.client = client;
            this.listener = client.Listen().GetAsyncEnumerator();
        }

        public static async Task<Session> Create(Client client)
        {
            await client.Connect();
            return new Session(client);
        }

        public async Task<Command[]> Exchange(Command command)
        {
            await client.Send(command);
            var commands = new List<Command>();

            while (await listener.MoveNextAsync())
            {
                command = listener.Current;
                commands.Add(command);
                if (command.Name == "error") break;
            }

            return commands.ToArray();
        }

        public void Dispose()
        {
            client.Disconnect();
        }
    }

    public class Client
    {
        public delegate Task<Stream> Connector(string ip, int port);

        private Connector connector;

        private Stream stream;

        public Client() => connector = ConnectTo;

        public Client(Connector connector) => this.connector = connector;

        public async Task Connect() => stream = await connector("127.0.0.1", 10011);

        public void Disconnect() => stream.Close();

        public async IAsyncEnumerable<Command> Listen()
        {
            var parser = new Parser();

            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                await SkipWelcome(reader);

                while (true)
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(line)) continue;

                    var commands = parser.Parse(line + "\n");
                    foreach (var command in commands)
                    {
                        yield return command;
                    }
                }
            }
        }

        private async Task SkipWelcome(StreamReader reader)
        {
            var welcomes = 2;

            while (welcomes > 0)
            {
                var line = await reader.ReadLineAsync();

                if (line.StartsWith("TS3")) welcomes--;
                if (line.StartsWith("Welcome")) welcomes--;
            }
        }

        public async Task Send(Command command)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(command.ToString());
            await stream.WriteAsync(bytes);
        }

        private async Task<Stream> ConnectTo(string ip, int port)
        {
            var address = IPAddress.Parse(ip);
            var endpoint = new IPEndPoint(address, port); 

            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            await socket.ConnectAsync(endpoint);

            return new NetworkStream(socket, true);
        }
    }

    public class Command
    {
        internal string name;

        private Dictionary<string, string> parameters = new Dictionary<string, string>();

        private HashSet<string> options = new HashSet<string>();

        public string Name
        {
            get { return name; }
            set { name = value; }
        }

        public string GetParameter(string key) => parameters.GetValueOrDefault(key, string.Empty);

        public bool TryGetParameter(string key, out string value) => parameters.TryGetValue(key, out value);

        public void AddParameter(string key, string value) => parameters.Add(key, value);

        public bool HasOption(string name) => options.Contains(name);

        public void SetOption(string name) => options.Add(name);

        public static bool IsSuccessful(IEnumerable<Command> commands)
        {
            foreach (var command in commands)
            {
                if (command.Name == "error")
                {
                    var message = command.GetParameter("msg");
                    return message == "ok";
                }
            }

            return false;
        }

        public override string ToString()
        {
            var builder = new StringBuilder();

            if (!string.IsNullOrEmpty(name))
            {
                builder.Append(name).Append(' ');
            }

            foreach (var (key, value) in parameters)
            {
                builder.Append(key).Append('=').AppendEscaped(value).Append(' ');
            }

            foreach (var name in options)
            {
                builder.Append('-').Append(name).Append(' ');
            }

            builder.RemoveLast().Append('\n');
            return builder.ToString();
        }
    }

    public class Parser
    {
        private StringBuilder builder = new StringBuilder();

        public IEnumerable<Command> Parse(string line)
        {
            var cursor = line.GetEnumerator();
            var command = new Command();

            while (NextToken(cursor, out var token))
            {
                if (Characters.IsSeparator(cursor.Current))
                {
                    NextValue(cursor, out var value);
                    command.AddParameter(token, value);
                }

                else if (Characters.IsDash(cursor.Current))
                {
                    NextToken(cursor, out var value);
                    command.SetOption(value);
                }

                else if (Characters.IsTerminator(cursor.Current))
                {
                    command.name = token;
                }

                else throw new FormatException();

                if (Characters.IsSplitterator(cursor.Current))
                {
                    yield return command;
                    command = new Command();
                }
            }

            yield return command;
        }

        private bool NextToken(CharEnumerator cursor, out string value)
        {
            builder.Clear();
            while (true)
            {
                if (!cursor.MoveNext())
                {
                    value = builder.ToString();
                    return false;
                }

                if (!Characters.IsToken(cursor.Current))
                {
                    value = builder.ToString();
                    return true;
                }

                builder.Append(cursor.Current);
            }
        }

        private bool NextValue(CharEnumerator cursor, out string value)
        {
            Characters.Escaper escape = Characters.NoEscape;

            builder.Clear();
            while (true)
            {
                if (!cursor.MoveNext())
                {
                    value = builder.ToString();
                    return false;
                }

                if (Characters.IsEscape(cursor.Current))
                {
                    escape = Characters.Unescape;
                    continue;
                }

                if (Characters.IsTerminator(cursor.Current))
                {
                    value = builder.ToString();
                    return true;
                }

                builder.Append(escape(cursor.Current));
                escape = Characters.NoEscape;
            }
        }
    }

    internal class Characters
    {
        internal delegate char Escaper(char value);

        internal static char NoEscape(char value) => value;

        internal static char Unescape(char value)
        {
            switch (value)
            {
                case 's': return  ' ';
                case 'p': return  '|';
                case 'a': return '\a';
                case 'b': return '\b';
                case 'f': return '\f';
                case 'n': return '\n';
                case 'r': return '\r';
                case 't': return '\t';
                case 'v': return '\v';
                default: return value;
            }
        }

        internal static char Escape(char value)
        {
            switch (value)
            {
                case  ' ': return 's';
                case  '|': return 'p';
                case '\a': return 'a';
                case '\b': return 'b';
                case '\f': return 'f';
                case '\n': return 'n';
                case '\r': return 'r';
                case '\t': return 't';
                case '\v': return 'v';
                default: return value;
            }
        }

        internal static bool IsTerminator(char value) => value == ' ' || value == '|' || value == '\n';

        internal static bool IsSeparator(char value) => value == '=';

        internal static bool IsSplitterator(char value) => value == '|';

        internal static bool IsToken(char value) => (value >= '0' && value <= '9') || (value >= 'a' && value <= 'z') || value == '_';

        internal static bool IsEscape(char value) => value == '\\';

        internal static bool IsDash(char value) => value == '-';

        internal static bool IsSpecial(char value)
        {
            switch (value)
            {
                case  ' ':
                case  '|':
                case '\a':
                case '\b':
                case '\f':
                case '\n':
                case '\r':
                case '\t':
                case '\v':
                    return true;
                default:
                    return false;
            }
        }
    }

    internal static class Extensions
    {
        public static StringBuilder RemoveLast(this StringBuilder builder) => builder.Remove(builder.Length - 1, 1);

        public static StringBuilder AppendEscaped(this StringBuilder builder, string value)
        {
            Characters.Escaper escape = Characters.NoEscape;

            foreach (var character in value)
            {
                if (Characters.IsSpecial(character))
                {
                    builder.Append('\\');
                    escape = Characters.Escape;
                }

                builder.Append(escape(character));
                escape = Characters.NoEscape;
            }

            return builder;
        }
    }
}