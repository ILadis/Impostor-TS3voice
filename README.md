# Impostor TS3voice
*Impostor TS3voice* is a plugin for the Among Us private server [Impostor] that integrates the current game state with a [TeamSpeak 3] server.

The following features are currently implemented:
- Creates a moderated TeamSpeak channel for each game lobby.
- Moves TeamSpeak clients into moderated channels when they join a game lobby.
- Auto mutes/unmutes TeamSpeak clients during meetings.

<br>

In order to function:
- The Among Us player name and TeamSpeak client nickname must match.
- The plugin must be able to access the ServerQuery interface of the TeamSpeak server.

# Installation
1. Set up an [Impostor] server by following the instructions on their GitHub page.
2. [Build](#building) the latest development version of this plugin.
3. Drop the `Impostor-TS3voice.dll` file in the `plugins` folder of your Impostor server.
4. [Configure](#configuration) the plugin and start the [Impostor] server.

# Building
The plugin can be built using [.NET Core](https://docs.microsoft.com/en-us/dotnet/core/install).

Clone the repository and build the `Release` configuration:

```sh
git clone git@github.com:ILadis/Impostor-TS3voice.git
cd Impostor-TS3voice
dotnet build --configuration Release
```

# Configuration
The plugin is configured via environment variables.

`TS3_SERVER_ID`:  
The virtual TeamSpeak server id to use, usually `1`.

`TS3_USERNAME`:  
The username of the [ServerQuery login](https://www.teamspeak3.com/support/teamspeak-3-add-server-query-user.php) to use, e.g. `serveradmin`.

`TS3_PASSWORD`:  
The password of the ServerQuery login.

`TS3_CHANNEL_ORDER`:  
The parent TeamSpeak channel id under which the plugin should create the channels for each game lobby.

[Impostor]: https://github.com/Impostor/Impostor
[TeamSpeak 3]: https://www.teamspeak.com
