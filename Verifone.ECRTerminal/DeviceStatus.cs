using System;
using System.Linq;

namespace Verifone.ECRTerminal
{
    public sealed class DeviceStatus
    {
        public DeviceStatus(string statusString)
        {
            StatusString = statusString;
            if (StatusString?.Length < 8
                || StatusString.First() != 'S')
                throw new ArgumentException("Invalid device status string argument.", nameof(statusString));
        }

        public string StatusString { get; } = string.Empty;
        public string MessageId => StatusString.Substring(0, 1);
        public string ResultCode => StatusString.Substring(1, 4);
        public string ResultCodeMessage
        {
            get
            {
                string result = StringResources.NoValueString;

                switch (ResultCode)
                {
                    case "0000": result = StringResources.GetCommonString(StringResources.MessageTerminalStateOk); break;
                    case "0003": result = StringResources.GetCommonString(StringResources.MessageBlacklistOldOrMissing); break;
                    case "0004": result = StringResources.GetCommonString(StringResources.MessageCAPKFileOldOrMissing); break;
                    default:
                        break;
                }

                return result;
            }
        }
        public string ReaderStatus => StatusString.Substring(5, 1);
        public string ReaderStatusMessage
        {
            get
            {
                string result = StringResources.NoValueString;

                switch (ReaderStatus)
                {
                    case "0": result = StringResources.GetCommonString(StringResources.MessageCardReaderEmpty); break;
                    case "1": result = StringResources.GetCommonString(StringResources.MessageCardInserted); break;
                    default:
                        break;
                }

                return result;
            }
        }
        public string Environment => StatusString.Substring(6, 1);
        public string EnvironmentMessage
        {
            get
            {
                string result = StringResources.NoValueString;

                switch (Environment)
                {
                    case "0": result = StringResources.GetCommonString(StringResources.MessageProductionEnvironment); break;
                    case "1": result = StringResources.GetCommonString(StringResources.MessageTestEnvironment); break;
                    default:
                        break;
                }

                return result;
            }
        }
        public string MessagePresent => StatusString.Substring(7, 1);
        public bool IsTCSMessagePresent => MessagePresent == "1";
        public string TCSMessagePresentMessage
        {
            get
            {
                string result = StringResources.NoValueString;

                switch (MessagePresent)
                {
                    case "0": result = StringResources.GetCommonString(StringResources.MessageTCSMessageMissing); break;
                    case "1": result = StringResources.GetCommonString(StringResources.MessageTCSMessagePresent); break;
                    default:
                        break;
                }

                return result;
            }
        }
        public string Data => StatusString.Substring(8);

        public override string ToString()
        {
            return $"{StatusString.Substring(0, 8)}\n{ResultCodeMessage}\n{ReaderStatusMessage}\n{EnvironmentMessage}\n{TCSMessagePresentMessage}\n{Data}";
        }
    }
}