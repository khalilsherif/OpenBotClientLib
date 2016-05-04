using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;
using TwitchIRCLib;
using OpenBot.Plugins;
using OpenBot.Plugins.Interfaces;
using OpenBot.Models;
using OpenBot.Collections;
using System.Diagnostics;
using System.Data;
using System.IO;
using System.Data.SQLite;
using System.Security.Cryptography;
using System.Reflection;

namespace OpenBot
{
    public class OpenBot : AbstractBotAdapter
    {
        private IRCClient _irc;
        private FullCommandReceivedDelegate _masterDelegate;
        private PluginManager _pluginManager;
        private ChatUser _currentUser;
        private List<ChatUser> _userList;

        private const int RATELIMIT_NORMAL = 20;
        private const int RATELIMIT_MODERATOR = 100;

        private int _rateLimit = RATELIMIT_NORMAL;
        private TimeSpan period = new TimeSpan(0, 0, 31);
        private ConcurrentQueue<string> _commandQueue;
        private TimeSpanList<string> _sentCommands;
        public PluginManager PluginManager
        {
            get
            {
                return _pluginManager;
            }
        }

        public bool DebugOutput { get; set; }

        public override IChatUser CurrentUser
        {
            get
            {
                return _currentUser;
            }
        }

        public override IChatUser[] UserList
        {
            get
            {
                return _userList.ToArray();
            }
        }

        public override string ChannelName
        {
            get
            {
                return _irc.Channel;
            }
        }

        public OpenBot()
        {
            _sentCommands = new TimeSpanList<string>(period);
            _commandQueue = new ConcurrentQueue<string>();

            _userList = new List<ChatUser>();

            _masterDelegate = _MasterCallback;

            _irc = new IRCClient();

            _irc.MasterCallback = _masterDelegate;
            AddCommandCallback("PRIVMSG", MasterMessageCallback);
            AddCommandCallback("USERSTATE", UserStateUpdatedCallback);
            AddCommandCallback("JOIN", UserJoinCallback);
            AddCommandCallback("PART", UserPartCallback);
            AddCommandCallback("353", UserNamesCallback);
            AddCommandCallback("MODE", UserModeCallback);
            AddCommandCallback("PING", PingCallback);
        }

        public void InitializePluginManager()
        {
            if (!_irc.Connected)
                throw new IRCConnectionException("You must connect to the IRC before trying to initialize the plugin manager");

            _pluginManager = new PluginManager();
            _pluginManager.AddBaseAssembly(typeof(SQLiteException).Assembly.GetName());
            _pluginManager.AddBaseAssembly(typeof(SQLiteConnection).Assembly.GetName());
            _pluginManager.LoadAllPluginAssemblies(this);

            _pluginManager.InitializeAllPlugins();
            _pluginManager.InitializeAllServices();
        }
        private void PingCallback(string[] args, string command, IRCClient irc_client)
        {
            _irc.SendCommand(string.Format("PONG {0}", args[1]));
        }

        private void UserModeCallback(string[] args, string command, IRCClient irc_client)
        {
            if (args.Length < 5) return;

            if (args[3] == "+o")
            {
                string username = args[4].ToLower();
                if (_userList.Where((a) => a.Username.ToLower() == username).Count() == 0)
                    _userList.Add(new ChatUser(username, _irc.Channel, true));
                else
                    _userList.Where((a) => a.Username.ToLower() == username).First().SetModerator(true);
            }
        }

        private void UserNamesCallback(string[] args, string command, IRCClient irc_client)
        {
            if (args.Length < 6)
                return;
            string[] usernames = args[5].Split(' ');

            foreach (string i in usernames)
            {
                if (_userList.Where((a) => a.Username.ToLower() == i.ToLower()).Count() == 0)
                    _userList.Add(new ChatUser(i.ToLower(), _irc.Channel));
            }
        }

        private void UserJoinCallback(string[] args, string command, IRCClient irc_client)
        {
            string delimiter = "!";
            if (!args[0].Contains(delimiter))
                return;

            string username = args[0].Substring(0, args[0].IndexOf(delimiter)).ToLower();

            if (_userList.Where((a) => a.Username.ToLower() == username).Count() == 0)
                _userList.Add(new ChatUser(username, _irc.Channel));
        }

        private void UserPartCallback(string[] args, string command, IRCClient irc_client)
        {
            string delimiter = "!";
            if (!args[0].Contains(delimiter))
                return;

            string username = args[0].Substring(0, args[0].IndexOf(delimiter)).ToLower();

            _userList.RemoveAll((a) => a.Username.ToLower() == username);
        }

        private void ProcessMessageQueue()
        {
            while (_irc.Connected)
            {
                while (_sentCommands.Count() < _rateLimit - 1 && _commandQueue.Count() > 0)
                {
                    string command;
                    bool result = _commandQueue.TryDequeue(out command);

                    if (result)
                    {
                        _irc.SendCommand(command);
                        _sentCommands.Add(command);
                    }

                    Thread.Sleep(_currentUser.Moderator ? 2 : (int)(period.TotalMilliseconds / _rateLimit));
                }
                Thread.Sleep(5);
            }
        }
        private void UserStateUpdatedCallback(string[] args, string command, IRCClient irc_client)
        {
            string delimiter = "mod=";
            if (!args[0].Contains(delimiter))
                return;

            bool isMod = args[0][args[0].IndexOf(delimiter) + delimiter.Length] == '1';
            _currentUser.SetModerator(isMod);

            _rateLimit = isMod ? RATELIMIT_MODERATOR : RATELIMIT_NORMAL;
        }

        private void MasterMessageCallback(string[] args, string command, IRCClient irc_client)
        {
            string tags = args[0];
            string username = args[1].Remove(0, 1).Split('!')[0].ToLower();
            string channel = args[3];
            string message = args[4];


            string delimiter = "mod=";
            if (!tags.Contains(delimiter))
                return;

            bool isMod = tags[tags.IndexOf(delimiter) + delimiter.Length] == '1';

            if (_userList.Where((a) => a.Username.ToLower() == username).Count() == 0)
                _userList.Add(new ChatUser(username, _irc.Channel, isMod));
            else
                _userList.Where((a) => a.Username.ToLower() == username).First().SetModerator(isMod);

            _userList.RemoveAll((a) => a.Username.ToLower() == _currentUser.Username.ToLower());

            bool handled = false;

            ChatUser sender;
            if (username.ToLower() == _currentUser.Username.ToLower())
                sender = _currentUser;
            else
                sender = _userList.Where((a) => a.Username.ToLower() == username.ToLower()).First();

            foreach (var i in PluginManager.PluginAssemblies)
            {
                handled = i.HandleMessageIfMatched(sender, message, args, command, handled) || handled;
            }
        }

        public void Connect()
        {
            if (!_irc.Connect())
                throw new IRCConnectionException("Unable to connect to Twitch IRC");

            _irc.SendCommand("CAP REQ :twitch.tv/membership");
            _irc.SendCommand("CAP REQ :twitch.tv/commands");
            _irc.SendCommand("CAP REQ :twitch.tv/tags");
        }

        public void BeginListen()
        {
            _irc.BeginListenAsync();
        }

        public void Login(string username, string token)
        {
            if (_irc.Connected)
            {
                _irc.Login(username, token);
                _currentUser = new ChatUser(username, _irc.Channel);

                Thread T = new Thread(ProcessMessageQueue);
                T.IsBackground = true;
                T.Start();
            }
        }

        public void JoinChannel(string channel)
        {
            if (_irc.Connected)
                _irc.JoinChannel(channel);
        }

        public void Disconnect()
        {
            _irc.Disconnect();
        }

        protected void AddCommandCallback(string command, CommandReceivedDelegate callback)
        {
            if (_irc.Callbacks.ContainsKey(command))
                _irc.Callbacks[command] += callback;
            else
                _irc.Callbacks[command] = callback;
        }

        protected void RemoveCommandCallback(string command, CommandReceivedDelegate callback)
        {
            if (_irc.Callbacks.ContainsKey(command))
                _irc.Callbacks[command] -= callback;
        }
        private void _MasterCallback(int callbackIndex, string[] args, string command, IRCClient client)
        {
            bool handled = false;

            foreach (var i in PluginManager.PluginAssemblies)
            {
                handled = i.HandleRawCommandIfMatched(callbackIndex, args, command, handled) || handled;
            }

            if (DebugOutput)
            {
                string output = string.Format("[{0}] {1} received: {2}", DateTime.Now.ToLongTimeString(), client.Username, command);
                Debug.WriteLine(output);
            }
        }

        public void SendCommand(IPlugin sender, string command)
        {
            _commandQueue.Enqueue(command);
        }

        public void SendMessage(IPlugin sender, string message)
        {
            _commandQueue.Enqueue(_irc.GetMessageCommand(message));
        }

        public override IDbConnection GetDatabase(int index)
        {
            HashAlgorithm algorithm = MD5.Create();

            byte[] hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(Assembly.GetCallingAssembly().GetName().Name));

            string fileName = "";
            foreach (byte i in hash)
                fileName += i.ToString("X2");

            fileName += index;

            fileName += ".sqlite";

            string databaseFolder = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "Databases");

            if (!Directory.Exists(databaseFolder))
                Directory.CreateDirectory(databaseFolder);

            string connectionString = string.Format("Data Source={0};Version=3;", Path.Combine(databaseFolder, fileName));

            SQLiteConnection conn = new SQLiteConnection(connectionString);
            return conn;
        }

        public override string GetFilePath(int index)
        {
            HashAlgorithm algorithm = MD5.Create();

            byte[] hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(Assembly.GetCallingAssembly().GetName().Name));

            string fileName = "";
            foreach (byte i in hash)
                fileName += i.ToString("X2");

            fileName += index;

            fileName += ".obf";

            string filesFolder = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "Storage");

            if (!Directory.Exists(filesFolder))
                Directory.CreateDirectory(filesFolder);

            return Path.Combine(filesFolder, fileName);
        }

        public override void SendMessage(string message)
        {
            _commandQueue.Enqueue(_irc.GetMessageCommand(message));
        }

        public override void SendCommand(string command)
        {
            _commandQueue.Enqueue(command);
        }

        public override bool RequestOAuthKey(out string oauthKey, string reason = "")
        {
            throw new NotImplementedException();
        }
    }
}
