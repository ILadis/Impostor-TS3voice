namespace Impostor.TS3mod.Plugin.Configuration
{
    public interface IConfiguration
    {
        public string ServerId { get; }

        public string Username { get; }

        public string Password { get; }

        public string ChannelOrder { get; }
    }

    public class EnvironmentConfiguration : IConfiguration
    {
        public static readonly IConfiguration Instance = new EnvironmentConfiguration();

        public string ServerId { get => System.Environment.GetEnvironmentVariable("TS3_SERVER_ID"); }

        public string Username { get => System.Environment.GetEnvironmentVariable("TS3_USERNAME"); }

        public string Password { get => System.Environment.GetEnvironmentVariable("TS3_PASSWORD"); }

        public string ChannelOrder { get => System.Environment.GetEnvironmentVariable("TS3_CHANNEL_ORDER"); }
    }
}
