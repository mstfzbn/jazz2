﻿#if MULTIPLAYER

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Duality;
using Duality.Async;
using Jazz2.Game;
using Lidgren.Network;

namespace Jazz2.Game
{
    public partial class App
    {
        private static string assemblyPath;

        public static string AssemblyTitle
        {
            get
            {
                object[] attributes = Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), false);
                if (attributes.Length > 0) {
                    AssemblyTitleAttribute titleAttribute = (AssemblyTitleAttribute)attributes[0];
                    if (!string.IsNullOrEmpty(titleAttribute.Title)) {
                        return titleAttribute.Title;
                    }
                }
                return Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location);
            }
        }

        public static string AssemblyVersion
        {
            get
            {
                Version v = Assembly.GetEntryAssembly().GetName().Version;
                return v.Major.ToString(CultureInfo.InvariantCulture) + "." + v.Minor.ToString(CultureInfo.InvariantCulture) + "." + v.Build.ToString(CultureInfo.InvariantCulture) + (v.Revision != 0 ? "." + v.Revision.ToString(CultureInfo.InvariantCulture) : "");
            }
        }

        public static string AssemblyPath
        {
            get
            {
                if (assemblyPath == null) {
#if LINUX_BUNDLE
                    try {
                        assemblyPath = Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
                    } catch {
                        assemblyPath = "";
                    }
#else
                    assemblyPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
#endif
                }

                return assemblyPath;
            }
        }

        public static void GetAssemblyVersionNumber(out byte major, out byte minor, out byte build)
        {
            Version v = Assembly.GetEntryAssembly().GetName().Version;
            major = (byte)v.Major;
            minor = (byte)v.Minor;
            build = (byte)v.Build;
        }
    }
}

namespace Jazz2.Server
{
    internal static partial class App
    {
        private static GameServer gameServer;
        private static Dictionary<string, Func<string, bool>> availableCommands;
        private static int lastUnknownCommandMessageIndex;

        private static void Main(string[] args)
        {
            ConsoleUtils.TryEnableUnicode();

            // Try to render Jazz2 logo
            if (ConsoleImage.RenderFromManifestResource("ConsoleImage.udl", out int imageTop) && imageTop >= 0) {
                int width = Console.BufferWidth;

                // Show version number in the right corner
                string appVersion = "v" + Game.App.AssemblyVersion;

                int currentCursorTop = Console.CursorTop;
                Console.SetCursorPosition(width - appVersion.Length - 2, imageTop + 1);
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine(appVersion);
                Console.ResetColor();
                Console.CursorTop = currentCursorTop;
            }

            // Override working directory
            try {
                Environment.CurrentDirectory = Jazz2.Game.App.AssemblyPath;
            } catch (Exception ex) {
                Log.Write(LogType.Warning, "Cannot override working directory: " + ex);
            }

            // Process parameters
            int port;
            if (!TryRemoveArg(ref args, "/port:", out port)) {
                port = 10666;
            }

            string overrideHostname;
            if (!TryRemoveArg(ref args, "/override-hostname:", out overrideHostname)) {
                overrideHostname = null;
            }

            string name;
            if (!TryRemoveArg(ref args, "/name:", out name) || string.IsNullOrWhiteSpace(name)) {
                name = "Unnamed server";
            }

            int maxPlayers;
            if (!TryRemoveArg(ref args, "/players:", out maxPlayers)) {
                maxPlayers = 64;
            }

            string levelName;
            if (!TryRemoveArg(ref args, "/level:", out levelName)) {
                levelName = "unknown/battle2";
            }

            bool isPrivate = TryRemoveArg(ref args, "/private");
            bool enableUPnP = TryRemoveArg(ref args, "/upnp");

            // Initialization
            Version v = Assembly.GetEntryAssembly().GetName().Version;
            byte neededMajor = (byte)v.Major;
            byte neededMinor = (byte)v.Minor;
            byte neededBuild = (byte)v.Build;

            Log.Write(LogType.Info, "Starting server...");
            Log.PushIndent();

            // Start game server
            DualityApp.Init(DualityApp.ExecutionContext.Server, null, args);

            AsyncManager.Init();

            gameServer = new GameServer();

            if (overrideHostname != null) {
                try {
                    gameServer.OverrideHostname(overrideHostname);
                } catch {
                    Log.Write(LogType.Error, "Cannot set override public hostname!");
                }
            }

            gameServer.Run(port, name, maxPlayers, isPrivate, enableUPnP, neededMajor, neededMinor, neededBuild);

            Log.PopIndent();

            gameServer.ChangeLevel(levelName, MultiplayerLevelType.Battle);

            Log.Write(LogType.Info, "Ready!");
            Log.Write(LogType.Info, "");

            // Processing of console commands
            ProcessConsoleCommands();

            // Shutdown
            Log.Write(LogType.Info, "Closing...");

            gameServer.Dispose();
        }

        private static void ProcessConsoleCommands()
        {
            // Register all available commands
            availableCommands = new Dictionary<string, Func<string, bool>>();

            availableCommands.Add("quit", HandleCommandExit);
            availableCommands.Add("exit", HandleCommandExit);

            availableCommands.Add("help", HandleCommandHelp);
            availableCommands.Add("info", HandleCommandInfo);

            availableCommands.Add("set", HandleCommandSet);

            availableCommands.Add("ban", HandleCommandBan);
            availableCommands.Add("unban", HandleCommandUnban);
            availableCommands.Add("kick", HandleCommandKick);
            availableCommands.Add("kill", HandleCommandKill);

            // Start process command loop
            while (true) {
                string input = Log.FetchLine(GetConsoleSuggestions);
                if (input == null) {
                    break;
                }

                input = input.Trim();

                string command = GetPartFromInput(ref input);

                Func<string, bool> handler;
                if (availableCommands.TryGetValue(command, out handler)) {
                    if (!handler(input)) {
                        break;
                    }
                } else {
                    HandleUnknownCommand();
                }
            }
        }

        private static string GetConsoleSuggestions(string input)
        {
            if (string.IsNullOrEmpty(input)) {
                return null;
            }

            foreach (KeyValuePair<string, Func<string, bool>> pair in availableCommands) {
                if (pair.Key.StartsWith(input, StringComparison.InvariantCultureIgnoreCase)) {
                    if (input == pair.Key) {
                        return null;
                    } else {
                        return pair.Key;
                    }
                }
            }

            return null;
        }

        private static void HandleUnknownCommand()
        {
            string[] answers = new[] {
                "What do you mean?",
                "I really don't know what do you mean.",
                "What?",
                "Wut?",
                "This command never existed.",
                "Sorry, but this command never existed.",
                "Did you type it correctly?",
                "Ensure that you typed it correctly.",
                "What do you need?",
                "I'm not sure what do you need.",
                "Are you sure you typed it correctly?",
                "Please, try it again. Maybe it works out now."
            };

            if (lastUnknownCommandMessageIndex == 11) { // "Please, try it again. Maybe it works out now."
                Log.Write(LogType.Error, "No, it does not...");

                lastUnknownCommandMessageIndex = -1;
                return;
            }

            int idx = MathF.Rnd.Next(answers.Length);
            if (idx == lastUnknownCommandMessageIndex) {
                idx = MathF.Rnd.Next(answers.Length);
            }

            Log.Write(LogType.Error, answers[idx]);

            lastUnknownCommandMessageIndex = idx;
        }

        private static bool HandleCommandExit(string input)
        {
            return false;
        }

        private static bool HandleCommandHelp(string input)
        {
            Log.Write(LogType.Info, "Visit \"http://deat.tk/jazz2/\" for more info!");
            Log.Write(LogType.Info, "Available commands:");
            Log.PushIndent();

            foreach (KeyValuePair<string, Func<string, bool>> pair in availableCommands) {
                Log.Write(LogType.Info, pair.Key);
            }

            Log.PopIndent();
            Log.Write(LogType.Info, "");

            return true;
        }

        private static bool HandleCommandInfo(string input)
        {
            TimeSpan uptime = (DateTime.Now - gameServer.StartedTime);
            Log.Write(LogType.Info, "Uptime: " + uptime);
            Log.Write(LogType.Info, "Server Load: " + gameServer.LoadMs + " ms");
            Log.Write(LogType.Info, "Current Level: " + gameServer.CurrentLevel);

            int playerCount = gameServer.PlayerCount;
            if (playerCount > 0) {
                Log.Write(LogType.Info, "Players (" + playerCount + "/" + gameServer.MaxPlayers + ")".PadRight(12) + "Pos              D / K / Hits Remote Endpoint");
                Log.PushIndent();

                foreach (KeyValuePair<NetConnection, GameServer.PlayerClient> pair in gameServer.Players) {
                    var player = pair.Value;

                    Log.Write(LogType.Info,
                        GameServer.PlayerNameToConsole(player).PadRight(6) + " " +
                        player.State.ToString().PadRight(15) +
                        (player.ProxyActor == null ? 
                            " -                " :
                            " [" + ((int)player.ProxyActor.Transform.Pos.X).ToString().PadLeft(5) + "; " + ((int)player.ProxyActor.Transform.Pos.Y).ToString().PadLeft(5) + "]   ") +
                        player.StatsDeaths.ToString().PadRight(4) +
                        player.StatsKills.ToString().PadRight(4) +
                        player.StatsHits.ToString().PadRight(5) +
                        pair.Key.RemoteEndPoint);
                }
            } else {
                Log.Write(LogType.Info, "Players (0/" + gameServer.MaxPlayers + ")");
            }

            Log.PopIndent();
            Log.Write(LogType.Info, "");

            return true;
        }

        private static bool HandleCommandSet(string input)
        {
            string key = GetPartFromInput(ref input);
            switch (key) {
                case "name": {
                    if (!string.IsNullOrWhiteSpace(input)) {
                        gameServer.Name = input;
                        Log.Write(LogType.Info, "Server name was set to \"" + input + "\"!");
                    } else {
                        Log.Write(LogType.Error, "Cannot set server name to \"" + input + "\"!");
                    }
                    break;
                }

                case "level": {
                    string value = GetPartFromInput(ref input);
                    if (gameServer.ChangeLevel(value, MultiplayerLevelType.Battle)) {
                        Log.Write(LogType.Info, "Level was changed to \"" + value + "\"!");
                    } else {
                        Log.Write(LogType.Error, "Cannot load level \"" + value + "\"!");
                    }
                    break;
                }

                case "only_unique_clients": {
                    string value = GetPartFromInput(ref input);
                    bool enabled = (value == "true" || value == "yes" || value == "1");
                    gameServer.AllowOnlyUniqueClients = enabled;
                    Log.Write(LogType.Info, "Allow only unique clients is " + (enabled ? "enabled" : "disabled") + "!");
                    break;
                }

                case "player_health": {
                    string value = GetPartFromInput(ref input);
                    byte health;
                    if (byte.TryParse(value, out health) && health > 0 && health < 20) {
                        gameServer.SpawnedPlayerHealth = health;
                        Log.Write(LogType.Info, "Player health was changed to " + health + "!");
                    } else {
                        Log.Write(LogType.Info, "Invalid value provided!");
                    }
                    break;
                }

                case "spawning": {
                    string value = GetPartFromInput(ref input);
                    bool enabled = (value == "true" || value == "yes" || value == "1");
                    gameServer.IsPlayerSpawningEnabled = enabled;
                    Log.Write(LogType.Info, "Player spawning is " + (enabled ? "enabled" : "disabled") + "!");
                    break;
                }

                default: {
                    if (string.IsNullOrEmpty(key)) {
                        Log.Write(LogType.Info, "name = " + gameServer.Name);
                        Log.Write(LogType.Info, "level = " + gameServer.CurrentLevel);
                        Log.Write(LogType.Info, "only_unique_clients = " + gameServer.AllowOnlyUniqueClients);
                        Log.Write(LogType.Info, "player_health = " + gameServer.SpawnedPlayerHealth);
                        Log.Write(LogType.Info, "spawning = " + gameServer.IsPlayerSpawningEnabled);
                        Log.Write(LogType.Info, "");
                    } else {
                        HandleUnknownCommand();
                    }
                    break;
                }
            }

            return true;
        }

        private static bool HandleCommandBan(string input)
        {
            // ToDo
            Log.Write(LogType.Error, "Not supported yet!");

            return true;
        }

        private static bool HandleCommandUnban(string input)
        {
            // ToDo
            Log.Write(LogType.Error, "Not supported yet!");

            return true;
        }

        private static bool HandleCommandKick(string input)
        {
            int playerIndex;
            if (input == ":all") {
                gameServer.KickAllPlayers();
                Log.Write(LogType.Info, "All players were kicked from the server!");
            } else if (int.TryParse(input, out playerIndex)) {
                if (gameServer.KickPlayer((byte)playerIndex)) {
                    Log.Write(LogType.Info, "Player was kicked from the server!");
                } else {
                    Log.Write(LogType.Error, "Player was not found!");
                }
            } else {
                Log.Write(LogType.Error, "You have to specify player index! (or :all to kick all players)");
            }

            return true;
        }

        private static bool HandleCommandKill(string input)
        {
            int playerIndex;
            if (input == ":all") {
                gameServer.KillAllPlayers();
                Log.Write(LogType.Info, "All players were killed!");
            } else if (int.TryParse(input, out playerIndex)) {
                if (gameServer.KillPlayer((byte)playerIndex)) {
                    Log.Write(LogType.Info, "Player was killed!");
                } else {
                    Log.Write(LogType.Error, "Player was not found!");
                }
            } else {
                Log.Write(LogType.Error, "You have to specify player index! (or :all to kill all players)");
            }

            return true;
        }

        private static string GetPartFromInput(ref string input)
        {
            if (input == null) {
                return null;
            }

            string part;
            int idx = input.IndexOf(' ');
            if (idx == -1) {
                part = input;
                input = null;
            } else {
                part = input.Substring(0, idx);
                input = input.Substring(idx + 1);
            }
            return part;
        }

        public static bool TryRemoveArg(ref string[] args, string arg)
        {
            for (int i = 0; i < args.Length; i++) {
                if (string.Compare(args[i], arg, StringComparison.OrdinalIgnoreCase) == 0) {
                    List<string> list = new List<string>(args);
                    list.RemoveAt(i);
                    args = list.ToArray();
                    return true;
                }
            }

            return false;
        }

        public static bool TryRemoveArg(ref string[] args, string argPrefix, out string argSuffix)
        {
            for (int i = 0; i < args.Length; i++) {
                if (args[i].StartsWith(argPrefix, StringComparison.OrdinalIgnoreCase)) {
                    argSuffix = args[i].Substring(argPrefix.Length);

                    List<string> list = new List<string>(args);
                    list.RemoveAt(i);
                    args = list.ToArray();
                    return true;
                }
            }

            argSuffix = null;
            return false;
        }

        public static bool TryRemoveArg(ref string[] args, string argPrefix, out int argSuffix)
        {
            string suffix;
            if (TryRemoveArg(ref args, argPrefix, out suffix) && int.TryParse(suffix, out argSuffix)) {
                return true;
            }

            argSuffix = 0;
            return false;
        }
    }
}

#else

namespace Jazz2.Server
{
    public class App
    {
        public static void Main()
        {
            System.Console.WriteLine("Multiplayer is disabled in this build configuration!");
        }
    }
}

#endif