using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenBot.Plugins.Interfaces;

namespace OpenBot.Models
{
    public class ChatUser : MarshalByRefObject, IChatUser
    {
        private bool _moderator;
        private string _username;
        private DateTime _timeJoined;
        private string _channel;
        public bool Moderator
        {
            get
            {
                return _moderator;
            }
        }

        public DateTime TimeJoined
        {
            get
            {
                return _timeJoined;
            }
        }

        public string Username
        {
            get
            {
                return _username;
            }
        }

        public string Channel
        {
            get
            {
                return _channel;
            }
        }

        public bool Streamer
        {
            get
            {
                return Username.ToLower() == Channel.ToLower().TrimStart('#');
            }
        }

        public void SetModerator(bool isMod)
        {
            _moderator = isMod;
        }

        public ChatUser(string username, string channel) : this(username, channel, false, DateTime.Now) { }

        public ChatUser(string username, string channel, bool moderator) : this(username, channel, moderator, DateTime.Now) { }
        public ChatUser(string username, string channel, bool moderator, DateTime timeJoined)
        {
            _moderator = moderator;
            _username = username;
            _channel = channel;
            _timeJoined = timeJoined;
        }
    }
}
