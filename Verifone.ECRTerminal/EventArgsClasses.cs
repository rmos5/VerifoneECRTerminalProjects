using System;

namespace Verifone.ECRTerminal
{
    public class TransactionStatusEventArgs : EventArgs
    {
        public TransactionStatusEventArgs(string statusPhase, string statusPhaseMessage, string statusResultCode, string statusResultCodeMessage, string info)
        {
            StatusPhase = statusPhase;
            StatusPhaseMessage = statusPhaseMessage;
            StatusResultCode = statusResultCode;
            StatusResultCodeMessage = statusResultCodeMessage;
            Info = info;
        }

        public string StatusPhase { get; }
        public string StatusPhaseMessage { get; }
        public string StatusResultCode { get; }
        public string StatusResultCodeMessage { get; }
        public string Info { get; }

        public override string ToString()
        {
            return $"{StatusPhase} {StatusPhaseMessage} {StatusResultCode} {StatusResultCodeMessage} {Info}";
        }
    }

    public class TransactionEventArgs : EventArgs
    {
        public string TransactionId { get; }
        public DateTime TransactionDateTime { get; set; }

        public TransactionEventArgs(string transactionId, DateTime transactionDateTime)
        {
            TransactionId = transactionId;
            TransactionDateTime = transactionDateTime;
        }
    }

    public class AbortTransactionResultEventArgs : EventArgs
    {
        public AbortTransactionResultEventArgs(string resultCode)
        {
            ResultCode = resultCode;
            Message = IsAborted ? StringResources.GetCommonString(StringResources.MessageTransactionAborted) : StringResources.GetCommonString(StringResources.MessageTransactionAbortFailed);
        }

        public string ResultCode { get; }
        public string Message { get; }
        public bool IsAborted => ResultCode?.Equals("721") == true;

        public override string ToString()
        {
            return $"{ResultCode} {IsAborted}";
        }
    }

    public class UserPromptEventArgs : EventArgs
    {
        public UserPromptEventArgs(string resultCode, string promptMessage, string info)
        {
            ResultCode = resultCode;
            PromptMessage = promptMessage;
            Info = info;
        }

        public string ResultCode { get; }
        public string PromptMessage { get; }
        public string Info { get; }

        public override string ToString()
        {
            return $"{ResultCode} {PromptMessage} {Info}";
        }
    }

    public class DeviceControlResultEventArgs : EventArgs
    {
        public DeviceControlResultEventArgs(DeviceStatus deviceStatus)
        {
            DeviceStatus = deviceStatus ?? throw new ArgumentNullException(nameof(deviceStatus));
        }

        public DeviceStatus DeviceStatus { get; }

        public override string ToString()
        {
            return $"{DeviceStatus}";
        }
    }

    public class TransactionResultExEventArgs : EventArgs
    {
        public string MessageId { get; }
        public string TransactionType { get; }
        public string PaymentMethod { get; }
        public string CardType { get; }
        public string TransactionUsage { get; }
        public string SettlementId { get; }
        public string MaskedCardNumber { get; }
        public string Aid { get; }
        public string TransactionCertificate { get; }
        public string Tvr { get; }
        public string Tsi { get; }
        public string TransactionId { get; }
        public string FilingCode { get; }
        public DateTime TransactionDateTime { get; }
        public decimal Amount { get; }
        public string Currency { get; }
        public string ReaderSerialNumber { get; }
        public int PrintPayeeReceipt { get; }
        public string Flags { get; }
        public string PayerReceiptText { get; }
        public string PayeeReceiptText { get; }

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

    public class CustomerRequestResultEventArgs : EventArgs
    {
        public CustomerRequestResultEventArgs(string customerNumber, string memberClass, string statusText)
        {
            CustomerNumber = customerNumber;
            MemberClass = memberClass;
            StatusText = statusText;
        }

        public string CustomerNumber { get; }
        public string MemberClass { get; }
        public string StatusText { get; }

        public override string ToString()
        {
            return $"{CustomerNumber} {MemberClass} {StatusText}";
        }
    }

    public class TransactionResultEventArgs
    {
        public TransactionResultEventArgs(TransactionResultExEventArgs transactionInfo, CustomerRequestResultEventArgs bonusInfo, string sessionId, string transactionFilePath)
        {
            TransactionInfo = transactionInfo;
            BonusInfo = bonusInfo;
            SessionId = sessionId;
            TransactionFilePath = transactionFilePath;
        }

        public TransactionResultExEventArgs TransactionInfo { get; }
        public CustomerRequestResultEventArgs BonusInfo { get; }
        public string SessionId { get; }
        public string TransactionFilePath { get; set; }

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

    public class ExceptionEventArgs : EventArgs
    {
        public Exception Exception { get; private set; }

        public ExceptionEventArgs(Exception exception)
        {
            Exception = exception;
        }
    }
}
