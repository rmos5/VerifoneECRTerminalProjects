using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Verifone.ECRTerminal.Properties;

namespace Verifone.ECRTerminal
{
    internal static class StringResources
    {
        internal static readonly string MessageTerminalCommunicationError = "MessageTerminalCommunicationError";
        internal static readonly string MessageTerminalStateOk = "MessageTerminalStateOk";
        internal static readonly string MessageBlacklistOldOrMissing = "MessageBlacklistOldOrMissing";
        internal static readonly string MessageCAPKFileOldOrMissing = "MessageCAPKFileOldOrMissing";
        internal static readonly string MessageCardReaderEmpty = "MessageCardReaderEmpty";
        internal static readonly string MessageCardInserted = "MessageCardInserted";
        internal static readonly string MessageProductionEnvironment = "MessageProductionEnvironment";
        internal static readonly string MessageTestEnvironment = "MessageTestEnvironment";
        internal static readonly string MessageTCSMessageMissing = "MessageTCSMessageMissing";
        internal static readonly string MessageTCSMessagePresent = "MessageTCSMessagePresent";
        internal static readonly string MessageTransactionAborted = "MessageTransactionAborted";
        internal static readonly string MessageTransactionAbortFailed = "MessageTransactionAbortFailed";
        internal static readonly string MessageLastSessionRunning = "MessageLastSessionRunning";
        internal static readonly string MessageNoPreviousPaymentSessionToRetry = "MessageNoPreviousPaymentSessionToRetry";
        internal static readonly string MessageLastSessionIsNotRetryable = "MessageLastSessionIsNotRetryable";
        internal static readonly string PatternAllOfNAttemptsFailed = "PatternAllOfNAttemptsFailed";

        private static IDictionary<string, string> _transactionStatusPhase = new Dictionary<string, string>();
        private static IDictionary<string, string> _transactionStatusResultCode = new Dictionary<string, string>();
        private static IDictionary<string, string> _transactionStatusResultCodeUserPrompt = new Dictionary<string, string>();
        private static IDictionary<string, string> _customerBonusStatus = new Dictionary<string, string>();
        private static IDictionary<string, string> _commonStrings = new Dictionary<string, string>();

        public static string NoValueString { get; set; } = "NV";

        static StringResources()
        {
            string cultureName = CultureInfo.CurrentUICulture.Name;
            //load resources
            if (cultureName.Contains("fi-FI"))
            {
                _transactionStatusPhase = ParseKeyValuePairs(Encoding.UTF8.GetString(Strings.TransactionStatusPhase_fi_FI));
                _transactionStatusResultCode = ParseKeyValuePairs(Encoding.UTF8.GetString(Strings.TransactionStatusResultCode_fi_FI));
                _transactionStatusResultCodeUserPrompt = ParseKeyValuePairs(Encoding.UTF8.GetString(Strings.TransactionStatusResultCodeUserPrompt_fi_Fi));
                _customerBonusStatus = ParseKeyValuePairs(Encoding.UTF8.GetString(Strings.CustomerBonusStatus_fi_FI));
                _commonStrings = ParseKeyValuePairs(Encoding.UTF8.GetString(Strings.CommonStrings_fi_FI));
            }
            else
            {
                _transactionStatusPhase = ParseKeyValuePairs(Encoding.UTF8.GetString(Strings.TransactionStatusPhase));
                _transactionStatusResultCode = ParseKeyValuePairs(Encoding.UTF8.GetString(Strings.TransactionStatusResultCode));
                _transactionStatusResultCodeUserPrompt = ParseKeyValuePairs(Encoding.UTF8.GetString(Strings.TransactionStatusResultCodeUserPrompt));
                _customerBonusStatus = ParseKeyValuePairs(Encoding.UTF8.GetString(Strings.CustomerBonusStatus));
                _commonStrings = ParseKeyValuePairs(Encoding.UTF8.GetString(Strings.CommonStrings));
            }

            IDictionary<string, string> ParseKeyValuePairs(string text)
            {
                Dictionary<string, string> dict = new Dictionary<string, string>();
                using (StringReader reader = new StringReader(text))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        int idx = line.IndexOf('=');
                        if (idx > 0)
                        {
                            string key = line.Substring(0, idx).Trim();
                            string value = line.Substring(idx + 1).Trim();
                            dict[key] = value;
                        }
                    }
                }
                return dict;
            }
        }

        public static string GetTransactionStatusPhaseMessage(string phase)
        {
            if (_transactionStatusPhase.TryGetValue(phase, out string message))
                return message;
            else
                return phase;
        }

        public static string GetTransactionStatusResultCodeMessage(string resultCode)
        {
            if (_transactionStatusResultCode.TryGetValue(resultCode, out var message))
                return message;

            var key = resultCode[0] + "xxx";

            if (_transactionStatusResultCode.TryGetValue(key, out message))
                return $"{message} ({resultCode}).";

            return $"{_transactionStatusResultCode["xxxx"]} ({resultCode}).";
        }

        public static string GetTransactionStatusResultCodeUserPromptMessage(string resultCode)
        {
            if (_transactionStatusResultCodeUserPrompt.TryGetValue(resultCode, out string message))
                return message;
            else
                return NoValueString;
        }

        public static string GetCustomerBonusStatusMessage(string bonusStatus)
        {
            if (_customerBonusStatus.TryGetValue(bonusStatus, out string message))
                return message;
            else
                return NoValueString;
        }

        internal static string GetCommonString(string key)
        {
            if (_commonStrings.TryGetValue(key, out string message))
                return message;
            else
                return NoValueString;
        }
    }
}
