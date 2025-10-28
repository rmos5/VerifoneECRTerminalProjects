using System;
using System.Runtime.Serialization;

namespace Verifone.ECRTerminal
{
    /// <summary>
    /// Base exception type for ECR terminal errors.
    /// </summary>
    [Serializable]
    public class ECRTerminalException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ECRTerminalException"/> class.
        /// </summary>
        public ECRTerminalException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ECRTerminalException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public ECRTerminalException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ECRTerminalException"/> class with a specified error message and inner exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public ECRTerminalException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ECRTerminalException"/> class with serialized data.
        /// </summary>
        /// <param name="info">The <see cref="SerializationInfo"/> that holds the serialized object data.</param>
        /// <param name="context">The <see cref="StreamingContext"/> containing the source and destination context.</param>
        protected ECRTerminalException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        /// <summary>
        /// Populates a <see cref="SerializationInfo"/> with the data needed to serialize the target object.
        /// </summary>
        /// <param name="info">The <see cref="SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The <see cref="StreamingContext"/> containing the source and destination context.</param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            base.GetObjectData(info, context);
        }
    }

    /// <summary>
    /// Exception type for errors related to terminal sessions.
    /// </summary>
    [Serializable]
    public class ECRTerminalSessionException : ECRTerminalException
    {
        /// <summary>
        /// Gets the session identifier.
        /// </summary>
        public string SessionId { get; private set; }

        /// <summary>
        /// Gets the session type.
        /// </summary>
        public SessionType SessionType { get; private set; }

        /// <summary>
        /// Gets the session state.
        /// </summary>
        public SessionState SessionState { get; private set; }

        /// <summary>
        /// Gets the transaction identifier.
        /// </summary>
        public string TransactionId { get; private set; }

        /// <summary>
        /// Gets the transaction creation timestamp.
        /// </summary>
        public DateTime CreatedAt { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ECRTerminalSessionException"/> class.
        /// </summary>
        public ECRTerminalSessionException() : base("Terminal session error.") //todo: localize
        {
        }
            
        /// <summary>
        /// Initializes a new instance of the <see cref="ECRTerminalSessionException"/> class with session details.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="sessionId">The session identifier.</param>
        /// <param name="sessionType">The session type.</param>
        /// <param name="sessionState">The session state.</param>
        /// <param name="transactionId">The transaction identifier.</param>
        /// <param name="transactionDate">The transaction timestamp.</param>
        public ECRTerminalSessionException(string message, string sessionId, SessionType sessionType, SessionState sessionState, string transactionId, DateTime transactionDate)
            : this(message, sessionId, sessionType, sessionState, transactionId, transactionDate, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ECRTerminalSessionException"/> class with session details and inner exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="sessionId">The session identifier.</param>
        /// <param name="sessionType">The session type.</param>
        /// <param name="sessionState">The session state.</param>
        /// <param name="transactionId">The transaction identifier.</param>
        /// <param name="transactionDate">The transaction timestamp.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public ECRTerminalSessionException(string message, string sessionId, SessionType sessionType, SessionState sessionState, string transactionId, DateTime transactionDate, Exception innerException)
            : base(message, innerException)
        {
            SessionId = sessionId;
            SessionType = sessionType;
            SessionState = sessionState;
            TransactionId = transactionId;
            CreatedAt = transactionDate;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ECRTerminalSessionException"/> class with serialized data.
        /// </summary>
        /// <param name="info">The <see cref="SerializationInfo"/> that holds the serialized object data.</param>
        /// <param name="context">The <see cref="StreamingContext"/> containing the source and destination context.</param>
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

        /// <summary>
        /// Populates a <see cref="SerializationInfo"/> with the data needed to serialize the target object.
        /// </summary>
        /// <param name="info">The <see cref="SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The <see cref="StreamingContext"/> containing the source and destination context.</param>
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