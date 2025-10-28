using System;
using System.Runtime.Serialization;

namespace Verifone.ECRTerminal
{
    [Serializable]
    public class ECRTerminalException : Exception
    {
        public ECRTerminalException()
        {
        }

        public ECRTerminalException(string message)
            : base(message)
        {
        }

        public ECRTerminalException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected ECRTerminalException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        // Ensure custom fields can be serialized in derived classes
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            base.GetObjectData(info, context);
        }
    }

    [Serializable]
    public class ECRTerminalSessionException : ECRTerminalException
    {
        public string SessionId { get; private set; }
        public SessionType SessionType { get; private set; }
        public SessionState SessionState { get; private set; }
        public string TransactionId { get; private set; }
        public DateTime CreatedAt { get; private set; }

        public ECRTerminalSessionException() : base("Terminal session error.") //todo: localize
        {
        }
            
        public ECRTerminalSessionException(string message, string sessionId, SessionType sessionType, SessionState sessionState, string transactionId, DateTime transactionDate)
            : this(message, sessionId, sessionType, sessionState, transactionId, transactionDate, null)
        {
        }

        public ECRTerminalSessionException(string message, string sessionId, SessionType sessionType, SessionState sessionState, string transactionId, DateTime transactionDate, Exception innerException)
            : base(message, innerException)
        {
            SessionId = sessionId;
            SessionType = sessionType;
            SessionState = sessionState;
            TransactionId = transactionId;
            CreatedAt = transactionDate;
        }

        protected ECRTerminalSessionException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            SessionId = info.GetString(nameof(SessionId));
            SessionType = (SessionType)info.GetValue(nameof(SessionType), typeof(SessionType));
            SessionState = (SessionState)info.GetValue(nameof(SessionState), typeof(SessionState));
            TransactionId = info.GetString(nameof(TransactionId));
            CreatedAt = info.GetDateTime(nameof(CreatedAt));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            info.AddValue(nameof(SessionId), SessionId);
            info.AddValue(nameof(SessionType), SessionType);
            info.AddValue(nameof(SessionState), SessionState);
            info.AddValue(nameof(TransactionId), TransactionId);
            info.AddValue(nameof(CreatedAt), CreatedAt);

            base.GetObjectData(info, context);
        }
    }
}