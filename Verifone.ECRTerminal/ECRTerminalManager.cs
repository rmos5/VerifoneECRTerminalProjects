using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;

namespace Verifone.ECRTerminal
{
    /// <summary>
    /// Default implementation of <see cref="IECRTerminalManager"/> for Verifone ECR terminals.
    /// Manages sessions, serial protocol, and event routing.
    /// </summary>
    public partial class ECRTerminalManager : IECRTerminalManager
    {
        /// <summary>
        /// In-memory ring buffer of recent terminal sessions, guarded by <see cref="_sessionsLock"/>.
        /// </summary>
        private readonly LinkedList<TerminalSession> _sessions;
        /// <summary>
        /// Maximum number of sessions to keep in <see cref="_sessions"/> before trimming the oldest.
        /// </summary>
        private readonly int _maxSessions;
        /// <summary>
        /// UI callback handler for user prompts (manual auth codes, confirmations, etc.).
        /// </summary>
        private IUserPromptHandler _userPromptHandler;
        /// <summary>
        /// Optional directory path used to persist completed payment/refund/reversal results.
        /// </summary>
        private string _dataDirectoryPath;

        /// <summary>
        /// Synchronization root for protocol creation and disposal.
        /// </summary>
        private readonly object _lock = new object();
        /// <summary>
        /// Synchronization root for accessing and mutating <see cref="_sessions"/>.
        /// </summary>
        private readonly object _sessionsLock = new object();

        /// <summary>
        /// Current protocol instance that handles low-level ECR communication. May be <c>null</c> if not initialized.
        /// </summary>
        private ECRProtocol _ecrProtocol;

        /// <summary>
        /// Lazily initialized accessor for the active <see cref="ECRProtocol"/>; recreates and re-subscribes if disposed.
        /// </summary>
        private ECRProtocol ECRProtocol
        {
            get
            {
                lock (_lock)
                {
                    if (_ecrProtocol == null
                        || _ecrProtocol.IsDisposed)
                    {
                        _ecrProtocol = null;
                        try
                        {
                            _ecrProtocol = new ECRProtocol(PortName, traceSerialBytes: TraceSerialBytes);
                            AddEcrProtocolEvents(_ecrProtocol);
                        }
                        catch (Exception ex)
                        {
                            OnProtocolError(new ExceptionEventArgs(ex));
                        }

                    }
                    return _ecrProtocol;
                }
            }
        }

        /// <summary>
        /// Long date/time format pattern used by the underlying protocol; defaults to "G" if unavailable.
        /// </summary>
        public string DateTimeFormatLong => _ecrProtocol?.DateTimeFormatLong ?? "G";

        /// <summary>
        /// Short date/time format pattern used by the underlying protocol; defaults to "g" if unavailable.
        /// </summary>
        public string DateTimeFormatShort => _ecrProtocol?.DateTimeFormatShort ?? "g";

        /// <inheritdoc />
        public bool IsDisposed { get; private set; } = false;

        public event EventHandler WakeupECRReceived;
        public event EventHandler<TerminalCommandAcceptedEventArgs> TerminalCommandAccepted;
        public event EventHandler<DeviceControlResultEventArgs> DeviceControlResultReceived;
        public event EventHandler<TransactionStatusEventArgs> TransactionStatusChanged;
        public event EventHandler<TransactionEventArgs> TransactionInitialized;
        public event EventHandler<TransactionStatusEventArgs> TransactionTerminalAbortReceived;
        public event EventHandler<TransactionResultEventArgs> TransactionResultReceived;
        public event EventHandler<TransactionResultEventArgs> PurchaseCreated;
        public event EventHandler<TransactionResultEventArgs> ReversalCreated;
        public event EventHandler<TransactionResultEventArgs> RefundCreated;
        public event EventHandler<TransactionResultEventArgs> TransactionRetrieved;
        public event EventHandler<CustomerRequestResultEventArgs> BonusResultReceived;
        public event EventHandler<AbortTransactionResultEventArgs> AbortTransactionResultReceived;
        public event EventHandler<ExceptionEventArgs> TerminalError;

        /// <summary>
        /// Serial port name (e.g., "COM3") used to communicate with the terminal.
        /// </summary>
        public string PortName { get; }

        /// <summary>
        /// Gets a value indicating whether serial port traffic (raw bytes) is traced
        /// to diagnostic output for debugging purposes.
        /// </summary>
        /// 
        public bool TraceSerialBytes { get; }

        /// <summary>
        /// Gets the most recently created or active terminal session,
        /// regardless of its current state (running, succeeded, or failed).
        /// </summary>
        public TerminalSession LastSession => _sessions?.LastOrDefault();

        /// <summary>
        /// Initializes a new instance of the <see cref="ECRTerminalManager"/> class.
        /// Validates the serial port, initializes the session store, and optionally creates the data directory.
        /// </summary>
        /// <param name="portName">The serial port name (e.g., "COM3"). Must refer to an existing port.</param>
        /// <param name="userPromptHandler">Callback handler for managing manual prompts from the payment terminal.</param>
        /// <param name="maxSessions">The maximum number of session records to retain in memory. Default is 100.</param>
        /// <param name="dataDirectoryPath">Optional directory path for storing transaction files. Created if it does not exist.</param>
        /// <param name="traceSerialBytes">If <c>true</c>, enables tracing of raw serial communication bytes for diagnostic purposes. Default is false.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required parameter is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown when the specified serial port name is invalid or does not exist.</exception>
        public ECRTerminalManager(
            string portName,
            IUserPromptHandler userPromptHandler,
            int maxSessions = 100,
            string dataDirectoryPath = null,
            bool traceSerialBytes = false)
        {
            PortName = portName ?? throw new ArgumentNullException(nameof(portName));

            if (!SerialPort.GetPortNames().Any(o => o.Equals(portName, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException($"Invalid value '{portName}' for serial port.", nameof(portName));

            _maxSessions = maxSessions;
            _sessions = new LinkedList<TerminalSession>();
            _userPromptHandler = userPromptHandler ?? throw new ArgumentNullException(nameof(userPromptHandler));
            if (IsValidDirectoryPath(dataDirectoryPath, allowRelative: true))
            {
                _dataDirectoryPath = Path.GetFullPath(dataDirectoryPath);
                if (!Directory.Exists(_dataDirectoryPath))
                    Directory.CreateDirectory(_dataDirectoryPath);
            }
            else if (!string.IsNullOrEmpty(dataDirectoryPath))
            {
                Trace.WriteLine($"Invalid data directory parameter value {nameof(dataDirectoryPath)}='{dataDirectoryPath}'", GetType().FullName);
            }

            TraceSerialBytes = traceSerialBytes;
        }

        /// <summary>
        /// Returns the last session that is currently in the <see cref="SessionState.Running"/> state, if any.
        /// </summary>
        /// <returns>The running <see cref="TerminalSession"/> or <c>null</c>.</returns>
        private TerminalSession GetLastRunningSession()
        {
            lock (_sessionsLock)
                return _sessions.LastOrDefault(o => o.State == SessionState.Running);
        }

        /// <summary>
        /// Adds a session to the history, truncating the oldest if capacity is reached.
        /// </summary>
        /// <param name="session">The session to add.</param>
        private void AddSession(TerminalSession session)
        {
            lock (_sessionsLock)
            {
                if (_sessions.Count >= _maxSessions)
                    _sessions.RemoveFirst();
                _sessions.AddLast(session);
            }
        }

        /// <summary>
        /// Checks whether there is an active terminal session currently in progress.
        /// <para>
        /// If a running session is found, a <see cref="ECRTerminalSessionException"/> is raised through
        /// <see cref="OnProtocolError(object, ExceptionEventArgs)"/> to report the conflict.
        /// </para>
        /// </summary>
        /// <returns>
        /// <c>true</c> if a session is currently active; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// This method is typically used to guard against starting a new session while another
        /// transaction is still in progress.
        /// </remarks>

        private bool HasRunningSession()
        {
            TerminalSession session = GetLastRunningSession();

            if (session == null)
            {
                return false;
            }
            else
            {
                OnProtocolError
                (
                    new ExceptionEventArgs
                    (
                        new ECRTerminalSessionException
                        (
                            StringResources.GetCommonString(StringResources.MessageLastSessionRunning),
                            session.SessionId,
                            session.SessionType,
                            session.State,
                            session.TransactionId,
                            session.CreatedAt
                        )
                    )
                );
            }

            return true;
        }

        /// <summary>
        /// Disposes and detaches the current <see cref="ECRProtocol"/> instance, if any.
        /// </summary>
        protected void ReleaseEcrPort()
        {
            Trace.WriteLine($"{nameof(ReleaseEcrPort)}", GetType().FullName);

            try
            {
                if (_ecrProtocol != null)
                {
                    RemoveEcrProtocolEvents(_ecrProtocol);
                    _ecrProtocol.Dispose();
                    _ecrProtocol = null;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"{nameof(ReleaseEcrPort)}:\n{ex}", GetType().FullName);
            }
        }

        /// <summary>
        /// Releases managed/unmanaged resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            Trace.WriteLine($"{nameof(Dispose)}:{disposing}", GetType().FullName);
            if (!IsDisposed)
            {
                if (disposing)
                {
                    ReleaseEcrPort();
                }

                IsDisposed = true;
                _ecrProtocol = null;
            }
        }

        /// <summary>
        /// Disposes the manager and suppresses finalization.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Subscribes this manager to all protocol events from the provided <paramref name="protocol"/>.
        /// </summary>
        /// <param name="protocol">The protocol instance to attach to.</param>
        private void AddEcrProtocolEvents(ECRProtocol protocol)
        {
            protocol.WakeupECRReceived += OnWakeupECRReceived;
            protocol.TerminalCommandAccepted += OnTerminalCommandAccepted;
            protocol.DeviceControlResult += OnDeviceControlResult;
            protocol.TransactionStatusChanged += OnTransactionStatusChanged;
            protocol.TransactionTerminalAbort += OnTransactionTerminalAbort;
            protocol.TransactionResultEx += OnTransactionResultEx;
            protocol.UserPromptRequired += OnUserPromptRequired;
            protocol.CustomerRequestResult += OnCustomerRequestResult;
            protocol.AbortTransactionResult += OnAbortTransactionResult;
            protocol.ProtocolError += OnProtocolError;
        }

        /// <summary>
        /// Unsubscribes this manager from all protocol events on the provided <paramref name="protocol"/>.
        /// </summary>
        /// <param name="protocol">The protocol instance to detach from.</param>
        private void RemoveEcrProtocolEvents(ECRProtocol protocol)
        {
            protocol.WakeupECRReceived -= OnWakeupECRReceived;
            protocol.TerminalCommandAccepted -= OnTerminalCommandAccepted;
            protocol.DeviceControlResult -= OnDeviceControlResult;
            protocol.TransactionStatusChanged -= OnTransactionStatusChanged;
            protocol.TransactionTerminalAbort -= OnTransactionTerminalAbort;
            protocol.TransactionResultEx -= OnTransactionResultEx;
            protocol.UserPromptRequired -= OnUserPromptRequired;
            protocol.CustomerRequestResult -= OnCustomerRequestResult;
            protocol.AbortTransactionResult -= OnAbortTransactionResult;
            protocol.ProtocolError -= OnProtocolError;
        }

        /// <summary>
        /// Masks a string for logging by replacing characters up to <paramref name="maxLength"/> with the given mask character.
        /// </summary>
        /// <param name="input">The value to mask.</param>
        /// <param name="value">The mask character, default is *</param>
        /// <param name="maxLength">Maximum number of characters to mask, default is 10</param>
        /// <returns>The masked string.</returns>
        private string MaskString(string input, char value = '*', int maxLength = 10)
        {
            int len = input?.Trim().Length ?? 0;
            return new string(value, Math.Min(len, maxLength));
        }

        /// <summary>
        /// Executes an action on a background task after an optional short delay, swallowing/logging exceptions.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <param name="delayMilliseconds">Delay before execution in milliseconds.</param>
        private void RunAction(Action action, int delayMilliseconds = 20)
        {
            Task.Run(async () =>
            {
                try { await Task.Delay(delayMilliseconds).ConfigureAwait(false); action(); }
                catch (Exception ex) { Trace.WriteLine($"{nameof(RunAction)}:\n{ex}", GetType().FullName); }
            });
        }

        /// <summary>
        /// Sets the last session state to <see cref="SessionState.Error"/> and
        /// records the specified exception details, if a session exists.
        /// </summary>
        /// <param name="exception">The exception that caused the last session to fail.</param>
        protected void SetLastSessionError(Exception exception)
        {
            LastSession?.MarkError(exception);
        }

        /// <summary>
        /// Sends a terminal handshake to verify connectivity.
        /// </summary>
        public void TestTerminal()
        {
            Trace.WriteLine($"{nameof(TestTerminal)}", GetType().FullName);
            ECRProtocol?.SendHandshake();
        }

        /// <summary>
        /// Requests the terminal to abort the current transaction.
        /// </summary>
        public void AbortTransaction()
        {
            Trace.WriteLine($"{nameof(AbortTransaction)}", GetType().FullName);
            ECRProtocol?.SendTransactionAbort();
        }

        /// <summary>
        /// Starts a new payment transaction for the specified amount.
        /// Returns immediately after sending the request; the transaction result
        /// is reported asynchronously through session events.
        /// </summary>
        /// <param name="amount">The amount to charge (must be positive).</param>
        /// <param name="isBonusHandled">
        /// If <c>true</c>, indicates that bonus handling was already performed by the ECR.
        /// </param>
        /// <param name="sessionId">Optional external correlation identifier.</param>
        public void RunPayment(decimal amount, bool isBonusHandled, string sessionId = null)
        {
            if (HasRunningSession())
                return;
            if (amount <= 0)
            {
                OnProtocolError(this, new ExceptionEventArgs(
                    new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.")));
                return;
            }

            PaymentSession session = new PaymentSession(amount, isBonusHandled, sessionId);
            AddSession(session);
            ECRProtocol?.SendRunPayment(amount, session.TransactionId, isBonusHandled);
        }

        /// <summary>
        /// Repeats the last payment session using its original parameters
        /// (amount, transaction identifier, and bonus flag).
        /// <para>
        /// If no previous payment session exists, a protocol error is raised through
        /// <see cref="OnProtocolError(object, ExceptionEventArgs)"/>.
        /// </para>
        /// </summary>
        private void RerunLastPayment()
        {
            Trace.WriteLine($"{nameof(RerunLastPayment)}", GetType().FullName);

            PaymentSession session = GetLastRunningSession() as PaymentSession;

            if (session == null)
            {
                OnProtocolError
                (
                    this,
                    new ExceptionEventArgs
                    (
                        new ECRTerminalException(StringResources.GetCommonString(StringResources.MessageNoPreviousPaymentSessionToRetry))
                    )
                );
            }
            else
            {
                ECRProtocol?.SendRunPayment(session.Amount, session.TransactionId, session.IsBonusHandled);
            }
        }

        /// <summary>
        /// Initiates a refund transaction for the specified amount.
        /// <para>
        /// If another session is already active, the operation is ignored and a protocol error
        /// is raised through <see cref="OnProtocolError(object, ExceptionEventArgs)"/>.
        /// </para>
        /// This method sends the refund request to the payment terminal and returns immediately;
        /// the result is reported asynchronously through session and protocol events.
        /// </summary>
        /// <param name="amount">The refund amount to be processed.</param>
        /// <param name="sessionId">Optional external session identifier for correlation.</param>

        public void Refund(decimal amount, string sessionId = null)
        {
            if (HasRunningSession())
                return;
            RefundSession session = new RefundSession(amount, sessionId);
            AddSession(session);
            ECRProtocol?.SendRefund(amount);
        }

        //// <summary>
        /// Initiates a reversal transaction to cancel a previously authorized payment.
        /// <para>
        /// The reversal is identified by the original transaction ID and timestamp provided.
        /// If another session is currently active, the operation is ignored and a protocol error
        /// is raised through <see cref="OnProtocolError(object, ExceptionEventArgs)"/>.
        /// </para>
        /// This method sends the reversal request to the payment terminal and returns immediately;
        /// the outcome is reported asynchronously via session and protocol events.
        /// </summary>
        /// <param name="transactionId">The identifier of the original transaction to reverse.</param>
        /// <param name="timestamp">The timestamp of the original transaction.</param>
        /// <param name="sessionId">Optional external session identifier for correlation.</param>

        public void Reversal(string transactionId, DateTime timestamp, string sessionId = null)
        {
            if (HasRunningSession())
                return;
            ReversalSession session = new ReversalSession(transactionId, timestamp, sessionId);
            AddSession(session);
            ECRProtocol?.SendReversal(transactionId, timestamp);
        }

        /// <summary>
        /// Sends an accept/decline decision for a paused transaction.
        /// </summary>
        /// <param name="transactionId">The transaction id requiring confirmation.</param>
        /// <param name="accept">If <c>true</c>, accepts; otherwise rejects.</param>
        private void AcceptOrDeclineTransaction(string transactionId, bool accept)
        {
            ECRProtocol?.SendTransactionAccept(transactionId, accept);
        }

        /// <summary>
        /// Enables bonus card mode on the terminal.
        /// </summary>
        /// <param name="autoReply">If <c>true</c>, terminal auto-replies to certain prompts.</param>
        public void EnableBonusCardMode(bool autoReply)
        {
            ECRProtocol?.SendCustomerCardModeStart(autoReply);
        }

        /// <summary>
        /// Disables bonus card mode on the terminal.
        /// </summary>
        public void DisableBonusCardMode()
        {
            ECRProtocol?.SendCustomerCardModeStop();
        }

        /// <summary>
        /// Requests customer/bonus card information; optionally stops active bonus mode.
        /// </summary>
        /// <param name="stopActive">If <c>true</c>, stops active mode after retrieval.</param>
        public void RequestBonusCardInfo(bool stopActive)
        {
            ECRProtocol?.SendCustomerRequest(stopActive);
        }

        /// <summary>
        /// Requests the terminal to publish its current status.
        /// </summary>
        public void RequestTerminalStatus()
        {
            ECRProtocol?.SendRequestStatus();
        }

        /// <summary>
        /// Requests terminal firmware/version information.
        /// </summary>
        public void RequestTerminalVersion()
        {
            ECRProtocol?.SendTerminalVersionRequest();
        }

        /// <summary>
        /// Displays text on the terminal customer display.
        /// </summary>
        /// <param name="line1">Top line text.</param>
        /// <param name="line2">Bottom line text.</param>
        /// <param name="bigFont">If <c>true</c>, uses larger font.</param>
        public void DisplayText(string line1, string line2, bool bigFont)
        {
            ECRProtocol?.SendDisplayText(line1, line2, bigFont);
        }

        /// <summary>
        /// Clears the terminal display.
        /// </summary>
        public void ClearDisplayText()
        {
            ECRProtocol?.SendClearDisplay();
        }

        /// <summary>
        /// Enables auxiliary accept mode on the terminal.
        /// </summary>
        public void EnableAuxiliaryMode()
        {
            ECRProtocol?.SendAuxiliaryAcceptMode(true);
        }

        /// <summary>
        /// Disables auxiliary accept mode on the terminal.
        /// </summary>
        public void DisableAuxiliaryMode()
        {
            ECRProtocol?.SendAuxiliaryAcceptMode(false);
        }

        /// <summary>
        /// Accepts a transaction currently awaiting ECR confirmation.
        /// </summary>
        /// <param name="transactionId">The transaction id to accept.</param>
        public void AcceptTransaction(string transactionId)
        {
            ECRProtocol?.SendTransactionAccept(transactionId, true);
        }

        /// <summary>
        /// Rejects a transaction currently awaiting ECR confirmation.
        /// </summary>
        /// <param name="transactionId">The transaction id to reject.</param>
        public void RejectTransaction(string transactionId)
        {
            ECRProtocol?.SendTransactionAccept(transactionId, false);
        }

        /// <summary>
        /// Retrieves a specific transaction result from the terminal.
        /// </summary>
        /// <param name="transactionId">Transaction id or <see cref="ECRProtocol.EmptyTransactionId"/>.</param>
        /// <param name="timestamp">Original timestamp.</param>
        public void RetrieveTransaction(string transactionId, DateTime timestamp)
        {
            if (HasRunningSession())
                return;
            RetrieveSession session = new RetrieveSession(transactionId, timestamp);
            AddSession(session);
            ECRProtocol?.SendRetrieveTransaction(transactionId, timestamp);
        }

        /// <summary>
        /// Retrieves the most recent transaction result from the terminal.
        /// </summary>
        public void RetrieveLastTransaction()
        {
            RetrieveTransaction(ECRProtocol.EmptyTransactionId, default(DateTime));
        }

        /// <summary>
        /// Requests the latest TCS message from the terminal.
        /// </summary>
        public void RetrieveTCSMessage()
        {
            ECRProtocol?.SendTCSMessageRequest();
        }

        /// <summary>
        /// Closes the serial port and disposes protocol resources.
        /// </summary>
        public void Disconnect()
        {
            ReleaseEcrPort();
        }

        /// <summary>
        /// Raises the <see cref="TransactionInitialized"/> event for the supplied session.
        /// </summary>
        /// <param name="session">The session for which initialization is reported.</param>
        private void OnTransactionInitialized(TerminalSession session)
        {
            EventHandler<TransactionEventArgs> h = TransactionInitialized;
            h?.Invoke(this, new TransactionEventArgs(session.TransactionId, session.CreatedAt));
        }

        /// <summary>
        /// Handles transaction initialization status by updating the current payment session and raising events.
        /// </summary>
        /// <param name="e">The status payload.</param>
        private void HandleTransactionInitialized(TransactionStatusEventArgs e)
        {
            Debug.WriteLine($"{nameof(HandleTransactionInitialized)}:{e}", GetType().FullName);
            PaymentSession session = GetLastRunningSession() as PaymentSession;
            if (session != null)
            {
                session.UpdateTransactionId(e.Info?.Trim());
                OnTransactionInitialized(session);
            }
        }

        /// <summary>
        /// Handles a bonus card detection event by updating the current payment session
        /// and restarting the payment flow with the detected bonus information.
        /// </summary>
        /// <param name="e">Event data containing bonus card details and status information.</param>
        private void HandleBonusCardFound(TransactionStatusEventArgs e)
        {
            Trace.WriteLine($"{nameof(HandleBonusCardFound)}:{e}", GetType().FullName);

            string customerNumber = e.Info?.Trim() ?? "";
            PaymentSession session = GetLastRunningSession() as PaymentSession;

            if (session != null)
            {
                session.UpdateBonusInfo(customerNumber, "", e.StatusResultCode, e.StatusResultCodeMessage);
                session.MarkBonusDetectedAndHalted();
                RunPayment(session.Amount, true);
            }
        }

        /// <summary>
        /// Provides an extension point for derived classes to perform additional
        /// processing after the framework has completed the default handling of a
        /// bonus-card-only event.
        /// <para>
        /// The base implementation performs the following actions:
        /// </para>
        /// <list type="bullet">
        /// <item>
        /// <description>Schedules the disabling of bonus card mode.</description>
        /// </item>
        /// <item>
        /// <description>Constructs and raises a <see cref="CustomerRequestResultEventArgs"/>
        /// notification containing the parsed customer number and status message.</description>
        /// </item>
        /// <item>
        /// <description>Raises a terminal-abort notification for the current transaction.</description>
        /// </item>
        /// </list>
        /// <para>
        /// Override this method when additional application-specific behavior is needed,
        /// such as logging, analytics, UI updates, workflow transitions, or integration
        /// with external systems. When overriding, call the base implementation unless
        /// you intend to completely replace the built-in behavior.
        /// </para>
        /// </summary>
        /// <param name="e">
        /// The event data associated with the bonus card operation, including the raw
        /// customer identifier and status information reported by the payment terminal.
        /// </param>
        protected virtual void HandleBonusCardOnlyAfter(TransactionStatusEventArgs e)
        {
            RunAction(DisableBonusCardMode, 500);
            string customerNumber = e.Info?.Trim() ?? "";
            string statusMessage = e.StatusResultCodeMessage?.Trim() ?? "" + $"({e.StatusResultCode})";
            OnCustomerRequestResultReceived(new CustomerRequestResultEventArgs(customerNumber, "", statusMessage));
            OnTransactionTerminalAbortReceived(e);
        }

        /// <summary>
        /// Performs the default handling of a bonus-card-only event. This includes
        /// disabling bonus card mode, updating the active payment session (if one is
        /// running), and invoking the post-processing step
        /// <see cref="HandleBonusCardOnlyAfter(TransactionStatusEventArgs)"/>.
        /// </summary>
        /// <param name="e">
        /// The event data containing bonus card information and status details.
        /// </param>
        private void HandleBonusCardOnly(TransactionStatusEventArgs e)
        {
            Trace.WriteLine($"{nameof(HandleBonusCardOnly)}:{e}", GetType().FullName);

            string customerNumber = e.Info?.Trim() ?? "";
            string statusMessage = e.StatusResultCodeMessage?.Trim() ?? "" + $"({e.StatusResultCode})";
            PaymentSession session = GetLastRunningSession() as PaymentSession;

            if (session != null)
            {
                session.UpdateBonusInfo(customerNumber, "", e.StatusResultCode, e.StatusResultCodeMessage);
                session.MarkAborted();
            }

            HandleBonusCardOnlyAfter(e);
        }

        /// <summary>
        /// Forwards terminal abort status to listeners.
        /// </summary>
        /// <param name="e">Transaction status with abort details.</param>
        private void OnTransactionTerminalAbortReceived(TransactionStatusEventArgs e)
        {
            EventHandler<TransactionStatusEventArgs> h = TransactionTerminalAbortReceived;
            h?.Invoke(this, e);
        }

        /// <summary>
        /// Marks the current session as terminal-aborted and raises the corresponding event.
        /// </summary>
        /// <param name="e">Transaction status payload.</param>
        private void HandleTransactionTerminalAbortReceived(TransactionStatusEventArgs e)
        {
            Trace.WriteLine($"{nameof(HandleTransactionTerminalAbortReceived)}", GetType().FullName);
            TerminalSession session = GetLastRunningSession();

            if (session != null)
            {
                session.MarkTerminalAborted();
                Trace.WriteLine($"Session {session.SessionId} marked as TerminalAborted due to ResultCode {e.StatusResultCode}.", GetType().FullName);
            }

            OnTransactionTerminalAbortReceived(e);
        }

        /// <summary>
        /// Performs internal handling of protocol-level errors detected during
        /// terminal communication.
        /// <para>
        /// Implementations can override this method to provide custom recovery logic,
        /// logging, or reconnection strategies.  
        /// If the underlying exception is an <see cref="IOException"/>, the default
        /// behavior is to close the current connection by calling <see cref="Disconnect"/>,
        /// allowing future operations to re-establish communication.
        /// After internal handling, the error is propagated to subscribers via
        /// <see cref="OnProtocolError(ExceptionEventArgs)"/>.
        /// </para>
        /// </summary>
        /// <param name="e">The exception event arguments describing the protocol error.</param>
        protected virtual void HandleProtocolError(ExceptionEventArgs e)
        {
        }

        /// <summary>
        /// Protocol event handler for terminal abort notifications.
        /// </summary>
        /// <param name="sender">The protocol source.</param>
        /// <param name="e">Transaction status payload.</param>
        private void OnTransactionTerminalAbort(object sender, TransactionStatusEventArgs e)
        {
            Trace.WriteLine($"{nameof(OnTransactionTerminalAbort)}:{e}", GetType().FullName);
            RunAction(() => HandleTransactionTerminalAbortReceived(e));
        }

        /// <summary>
        /// Protocol event handler for wakeup notifications (handshake from terminal).
        /// </summary>
        /// <param name="sender">The protocol source.</param>
        /// <param name="e">Unused.</param>
        private void OnWakeupECRReceived(object sender, EventArgs e)
        {
            Trace.WriteLine($"{nameof(OnWakeupECRReceived)}", GetType().FullName);
            EventHandler h = WakeupECRReceived;
            h?.Invoke(this, e);
        }

        /// <summary>
        /// Protocol event handler invoked when a terminal command has been
        /// acknowledged by the device (ACK or STX received).
        /// <para>
        /// Raises the <see cref="TerminalCommandAccepted"/> event to notify subscribers
        /// that the terminal has accepted or begun processing a specific command,
        /// identified by <see cref="TerminalCommandAcceptedEventArgs.CommandId"/>.
        /// </para>
        /// </summary>
        /// <param name="sender">The protocol source reporting the acknowledgment.</param>
        /// <param name="e">The event arguments containing the accepted command details.</param>

        private void OnTerminalCommandAccepted(object sender, TerminalCommandAcceptedEventArgs e)
        {
            Trace.WriteLine($"{nameof(OnTerminalCommandAccepted)}:{e}", GetType().FullName);
            EventHandler<TerminalCommandAcceptedEventArgs> h = TerminalCommandAccepted;
            h?.Invoke(this, e);
        }

        /// <summary>
        /// Protocol event handler for device control results (e.g., display/auxiliary responses).
        /// </summary>
        /// <param name="sender">The protocol source.</param>
        /// <param name="e">Device control result payload.</param>
        private void OnDeviceControlResult(object sender, DeviceControlResultEventArgs e)
        {
            Trace.WriteLine($"{nameof(OnDeviceControlResult)}", GetType().FullName);
            EventHandler<DeviceControlResultEventArgs> h = DeviceControlResultReceived;
            h?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the <see cref="TransactionStatusChanged"/> event.
        /// </summary>
        /// <param name="e">Transaction status payload.</param>
        private void OnTransactionStatusChanged(TransactionStatusEventArgs e)
        {
            EventHandler<TransactionStatusEventArgs> h = TransactionStatusChanged;
            h?.Invoke(this, e);
        }

        /// <summary>
        /// Protocol event handler for transaction status changes; routes to business logic and event subscribers.
        /// </summary>
        /// <param name="sender">The protocol source.</param>
        /// <param name="e">Transaction status payload.</param>
        private void OnTransactionStatusChanged(object sender, TransactionStatusEventArgs e)
        {
            Trace.WriteLine($"{nameof(OnTransactionStatusChanged)}:{e}", GetType().FullName);
            try
            {
                LastSession?.UpdateTransactionStatus(e);

                OnTransactionStatusChanged(e);

                // --- React to specific TransactionInfo Codes ---
                switch (e.StatusResultCode)
                {
                    case "0000": // Status is OK, no errors
                    case "0001": // Bonus card detected (status OK)
                    case "0002": // Card read failed, fallback continues
                    case "0003": // Blacklist missing or incorrect (inform operator)
                    case "0004": // CAPK missing or incorrect (inform operator)
                    case "0005": // Date of birth included (YYMMDD in ExtraInfo)
                    case "0014": // Using magstripe of chip card before chip
                    case "0015": // Incorrect PIN given, retry possible
                    case "0016": // Authorization checksum error, retry needed
                    case "0017": // PIN bypassed
                    case "0018": // PIN blocked
                    case "0019": // Authorization cancel failed, cashier must call
                    case "0020": // Surcharge amount and group in ExtraInfo
                    case "0021": // NFC processing failed, fallback to chip
                    case "0022": // Offline transaction queue, ExtraInfo = count
                    case "0023": // Unknown NFC card, request new card
                    case "0024": // Amount over NFC card limit
                        break;

                    // --- 1000–1999: Transaction must stop ---
                    case "1001": // Invalid or unknown card
                    case "1002": // Card read failed
                    case "1003": // Card removed
                    case "1004": // Stop key pressed
                    case "1005": // Invalid card
                    case "1006": // Card expired
                    case "1007": // Card blacklisted (warning in ExtraInfo)
                    case "1008": // Original transaction not found
                    case "1009": // Reversal/refund not allowed
                    case "1010": // Message syntax error (e.g., zero amount)
                    case "1012": // Terminal config error
                    case "1013": // Timeout (application selection or PIN)
                    case "1014": // Magstripe used instead of chip
                    case "1015": // Incorrect PIN, last attempt
                    case "1016": // App not allowed
                    case "1017": // PIN bypass not allowed
                    case "1018": // Authorization code error, abort
                    case "1019": // Below application min amount
                    case "1020": // Above application max amount
                    case "1021": // Service forbidden by app (e.g., cashback)
                    case "1022": // Transaction auto-cancelled (missing ACK)
                    case "1024": // Card can't be processed, manual fallback
                        break;

                    // --- 1100+ ---
                    case "1100": // No connection to Point
                    case "1102": // Preauthorization not found
                    case "1103": // Invalid new preauth expiration date
                        break;

                    // --- 2000–2999: Transaction paused, needs ECR confirmation ---
                    case "2001": // Bonus card found, continue with BonusHandled = 1
                        RunAction(() => HandleBonusCardFound(e));
                        break;
                    case "2002": // Bonus card only (no payment), abort
                        RunAction(() => HandleBonusCardOnly(e));
                        break;
                    case "2003": // Manual authorization required
                    case "2004": // PIN bypass needs ECR confirmation
                    case "2005": // ID check required (manual confirmation)
                    case "2006": // Chip read failed, confirm fallback to magstripe
                    case "2007": // Swedbank use: enter 4 digits
                    case "2008": // Reserved
                    case "2012": // PIN blocked, retry with verified customer ID
                    case "2022": // Waiting for AcceptTransaction
                        break;

                    // --- 9000–9999: Authorization declined ---
                    case "91Z3": // Declined before online
                    case "91Z1": // Card app expired
                    case "9400": // Card declined after successful authorization
                        break;
                    default:
                        break;
                }

                // --- React to specific Phases ---
                switch (e.StatusPhase)
                {
                    case "A": // Transaction initialized
                        HandleTransactionInitialized(e);
                        break;
                    case "0": // Waiting for card
                    case "1": // Chip card inserted
                    case "2": // Waiting for magstripe fallback
                    case "3": // Magstripe card read
                    case "4": // Manual card number entry
                    case "5": // Language selection
                    case "6": // Application selection
                    case "7": // Cardholder verification (e.g., PIN)
                    case "8": // Authorization in progress
                    case "9": // Contactless card read
                    case "B": // Terminal reports blacklist missing
                    case "C": // Terminal reports CAPK missing
                    case "#": // Preauthorization ID provided
                    case "$": // Waiting for AcceptTransaction
                    case "Q": // ECR confirmation required (fallback, ID check, etc.)
                    case "R": // Transaction complete, waiting for card removal
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"{nameof(OnTransactionStatusChanged)}:\n{ex}");
            }
        }

        /// <summary>
        /// Shows a user prompt dialog requesting input and logs the outcome.
        /// </summary>
        /// <param name="e">The prompt descriptor from the terminal.</param>
        /// <param name="userInput">On accept, the user-provided text.</param>
        /// <returns><c>true</c> if the user accepted; otherwise, <c>false</c>.</returns>
        protected virtual bool ShowUserPromptDialog(UserPromptEventArgs e, out string userInput)
        {
            bool result = _userPromptHandler.ShowUserPromptDialog(e.PromptMessage, out userInput);
            Trace.WriteLine($"{nameof(ShowUserPromptDialog)}:{e} {result} {MaskString(userInput)}", GetType().FullName);
            return result;
        }

        /// <summary>
        /// Shows a user prompt dialog without input and logs the outcome.
        /// </summary>
        /// <param name="e">The prompt descriptor from the terminal.</param>
        /// <returns><c>true</c> if the user accepted; otherwise, <c>false</c>.</returns>
        protected virtual bool ShowUserPromptDialog(UserPromptEventArgs e)
        {
            bool result = _userPromptHandler.ShowUserPromptDialog(e.PromptMessage);
            Trace.WriteLine($"{nameof(ShowUserPromptDialog)}:{e} {result}", GetType().FullName);
            return result;
        }

        /// <summary>
        /// Sends manual authorization data to the terminal (e.g., voice/phone auth code).
        /// </summary>
        /// <param name="amount">Transaction amount.</param>
        /// <param name="transactionId">Current transaction id.</param>
        /// <param name="bonusHandled">Whether bonus was already handled.</param>
        /// <param name="input">The manual authorization code.</param>
        private void SendManualAuthorization(decimal amount, string transactionId, bool bonusHandled, string input)
        {
            Trace.WriteLine($"{nameof(SendManualAuthorization)}:{MaskString(input)}", GetType().FullName);
            ECRProtocol?.SendManualAuthorization(0, "", false, input);
        }

        /// <summary>
        /// Determines whether manual authorization flows (e.g. result codes 2003, 2007)
        /// are permitted in this application.
        /// </summary>
        /// <param name="resultCode">
        /// The result code received from the terminal that triggered the manual prompt.
        /// </param>
        /// <returns>
        /// <c>true</c> to allow the cashier to enter an authorization code or last 4 digits
        /// and continue the transaction; <c>false</c> to reject the request and abort the
        /// transaction immediately.
        /// </returns>
        /// <remarks>
        /// By default this method returns <c>false</c> for security reasons, since manual
        /// authorization can allow sales without online host verification. Override in a
        /// derived class to enable this flow under controlled conditions (e.g. with
        /// manager approval).
        /// </remarks>
        protected virtual bool ShouldAllowManualAuthorization(string resultCode)
        {
            return false;
        }

        /// <summary>
        /// Determines whether a confirmation-only user prompt should be shown to the cashier
        /// (e.g. PIN bypass, ID check, fallback confirmation, or AcceptTransaction).
        /// </summary>
        /// <param name="resultCode">
        /// The result code received from the terminal that triggered the prompt.
        /// </param>
        /// <returns>
        /// <c>true</c> to display the prompt and handle the operator’s acceptance/rejection;
        /// <c>false</c> to automatically abort the transaction without user interaction.
        /// </returns>
        /// <remarks>
        /// By default this method returns <c>true</c> to allow normal operation as defined
        /// in the ECR specification. Override in a derived class to enforce stricter policies,
        /// such as automatically declining all bypass requests.
        /// </remarks>
        protected virtual bool ShouldAllowUserPrompt(string resultCode)
        {
            return true;
        }

        /// <summary>
        /// Optional post-processing logic that always runs after handling a user prompt.
        /// Can be overridden in derived classes.
        /// </summary>
        protected virtual void PostProcessUserPrompt(UserPromptEventArgs e)
        {
            // Default = no-op.
            // Override if you want to add logging, metrics, auditing, etc.
        }

        /// <summary>
        /// Handles protocol events that require cashier interaction.
        /// 
        /// <para>
        /// Depending on the result code, the prompt may require either:
        /// <list type="bullet">
        ///   <item><description>
        ///     Manual entry (e.g. authorization code for 2003, last 4 digits for 2007),
        ///     which is controlled by <see cref="ShouldAllowManualAuthorization(string)"/>.
        ///   </description></item>
        ///   <item><description>
        ///     Confirmation only (e.g. PIN bypass, ID check, chip fallback, AcceptTransaction),
        ///     which is controlled by <see cref="ShouldAllowUserPrompt(string)"/>.
        ///   </description></item>
        /// </list>
        /// </para>
        /// 
        /// <para>
        /// If a prompt is not allowed by policy, the transaction is aborted automatically.
        /// By default, manual entry is disallowed and confirmation prompts are allowed.
        /// </para>
        /// </summary>
        /// <param name="sender">The protocol source that raised the event.</param>
        /// <param name="e">The event arguments describing the user prompt request.</param>
        private void OnUserPromptRequired(object sender, UserPromptEventArgs e)
        {
            Trace.WriteLine($"{nameof(OnUserPromptRequired)}:{e}", GetType().FullName);

            try
            {
                bool accepted = false;
                string input = null;

                switch (e.ResultCode)
                {
                    case "2003":
                    case "2007":
                        if (ShouldAllowManualAuthorization(e.ResultCode))
                        {
                            accepted = ShowUserPromptDialog(e, out input);
                            string entered = (input ?? string.Empty).Trim();
                            string pattern = e.ResultCode == "2003" ? @"^\d{4,6}$" : @"^\d{4}$";

                            if (accepted && ECRProtocol.ValidateAuthorizationCode(entered, pattern))
                            {
                                var session = GetLastRunningSession() as PaymentSession;
                                if (session != null)
                                {
                                    if (e.ResultCode == "2003")
                                    {
                                        session.UpdateManualAuthorizationCode(entered);
                                        RunAction(() => SendManualAuthorization(session.Amount, session.TransactionId, session.IsBonusHandled, entered));
                                    }
                                    else
                                    {
                                        RunAction(RerunLastPayment);
                                    }
                                }
                                else
                                {
                                    RunAction(AbortTransaction);
                                }
                            }
                            else
                            {
                                RunAction(AbortTransaction);
                            }
                        }
                        else
                        {
                            RunAction(AbortTransaction);
                        }
                        break;

                    case "2004":
                    case "2005":
                    case "2006":
                    case "2012":
                    case "2022":
                    case ECRProtocol.RetryTransactionCode:
                        if (ShouldAllowUserPrompt(e.ResultCode))
                        {
                            accepted = ShowUserPromptDialog(e);

                            if (e.ResultCode == "2022")
                            {
                                string tx = (e.Info ?? string.Empty).Trim();
                                if (tx.Length != 5)
                                {
                                    var session = GetLastRunningSession() as PaymentSession;
                                    tx = session?.TransactionId ?? "00000";
                                }

                                RunAction(() => AcceptOrDeclineTransaction(tx, accepted));
                            }
                            else
                            {
                                if (accepted)
                                    RunAction(RerunLastPayment);
                                else
                                    RunAction(AbortTransaction);
                            }
                        }
                        else
                        {
                            RunAction(AbortTransaction);
                        }
                        break;

                    default:
                        RunAction(AbortTransaction);
                        break;
                }

                // 🔽🔽 Post-processing hook — always runs after the switch 🔽🔽
                PostProcessUserPrompt(e);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"{nameof(OnUserPromptRequired)}:\n{ex}");
            }
        }

        /// <summary>
        /// Raises the <see cref="TransactionResultReceived"/> event with the supplied result.
        /// </summary>
        /// <param name="transactionResult">The resulting transaction data.</param>
        private void OnTransactionResultReceived(TransactionResultEventArgs transactionResult)
        {
            EventHandler<TransactionResultEventArgs> h = TransactionResultReceived;
            h?.Invoke(this, transactionResult);
        }

        /// <summary>
        /// Raises the <see cref="PurchaseCreated"/> event with the supplied result.
        /// </summary>
        /// <param name="transactionResult">The resulting transaction data.</param>
        private void OnPurchaseCreated(TransactionResultEventArgs transactionResult)
        {
            EventHandler<TransactionResultEventArgs> h = PurchaseCreated;
            h?.Invoke(this, transactionResult);
        }

        /// <summary>
        /// Raises the <see cref="ReversalCreated"/> event with the supplied result.
        /// </summary>
        /// <param name="transactionResult">The resulting transaction data.</param>
        private void OnReversalCreated(TransactionResultEventArgs transactionResult)
        {
            EventHandler<TransactionResultEventArgs> h = ReversalCreated;
            h?.Invoke(this, transactionResult);
        }

        /// <summary>
        /// Raises the <see cref="RefundCreated"/> event with the supplied result.
        /// </summary>
        /// <param name="transactionResult">The resulting transaction data.</param>
        private void OnRefundCreated(TransactionResultEventArgs transactionResult)
        {
            EventHandler<TransactionResultEventArgs> h = RefundCreated;
            h?.Invoke(this, transactionResult);
        }

        /// <summary>
        /// Raises the <see cref="TransactionRetrieved"/> event with the supplied result.
        /// </summary>
        /// <param name="transactionResult">The resulting transaction data.</param>
        private void OnTransactionRetrieved(TransactionResultEventArgs transactionResult)
        {
            EventHandler<TransactionResultEventArgs> h = TransactionRetrieved;
            h?.Invoke(this, transactionResult);
        }

        /// <summary>
        /// Protocol event handler that finalizes a transaction, enriches it with bonus info if applicable,
        /// persists to disk when configured, and raises the public result event.
        /// </summary>
        /// <param name="sender">The protocol source.</param>
        /// <param name="e">Extended transaction result payload.</param>
        private void OnTransactionResultEx(object sender, TransactionResultExEventArgs e)
        {
            Trace.WriteLine($"{nameof(OnTransactionResultEx)}:{e}", GetType().FullName);

            CustomerRequestResultEventArgs bonusInfo = null;
            TransactionResultEventArgs transactionResult = null;
            SessionType? sessionType = null;
            string sessionId = null;

            try
            {
                lock (_sessionsLock)
                {
                    LinkedListNode<TerminalSession> node = _sessions.Last;
                    TerminalSession session = node?.Value;

                    if (session != null
                        && session.State == SessionState.Running)
                    {
                        sessionId = session.SessionId;
                        sessionType = session.SessionType;
                        session.MarkCompleted(e);

                        //add customer request result
                        PaymentSession session1 = session as PaymentSession;
                        if (session1?.IsBonusHandled == true)
                        {
                            session1 = node.Previous?.Value as PaymentSession;
                            if (session1 != null
                                && session1.State == SessionState.BonusDetectedAndHalted)
                            {
                                sessionId = session1.SessionId;
                                bonusInfo = new CustomerRequestResultEventArgs(session1.BonusCustomerNumber, session1.BonusMemberClass, session1.BonusStatusText);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"{nameof(OnTransactionResultEx)}:\n{ex}");
            }

            string transactionFilePath = null;
            bool save = sessionType != null
                && sessionType != SessionType.Retrieve
                && IsValidDirectoryPath(_dataDirectoryPath);
            if (save)
                transactionFilePath = GetTransactionFilePath(_dataDirectoryPath, e);
            transactionResult = new TransactionResultEventArgs(e, bonusInfo, sessionId, transactionFilePath);
            if (save)
                SaveTransaction(transactionResult);

            //no ongoing session
            if (sessionType == null)
            {
                OnTransactionResultReceived(transactionResult);
            }
            else
            {
                switch (sessionType.Value)
                {
                    case SessionType.Payment:
                        OnPurchaseCreated(transactionResult);
                        break;
                    case SessionType.Refund:
                        OnRefundCreated(transactionResult);
                        break;
                    case SessionType.Reversal:
                        OnReversalCreated(transactionResult);
                        break;
                    case SessionType.Retrieve:
                        OnTransactionRetrieved(transactionResult);
                        break;
                }
            }
        }

        /// <summary>
        /// Raises the <see cref="BonusResultReceived"/> event with the supplied data.
        /// </summary>
        /// <param name="e">Bonus/customer request result.</param>
        private void OnCustomerRequestResultReceived(CustomerRequestResultEventArgs e)
        {
            EventHandler<CustomerRequestResultEventArgs> h = BonusResultReceived;
            h?.Invoke(this, e);
        }

        /// <summary>
        /// Protocol event handler for customer/bonus request results; updates session state and forwards the event.
        /// </summary>
        /// <param name="sender">The protocol source.</param>
        /// <param name="e">Customer request result payload.</param>
        private void OnCustomerRequestResult(object sender, CustomerRequestResultEventArgs e)
        {
            Trace.WriteLine($"{nameof(OnCustomerRequestResultReceived)}:{e}", GetType().FullName);
            try
            {
                PaymentSession session = GetLastRunningSession() as PaymentSession;

                //customer card only
                if (session?.IsActive == true)
                {
                    session.UpdateBonusInfo(e.CustomerNumber, e.MemberClass, "", e.StatusText);
                }
                else
                {
                    RunAction(DisableBonusCardMode, 100);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"{nameof(OnCustomerRequestResultReceived)}:\n{ex}");
            }

            OnCustomerRequestResultReceived(e);
        }

        /// <summary>
        /// Raises the <see cref="AbortTransactionResultReceived"/> event with the supplied data.
        /// </summary>
        /// <param name="e">Abort result payload.</param>
        private void OnAbortTransactionResult(AbortTransactionResultEventArgs e)
        {
            EventHandler<AbortTransactionResultEventArgs> h = AbortTransactionResultReceived;
            h?.Invoke(this, e);
        }

        /// <summary>
        /// Protocol event handler for abort results; updates session state and notifies subscribers.
        /// </summary>
        /// <param name="sender">The protocol source.</param>
        /// <param name="e">Abort result payload.</param>
        private void OnAbortTransactionResult(object sender, AbortTransactionResultEventArgs e)
        {
            try
            {
                TerminalSession session = GetLastRunningSession();

                if (session != null)
                {
                    if (e.IsAborted)
                        session.MarkAborted();
                }

                OnAbortTransactionResult(e);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"{nameof(OnAbortTransactionResult)}:\n{ex}");
            }
        }

        private void OnProtocolError(ExceptionEventArgs e)
        {
            EventHandler<ExceptionEventArgs> h = TerminalError;
            h?.Invoke(this, e);
        }

        /// <summary>
        /// Protocol event handler for communication or protocol-level errors.
        /// <para>
        /// Invoked when a lower-level operation detects an error condition.
        /// Calls <see cref="HandleProtocolError(ExceptionEventArgs)"/> for internal
        /// processing and then raises the <see cref="TerminalError"/> event
        /// through <see cref="OnProtocolError(ExceptionEventArgs)"/> to notify subscribers.
        /// </para>
        /// </summary>
        /// <param name="sender">The source object that raised the error event.</param>
        /// <param name="e">The exception event arguments describing the error.</param>
        private void OnProtocolError(object sender, ExceptionEventArgs e)
        {
            HandleProtocolError(e);
            OnProtocolError(e);
        }
    }

    /// <summary>
    /// Indicates the type of terminal session (payment, refund, or reversal).
    /// </summary>
    public enum SessionType
    {
        /// <summary>
        /// A standard payment transaction session.
        /// </summary>
        Payment,

        /// <summary>
        /// A refund transaction session.
        /// </summary>
        Refund,

        /// <summary>
        /// A reversal transaction session against a prior transaction.
        /// </summary>
        Reversal,
        /// <summary>
        /// A retrieval transaction session
        /// </summary>
        Retrieve
    }

    /// <summary>
    /// Represents the lifecycle state of a terminal session.
    /// </summary>
    public enum SessionState
    {
        /// <summary>
        /// The session is created but has not started terminal interaction yet.
        /// </summary>
        Created = 0,

        /// <summary>
        /// The session is currently active and interacting with the terminal.
        /// </summary>
        Running = 1,

        /// <summary>
        /// The session completed normally and has a final result.
        /// </summary>
        Completed = 2,

        /// <summary>
        /// A bonus card was detected; payment was paused awaiting ECR action.
        /// </summary>
        BonusDetectedAndHalted = 3,

        /// <summary>
        /// The terminal aborted the session (e.g., stop key, card removed).
        /// </summary>
        TerminalAborted = 4,

        /// <summary>
        /// The session was aborted by the ECR/cashier logic.
        /// </summary>
        Aborted = 5,
        /// <summary>
        /// The session was set completed with error.
        /// </summary>
        Error = 6
    }

    /// <summary>
    /// Base type for all ECR terminal sessions, encapsulating common properties
    /// such as identity, timestamps, transaction identifiers, state transitions,
    /// and received results.
    /// </summary>
    public abstract class TerminalSession
    {
        /// <summary>
        /// Unique identifier for correlating this session with external systems,
        /// logs, or user interface components. Auto-generated if not supplied.
        /// </summary>
        public string SessionId { get; }

        /// <summary>
        /// Indicates the logical purpose of this session
        /// (e.g., <see cref="SessionType.Payment"/>, <see cref="SessionType.Refund"/>, or <see cref="SessionType.Reversal"/>).
        /// </summary>
        public SessionType SessionType { get; protected set; }

        /// <summary>
        /// The transaction identifier assigned by the terminal.
        /// May remain the <see cref="ECRProtocol.EmptyTransactionId"/> placeholder
        /// until the first <c>TransactionStatus</c> message is received.
        /// </summary>
        public string TransactionId { get; private set; }

        /// <summary>
        /// The final transaction result payload supplied by the terminal when
        /// the session completes successfully; <c>null</c> until completion.
        /// </summary>
        public TransactionResultExEventArgs TransactionResult { get; private set; }

        /// <summary>
        /// Represents the current lifecycle phase of the session.
        /// </summary>
        public SessionState State { get; protected set; }

        /// <summary>
        /// The time when this session object was instantiated on the ECR side.
        /// </summary>
        public DateTime CreatedAt { get; }

        /// <summary>
        /// The time when the session reached a terminal state
        /// (Completed, Failed, Aborted, etc.); <c>null</c> while still active.
        /// </summary>
        public DateTime? CompletedAt { get; protected set; }

        /// <summary>
        /// The exception that caused the session to enter the <see cref="SessionState.Error"/> state;
        /// <c>null</c> if the session completed successfully or is still active.
        /// </summary>
        public Exception Exception { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this session is still in progress,
        /// meaning <see cref="SessionState.Created"/> or <see cref="SessionState.Running"/>.
        /// </summary>
        public bool IsActive =>
            State == SessionState.Created
            || State == SessionState.Running;

        /// <summary>
        /// True if <see cref="TransactionId"/> equals
        /// <see cref="ECRProtocol.EmptyTransactionId"/>, indicating that
        /// the terminal has not yet assigned an ID.
        /// </summary>
        public bool IsEmptyTransactionId => ECRProtocol.EmptyTransactionId == TransactionId;

        /// <summary>
        /// Holds the most recent transaction status event received from the terminal.
        /// Updated each time a new <c>TransactionStatus</c> message arrives.
        /// </summary>
        public TransactionStatusEventArgs TransactionStatus { get; private set; }


        /// <summary>
        /// Initializes a new instance with a randomly generated session ID.
        /// </summary>
        protected TerminalSession()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance with the specified external correlation ID.
        /// If the parameter is <c>null</c> or empty, a random eight-character ID is generated.
        /// </summary>
        /// <param name="sessionId">Optional external correlation identifier.</param>
        protected TerminalSession(string sessionId)
        {
            TransactionId = ECRProtocol.EmptyTransactionId;
            SessionId = sessionId?.Trim() ?? "";
            if (SessionId.Length == 0)
                SessionId = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();

            CreatedAt = DateTime.Now;
            State = SessionState.Created;
        }

        /// <summary>
        /// Updates the transaction identifier once the terminal assigns it
        /// (typically received in the first status message, phase 'A').
        /// </summary>
        /// <param name="transactionId">The transaction ID from the terminal.</param>
        internal void UpdateTransactionId(string transactionId)
        {
            TransactionId = transactionId;
        }

        /// <summary>
        /// Marks the session as successfully completed
        /// and stores the final <see cref="TransactionResultExEventArgs"/> payload.
        /// </summary>
        /// <param name="e">Extended transaction result event arguments.</param>
        internal void MarkCompleted(TransactionResultExEventArgs e)
        {
            TransactionResult = e;
            State = SessionState.Completed;
            CompletedAt = DateTime.Now;
        }

        /// <summary>
        /// Marks the session as aborted by the ECR or cashier logic
        /// (for example, due to a user-initiated cancel command).
        /// </summary>
        internal void MarkAborted()
        {
            State = SessionState.Aborted;
            CompletedAt = DateTime.Now;
        }

        /// <summary>
        /// Marks the session as halted because a bonus card was detected
        /// and the terminal is waiting for ECR confirmation or restart.
        /// </summary>
        internal void MarkBonusDetectedAndHalted()
        {
            State = SessionState.BonusDetectedAndHalted;
            CompletedAt = DateTime.Now;
        }

        /// <summary>
        /// Marks the session as aborted by the terminal itself
        /// (for example, the payer pressed cancel on the device).
        /// </summary>
        internal void MarkTerminalAborted()
        {
            State = SessionState.TerminalAborted;
            CompletedAt = DateTime.Now;
        }

        /// <summary>
        /// Marks the session as failed due to an error and
        /// records the associated exception details.
        /// </summary>
        /// <param name="exception">The exception that caused the session to fail.</param>
        internal void MarkError(Exception exception)
        {
            State = SessionState.Error;
            CompletedAt = DateTime.Now;
            Exception = exception;
        }

        /// <summary>
        /// Updates the stored <see cref="TransactionStatus"/> with a newly received
        /// status message from the terminal.
        /// </summary>
        /// <param name="e">The transaction status event arguments.</param>
        internal void UpdateTransactionStatus(TransactionStatusEventArgs e)
        {
            TransactionStatus = e;
        }
    }

    /// <summary>
    /// Session representing a payment flow and any associated bonus/manual auth data.
    /// </summary>
    public class PaymentSession : TerminalSession
    {
        /// <summary>
        /// The amount to charge for the payment session.
        /// </summary>
        public decimal Amount { get; }

        /// <summary>
        /// Indicates whether bonus handling is performed in-terminal for this payment.
        /// </summary>
        public bool IsBonusHandled { get; }

        /// <summary>
        /// Bonus/customer number captured during the flow, if available.
        /// </summary>
        public string BonusCustomerNumber { get; private set; }

        /// <summary>
        /// Bonus membership class/level captured during the flow, if available.
        /// </summary>
        public string BonusMemberClass { get; private set; }

        /// <summary>
        /// Status code from terminal.
        /// </summary>
        public string BonusStatusCode { get; private set; }

        /// <summary>
        /// Human-readable status text from terminal.
        /// </summary>
        public string BonusStatusText { get; private set; }

        /// <summary>
        /// Manual authorization code entered by the operator during the flow, if any.
        /// </summary>
        public string ManualAuthorizationCode { get; private set; }

        /// <summary>
        /// Creates a payment session and places it in the running state.
        /// </summary>
        /// <param name="amount">Amount to charge.</param>
        /// <param name="isBonusHandled">If <c>true</c>, bonus handling is expected.</param>
        /// <param name="sessionId">Optional external correlation id.</param>
        public PaymentSession(decimal amount, bool isBonusHandled, string sessionId)
            : base(sessionId)
        {
            Amount = amount;
            IsBonusHandled = isBonusHandled;
            SessionType = SessionType.Payment;
            State = SessionState.Running;
        }

        /// <summary>
        /// Records the manual authorization code provided by the operator.
        /// </summary>
        /// <param name="authorizationCode">The manual authorization code.</param>
        internal void UpdateManualAuthorizationCode(string authorizationCode)
        {
            ManualAuthorizationCode = authorizationCode;
        }

        /// <summary>
        /// Updates bonus/customer information captured during the payment flow.
        /// </summary>
        /// <param name="customerNumber">Bonus card/customer number.</param>
        /// <param name="memberClass">Membership class/level.</param>
        /// <param name="statusCode">Status code from the terminal.</param>
        /// <param name="statusText">Status text from the terminal.</param>
        internal void UpdateBonusInfo(string customerNumber, string memberClass, string statusCode, string statusText)
        {
            BonusCustomerNumber = customerNumber;
            BonusMemberClass = memberClass;
            BonusStatusCode = statusCode;
            BonusStatusText = statusText;
        }
    }

    /// <summary>
    /// Session representing a refund flow.
    /// </summary>
    public class RefundSession : TerminalSession
    {
        /// <summary>
        /// The amount to refund for this refund session.
        /// </summary>
        public decimal Amount { get; }

        /// <summary>
        /// Creates a refund session and places it in the running state.
        /// </summary>
        /// <param name="amount">Amount to refund.</param>
        /// <param name="sessionId">Optional external correlation id.</param>
        public RefundSession(decimal amount, string sessionId = null) 
            : base(sessionId)
        {
            Amount = amount;
            SessionType = SessionType.Refund;
            State = SessionState.Running;
        }
    }

    /// <summary>
    /// Session representing a reversal flow for a previous transaction.
    /// </summary>
    public class ReversalSession : TerminalSession
    {
        /// <summary>
        /// The original transaction id for which this reversal is performed.
        /// </summary>
        public string OriginalTransactionId { get; }

        /// <summary>
        /// The timestamp of the original transaction being reversed.
        /// </summary>
        public DateTime OriginalTimestamp { get; }

        /// <summary>
        /// Creates a reversal session for the specified original transaction.
        /// </summary>
        /// <param name="transactionId">Original transaction id.</param>
        /// <param name="timestamp">Original transaction timestamp.</param>
        /// <param name="sessionId">Optional external correlation id.</param>
        public ReversalSession(string transactionId, DateTime timestamp, string sessionId = null)
            :base(sessionId)
        {
            OriginalTransactionId = transactionId;
            OriginalTimestamp = timestamp;
            SessionType = SessionType.Reversal;
            State = SessionState.Running;
        }
    }

    /// <summary>
    /// Session representing a retrieval flow for a transaction.
    /// </summary>
    public class RetrieveSession : TerminalSession
    {
        /// <summary>
        /// Transaction timestamp.
        /// </summary>
        public DateTime Timestamp { get; }

        public RetrieveSession(string transactionId, DateTime timestamp)
            : base(transactionId)
        {
            Timestamp = timestamp;
            SessionType = SessionType.Retrieve;
            State = SessionState.Running;
        }
    }
}
