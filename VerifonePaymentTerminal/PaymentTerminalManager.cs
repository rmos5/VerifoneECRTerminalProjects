using Verifone.ECRTerminal;

namespace VerifonePaymentTerminal
{
    internal class PaymentTerminalManager : ECRTerminalManager
    {
        public PaymentTerminalManager(string portName, IUserPromptHandler userPromptHandler, int maxSessions = 100, string dataDirectoryPath = null) : base(portName, userPromptHandler, maxSessions, dataDirectoryPath, true)
        {
        }

        protected override bool ShouldAllowManualAuthorization(string resultCode)
        {
            return false;
        }
    }
}
