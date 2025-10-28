using System;
using System.Collections.Generic;

namespace Verifone.ECRTerminal
{
    /// <summary>
    /// Defines the public contract for interacting with a Verifone payment terminal via ECR.
    /// </summary>
    public interface IECRTerminalManager : IDisposable
    {
        /// <summary>
        /// Gets a value indicating whether serial port traffic (raw bytes) is traced
        /// to diagnostic output for debugging purposes.
        /// </summary>
        bool TraceSerialBytes { get; }

        /// <summary>
        /// Gets a value indicating whether this manager instance has been disposed.
        /// </summary>
        bool IsDisposed { get; }

        /// <summary>
        /// Gets the most recently created or active terminal session,
        /// regardless of its current state (running, succeeded, or failed).
        /// </summary>
        TerminalSession LastSession { get; }

        /// <summary>
        /// Sends a handshake/test command to verify communication with the terminal.
        /// </summary>
        void TestTerminal();

        /// <summary>
        /// Requests the terminal to abort the current transaction, if any.
        /// </summary>
        void AbortTransaction();

        /// <summary>
        /// Starts a payment transaction for the specified amount.
        /// </summary>
        /// <param name="amount">The amount to charge.</param>
        /// <param name="isBonusHandled">If <c>true</c>, ECR handles bonus card flow automatically.</param>
        /// <param name="sessionId">Optional external session identifier for correlation.</param>
        void RunPayment(decimal amount, bool isBonusHandled, string sessionId = null);

        /// <summary>
        /// Puts the terminal into bonus card mode to detect and read customer bonus information.
        /// </summary>
        /// <param name="autoReply">If <c>true</c>, the terminal auto-acknowledges internal prompts.</param>
        void EnableBonusCardMode(bool autoReply);

        /// <summary>
        /// Exits bonus card mode on the terminal.
        /// </summary>
        void DisableBonusCardMode();

        /// <summary>
        /// Requests bonus card/customer info from the terminal.
        /// </summary>
        /// <param name="stopActive">If <c>true</c>, stops any active bonus mode after retrieval.</param>
        void RequestBonusCardInfo(bool stopActive);

        /// <summary>
        /// Displays two lines of text on the terminal customer display.
        /// </summary>
        /// <param name="line1">Top line text.</param>
        /// <param name="line2">Bottom line text.</param>
        /// <param name="bigFont">If <c>true</c>, uses larger font when supported.</param>
        void DisplayText(string line1, string line2, bool bigFont);

        /// <summary>
        /// Clears any text currently shown on the terminal display.
        /// </summary>
        void ClearDisplayText();

        /// <summary>
        /// Enables auxiliary accept mode for special flows that require cashier confirmation.
        /// </summary>
        void EnableAuxiliaryMode();

        /// <summary>
        /// Disables auxiliary accept mode.
        /// </summary>
        void DisableAuxiliaryMode();

        /// <summary>
        /// Sends an acceptance for a paused transaction that requires ECR confirmation.
        /// </summary>
        /// <param name="transactionId">The transaction identifier provided by the terminal.</param>
        void AcceptTransaction(string transactionId);

        /// <summary>
        /// Sends a rejection for a paused transaction that requires ECR confirmation.
        /// </summary>
        /// <param name="transactionId">The transaction identifier provided by the terminal.</param>
        void RejectTransaction(string transactionId);

        /// <summary>
        /// Requests the terminal to publish its current status.
        /// </summary>
        void RequestTerminalStatus();

        /// <summary>
        /// Requests the terminal to publish its firmware/version information.
        /// </summary>
        void RequestTerminalVersion();

        /// <summary>
        /// Starts a refund transaction for the specified amount.
        /// </summary>
        /// <param name="amount">The amount to refund.</param>
        /// <param name="sessionId">Optional external session identifier for correlation.</param>
        void Refund(decimal amount, string sessionId = null);

        /// <summary>
        /// Starts a reversal for a previously authorized transaction.
        /// </summary>
        /// <param name="transactionId">The original transaction identifier.</param>
        /// <param name="timestamp">The timestamp of the original transaction.</param>
        /// <param name="sessionId">Optional external session identifier for correlation.</param>
        void Reversal(string transactionId, DateTime timestamp, string sessionId = null);

        /// <summary>
        /// Retrieves a specific transaction result by id and timestamp from the terminal.
        /// </summary>
        /// <param name="transactionId">The transaction identifier or <see cref="ECRProtoco.Callers can’t tell it didn’t run"/> for last.</param>
        /// <param name="timestamp">The original transaction timestamp.</param>
        void RetrieveTransaction(string transactionId, DateTime timestamp);

        /// <summary>
        /// Retrieves the most recent transaction result from the terminal.
        /// </summary>
        void RetrieveLastTransaction();

        /// <summary>
        /// Requests the latest TCS (terminal control system) message from the terminal, if supported.
        /// </summary>
        void RetrieveTCSMessage();

        /// <summary>
        /// Closes the underlying serial port and releases protocol resources.
        /// </summary>
        void Disconnect();

        /// <summary>
        /// Enumerates persisted transaction results from the specified directory.
        /// </summary>
        /// <param name="directoryPath">Absolute or relative directory path containing transaction files.</param>
        /// <returns>A sequence of deserialized <see cref="TransactionResultEventArgs"/> objects.</returns>
        IEnumerable<TransactionResultEventArgs> GetTransactions(string directoryPath);

        // <summary>
        /// Enumerates persisted transaction results from the default data directory configured for this manager.
        /// </summary>
        /// <returns>A sequence of deserialized <see cref="TransactionResultEventArgs"/> objects.</returns>
        IEnumerable<TransactionResultEventArgs> GetTransactions();

        event EventHandler WakeupECRReceived;
        event EventHandler<TerminalCommandAcceptedEventArgs> TerminalCommandAccepted;
        event EventHandler<DeviceControlResultEventArgs> DeviceControlResultReceived;
        event EventHandler<TransactionStatusEventArgs> TransactionStatusChanged;
        event EventHandler<TransactionEventArgs> TransactionInitialized;
        event EventHandler<TransactionStatusEventArgs> TransactionTerminalAbortReceived;
        event EventHandler<TransactionResultEventArgs> TransactionResultReceived;
        event EventHandler<TransactionResultEventArgs> PurchaseCreated;
        event EventHandler<TransactionResultEventArgs> ReversalCreated;
        event EventHandler<TransactionResultEventArgs> RefundCreated;
        event EventHandler<TransactionResultEventArgs> TransactionRetrieved;
        event EventHandler<CustomerRequestResultEventArgs> BonusResultReceived;
        event EventHandler<AbortTransactionResultEventArgs> AbortTransactionResultReceived;
        event EventHandler<ExceptionEventArgs> TerminalError;
    }
}
