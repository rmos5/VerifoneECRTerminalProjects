using System;

namespace Verifone.ECRTerminal
{
    /// <summary>
    /// Provides data for transaction status updates.
    /// </summary>
    public class TransactionStatusEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TransactionStatusEventArgs"/> class.
        /// </summary>
        /// <param name="statusPhase">The status phase code.</param>
        /// <param name="statusPhaseMessage">The localized status phase message.</param>
        /// <param name="statusResultCode">The status result code.</param>
        /// <param name="statusResultCodeMessage">The localized status result code message.</param>
        /// <param name="info">Additional status information.</param>
        public TransactionStatusEventArgs(string statusPhase, string statusPhaseMessage, string statusResultCode, string statusResultCodeMessage, string info)
        {
            StatusPhase = statusPhase;
            StatusPhaseMessage = statusPhaseMessage;
            StatusResultCode = statusResultCode;
            StatusResultCodeMessage = statusResultCodeMessage;
            Info = info;
        }

        /// <summary>
        /// Gets the status phase code.
        /// </summary>
        public string StatusPhase { get; }

        /// <summary>
        /// Gets the localized status phase message.
        /// </summary>
        public string StatusPhaseMessage { get; }

        /// <summary>
        /// Gets the status result code.
        /// </summary>
        public string StatusResultCode { get; }

        /// <summary>
        /// Gets the localized status result code message.
        /// </summary>
        public string StatusResultCodeMessage { get; }

        /// <summary>
        /// Gets additional status information.
        /// </summary>
        public string Info { get; }

        public override string ToString()
        {
            return $"{StatusPhase} {StatusPhaseMessage} {StatusResultCode} {StatusResultCodeMessage} {Info}";
        }
    }

    /// <summary>
    /// Provides data for transaction events.
    /// </summary>
    public class TransactionEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the transaction identifier.
        /// </summary>
        public string TransactionId { get; }

        /// <summary>
        /// Gets or sets the transaction date and time.
        /// </summary>
        public DateTime TransactionDateTime { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TransactionEventArgs"/> class.
        /// </summary>
        /// <param name="transactionId">The transaction identifier.</param>
        /// <param name="transactionDateTime">The transaction date and time.</param>
        public TransactionEventArgs(string transactionId, DateTime transactionDateTime)
        {
            TransactionId = transactionId;
            TransactionDateTime = transactionDateTime;
        }
    }

    /// <summary>
    /// Provides data for transaction abort results.
    /// </summary>
    public class AbortTransactionResultEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AbortTransactionResultEventArgs"/> class.
        /// </summary>
        /// <param name="resultCode">The result code returned from the terminal.</param>
        public AbortTransactionResultEventArgs(string resultCode)
        {
            ResultCode = resultCode;
            Message = IsAborted ? StringResources.GetCommonString(StringResources.MessageTransactionAborted) : StringResources.GetCommonString(StringResources.MessageTransactionAbortFailed);
        }

        /// <summary>
        /// Gets the terminal result code.
        /// </summary>
        public string ResultCode { get; }

        /// <summary>
        /// Gets the localized status message for the abort result.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets a value indicating whether the transaction was aborted.
        /// </summary>
        public bool IsAborted => ResultCode?.Equals("721") == true;

        public override string ToString()
        {
            return $"{ResultCode} {IsAborted}";
        }
    }

    /// <summary>
    /// Provides data for user prompt events.
    /// </summary>
    public class UserPromptEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UserPromptEventArgs"/> class.
        /// </summary>
        /// <param name="resultCode">The prompt result code.</param>
        /// <param name="promptMessage">The localized prompt message.</param>
        /// <param name="info">Additional prompt information.</param>
        public UserPromptEventArgs(string resultCode, string promptMessage, string info)
        {
            ResultCode = resultCode;
            PromptMessage = promptMessage;
            Info = info;
        }

        /// <summary>
        /// Gets the prompt result code.
        /// </summary>
        public string ResultCode { get; }

        /// <summary>
        /// Gets the localized prompt message.
        /// </summary>
        public string PromptMessage { get; }

        /// <summary>
        /// Gets additional prompt information.
        /// </summary>
        public string Info { get; }

        public override string ToString()
        {
            return $"{ResultCode} {PromptMessage} {Info}";
        }
    }

    /// <summary>
    /// Provides data for device control results.
    /// </summary>
    public class DeviceControlResultEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceControlResultEventArgs"/> class.
        /// </summary>
        /// <param name="deviceStatus">The device status.</param>
        public DeviceControlResultEventArgs(DeviceStatus deviceStatus)
        {
            DeviceStatus = deviceStatus ?? throw new ArgumentNullException(nameof(deviceStatus));
        }

        /// <summary>
        /// Gets the device status.
        /// </summary>
        public DeviceStatus DeviceStatus { get; }

        public override string ToString()
        {
            return $"{DeviceStatus}";
        }
    }

    /// <summary>
    /// Provides extended data for transaction results.
    /// </summary>
    public class TransactionResultExEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the message identifier.
        /// </summary>
        public string MessageId { get; }
        /// <summary>
        /// Gets the transaction type.
        /// </summary>
        public string TransactionType { get; }
        /// <summary>
        /// Gets the payment method.
        /// </summary>
        public string PaymentMethod { get; }
        /// <summary>
        /// Gets the card type.
        /// </summary>
        public string CardType { get; }
        /// <summary>
        /// Gets the transaction usage.
        /// </summary>
        public string TransactionUsage { get; }
        /// <summary>
        /// Gets the settlement identifier.
        /// </summary>
        public string SettlementId { get; }
        /// <summary>
        /// Gets the masked card number.
        /// </summary>
        public string MaskedCardNumber { get; }
        /// <summary>
        /// Gets the application identifier (AID).
        /// </summary>
        public string Aid { get; }
        /// <summary>
        /// Gets the transaction certificate.
        /// </summary>
        public string TransactionCertificate { get; }
        /// <summary>
        /// Gets the terminal verification results (TVR).
        /// </summary>
        public string Tvr { get; }
        /// <summary>
        /// Gets the transaction status information (TSI).
        /// </summary>
        public string Tsi { get; }
        /// <summary>
        /// Gets the transaction identifier.
        /// </summary>
        public string TransactionId { get; }
        /// <summary>
        /// Gets the filing code.
        /// </summary>
        public string FilingCode { get; }
        /// <summary>
        /// Gets the transaction date and time.
        /// </summary>
        public DateTime TransactionDateTime { get; }
        /// <summary>
        /// Gets the transaction amount.
        /// </summary>
        public decimal Amount { get; }
        /// <summary>
        /// Gets the transaction currency.
        /// </summary>
        public string Currency { get; }
        /// <summary>
        /// Gets the reader serial number.
        /// </summary>
        public string ReaderSerialNumber { get; }
        /// <summary>
        /// Gets the payee receipt print flag.
        /// </summary>
        public int PrintPayeeReceipt { get; }
        /// <summary>
        /// Gets the flags value.
        /// </summary>
        public string Flags { get; }
        /// <summary>
        /// Gets the payer receipt text.
        /// </summary>
        public string PayerReceiptText { get; }
        /// <summary>
        /// Gets the payee receipt text.
        /// </summary>
        public string PayeeReceiptText { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TransactionResultExEventArgs"/> class.
        /// </summary>
        public TransactionResultExEventArgs(
            string messageId,
            string transactionType,
            string paymentMethod,
            string cardType,
            string transactionUsage,
            string settlementId,
            string maskedCardNumber,
            string aid,
            string transactionCertificate,
            string tvr,
            string tsi,
            string transactionId,
            string filingCode,
            DateTime transactionDateTime,
            decimal amount,
            string currency,
            string readerSerialNumber,
            int printPayeeReceipt,
            string flags,
            string payerReceiptText,
            string payeeReceiptText)
        {
            MessageId = messageId;
            TransactionType = transactionType;
            PaymentMethod = paymentMethod;
            CardType = cardType;
            TransactionUsage = transactionUsage;
            SettlementId = settlementId;
            MaskedCardNumber = maskedCardNumber;
            Aid = aid;
            TransactionCertificate = transactionCertificate;
            Tvr = tvr;
            Tsi = tsi;
            TransactionId = transactionId;
            FilingCode = filingCode;
            TransactionDateTime = transactionDateTime;
            Amount = amount;
            Currency = currency;
            ReaderSerialNumber = readerSerialNumber;
            PrintPayeeReceipt = printPayeeReceipt;
            Flags = flags;
            PayerReceiptText = payerReceiptText;
            PayeeReceiptText = payeeReceiptText;
        }

        public override string ToString()
        {
            return $"{MessageId} {TransactionId} {TransactionType} {PaymentMethod} {Amount}";
        }
    }

    /// <summary>
    /// Provides data for customer request results.
    /// </summary>
    public class CustomerRequestResultEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CustomerRequestResultEventArgs"/> class.
        /// </summary>
        /// <param name="customerNumber">The customer number.</param>
        /// <param name="memberClass">The member class.</param>
        /// <param name="statusCode">The status code (read mode).</param>
        /// <param name="statusText">The status text.</param>
        public CustomerRequestResultEventArgs(string customerNumber, string memberClass, string statusCode, string statusText)
        {
            CustomerNumber = customerNumber;
            MemberClass = memberClass;
            StatusCode = statusCode;
            StatusText = statusText;
        }

        /// <summary>
        /// Gets the customer number.
        /// </summary>
        public string CustomerNumber { get; }

        /// <summary>
        /// Gets the member class.
        /// </summary>
        public string MemberClass { get; }

        /// <summary>
        /// Gets the status text.
        /// </summary>
        public string StatusCode { get; }

        /// <summary>
        /// Gets the status text.
        /// </summary>
        public string StatusText { get; }

        public override string ToString()
        {
            return $"{CustomerNumber} {MemberClass} {StatusText}";
        }
    }

    /// <summary>
    /// Aggregates transaction and bonus information for a completed transaction.
    /// </summary>
    public class TransactionResultEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TransactionResultEventArgs"/> class.
        /// </summary>
        /// <param name="transactionInfo">The extended transaction information.</param>
        /// <param name="bonusInfo">The customer bonus information.</param>
        /// <param name="sessionId">The session identifier.</param>
        /// <param name="transactionFilePath">The transaction file path.</param>
        public TransactionResultEventArgs(TransactionResultExEventArgs transactionInfo, CustomerRequestResultEventArgs bonusInfo, string sessionId, string transactionFilePath)
        {
            TransactionInfo = transactionInfo;
            BonusInfo = bonusInfo;
            SessionId = sessionId;
            TransactionFilePath = transactionFilePath;
        }

        /// <summary>
        /// Gets the extended transaction information.
        /// </summary>
        public TransactionResultExEventArgs TransactionInfo { get; }

        /// <summary>
        /// Gets the customer bonus information.
        /// </summary>
        public CustomerRequestResultEventArgs BonusInfo { get; }

        /// <summary>
        /// Gets the session identifier.
        /// </summary>
        public string SessionId { get; }

        /// <summary>
        /// Gets or sets the transaction file path.
        /// </summary>
        public string TransactionFilePath { get; set; }

        /// <summary>
        /// Gets a value indicating whether bonus information is present.
        /// </summary>
        public bool HasBonusInfo => BonusInfo != null;

        public override string ToString()
        {
            return $"{TransactionInfo} {BonusInfo} {HasBonusInfo}";
        }
    }

    /// <summary>
    /// Provides data for the <see cref="TerminalCommandAccepted"/> event,
    /// raised when the terminal acknowledges receipt of a command frame (ACK).
    /// </summary>
    public class TerminalCommandAcceptedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the unique command identifier associated with the accepted command.
        /// </summary>
        public string CommandId { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TerminalCommandAcceptedEventArgs"/> class.
        /// </summary>
        /// <param name="commandId">The unique identifier of the accepted command.</param>
        public TerminalCommandAcceptedEventArgs(string commandId)
        {
            CommandId = commandId;
        }

        public override string ToString()
        {
            return $"CommandId='{CommandId}'";
        }
    }

    /// <summary>
    /// Provides data for exception events.
    /// </summary>
    public class ExceptionEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the exception instance.
        /// </summary>
        public Exception Exception { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExceptionEventArgs"/> class.
        /// </summary>
        /// <param name="exception">The exception instance.</param>
        public ExceptionEventArgs(Exception exception)
        {
            Exception = exception;
        }
    }
}
