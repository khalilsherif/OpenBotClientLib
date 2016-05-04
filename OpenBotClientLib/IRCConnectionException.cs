using System;
using System.Runtime.Serialization;

namespace OpenBot
{
    [Serializable]
    internal class IRCConnectionException : Exception
    {
        public IRCConnectionException()
        {
        }

        public IRCConnectionException(string message) : base(message)
        {
        }

        public IRCConnectionException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected IRCConnectionException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}