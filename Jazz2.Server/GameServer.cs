﻿#if MULTIPLAYER

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using Jazz2.Networking.Packets;
using Jazz2.Networking.Packets.Client;
using Jazz2.Networking.Packets.Server;
using Lidgren.Network;

namespace Jazz2.Server
{
    public partial class GameServer : IDisposable
    {
        private const string Token = "J²";
        private const string ServerListUrl = "http://deat.tk/jazz2/servers/";

        private object sync = new object();

        private string name;
        private int port;
        private int maxPlayers;

        private Thread threadGame, threadPublishToServerList;
        private ServerConnection server;
        private byte neededMajor, neededMinor, neededBuild;

        private Dictionary<byte, Action<NetIncomingMessage, bool>> callbacks;

        public int LoadMs => lastGameLoadMs;
        public int PlayerCount => players.Count;
        public int MaxPlayers => maxPlayers;
        public string CurrentLevel => currentLevel;
        public Dictionary<NetConnection, Player> Players => players;

        public void Run(int port, string name, int maxPlayers, bool isPrivate, byte neededMajor, byte neededMinor, byte neededBuild)
        {
            this.port = port;
            this.name = name;
            this.maxPlayers = maxPlayers;

            this.neededMajor = neededMajor;
            this.neededMinor = neededMinor;
            this.neededBuild = neededBuild;

            callbacks = new Dictionary<byte, Action<NetIncomingMessage, bool>>();
            players = new Dictionary<NetConnection, Player>();
            playerConnections = new List<NetConnection>();

            objects = new Dictionary<int, Object>();

            server = new ServerConnection(Token, port, maxPlayers);
            server.MessageReceived += OnMessageReceived;
            server.DiscoveryRequest += OnDiscoveryRequest;
            server.ClientConnected += OnClientConnected;
            server.ClientStatusChanged += OnClientStatusChanged;

            RegisterCallback<LevelReady>(OnLevelReady);
            RegisterCallback<UpdateSelf>(OnUpdateSelf);

            // Create game loop
            threadGame = new Thread(OnGameLoop);
            threadGame.IsBackground = true;
            threadGame.Start();

            // Publish to server list
            if (!isPrivate) {
                Log.Write(LogType.Info, "Publishing to server list...");

                threadPublishToServerList = new Thread(OnPublishToServerList);
                threadPublishToServerList.IsBackground = true;
                threadPublishToServerList.Start();
            }
        }

        public void Dispose()
        {
            if (server == null) {
                return;
            }

            server.ClientStatusChanged -= OnClientStatusChanged;
            server.ClientConnected -= OnClientConnected;
            server.MessageReceived -= OnMessageReceived;
            server.DiscoveryRequest -= OnDiscoveryRequest;

            //ClearCallbacks();

            Thread threadGame_ = threadGame;
            threadGame = null;
            threadGame_.Join();

            server.Close();
            server = null;
        }

        public void ChangeLevel(string levelName)
        {
            lock (sync) {
                currentLevel = levelName;

                foreach (KeyValuePair<NetConnection, Player> pair in players) {
                    pair.Value.State = PlayerState.NotReady;
                }

                playerConnections.Clear();

                foreach (KeyValuePair<NetConnection, Player> pair in players) {
                    Send(new LoadLevel {
                        LevelName = currentLevel,
                        AssignedPlayerIndex = pair.Value.Index
                    }, 64, pair.Key, NetDeliveryMethod.ReliableUnordered, PacketChannels.Main);
                }
            }
        }

        private void OnPublishToServerList()
        {
            bool isPublished = true;

            while (threadPublishToServerList != null) {
                try {
                    string currentVersion = Jazz2.App.AssemblyVersion;

                    IPAddress mask;
                    string endpoint = NetUtility.GetMyAddress(out mask).ToString() + ":" + port;

                    string dataString = "0|" + endpoint + "|" + currentVersion + "|" + players.Count + "|" + maxPlayers + "|" + name;
                    string data = "add=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(dataString))
                                .Replace('+', '-').Replace('/', '_').TrimEnd('=');

                    WebClient client = new WebClient();
                    client.Encoding = Encoding.UTF8;
                    client.Headers["User-Agent"] = Jazz2.App.AssemblyTitle;
                    client.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";

                    string content = client.UploadString(ServerListUrl, data);
                    if (content.Contains("\"r\":false")) {
                        if (content.Contains("\"e\":1")) {
                            Log.Write(LogType.Warning, "Cannot publish server with private IP address (" + endpoint + ")!");
                        } else if (content.Contains("\"e\":2")) {
                            Log.Write(LogType.Error, "Access to server list is denied! Try it later.");
                        } else {
                            Log.Write(LogType.Warning, "Server cannot be published to server list!");
                        }
                        return;
                    } else if (!isPublished) {
                        Log.Write(LogType.Error, "Server was successfully published again!");
                    }

                    isPublished = true;
                } catch {
                    // Try it again later
                    if (isPublished) {
                        isPublished = false;
                        Log.Write(LogType.Error, "Server list is unreachable!");
                    }
                }

                Thread.Sleep(300000); // 5 minutes
            }
        }
    }
}

#endif