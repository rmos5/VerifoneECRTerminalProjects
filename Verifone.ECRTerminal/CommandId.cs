namespace Verifone.ECRTerminal
{
    /// <summary>
    /// Defines canonical command identifiers recognized by the ECR terminal manager.
    /// These IDs are used for event correlation (e.g., in <see cref="TerminalCommandAcceptedEventArgs"/>).
    /// </summary>
    public static class CommandId
    {
        public static readonly string TestTerminal = "Test";
        public static readonly string AbortTransaction = "Abort";
        public static readonly string PurchaseTransaction = "Purchase";
        public static readonly string ReversalTransaction = "Reversal";
        public static readonly string RefundTransaction = "Refund";
        public static readonly string RetrieveTransaction = "Retrieve";
        public static readonly string RetrieveLastTransaction = "RetrieveLast";
        public static readonly string EnableBonusCardMode = "EnableBonus";
        public static readonly string DisableBonusCardMode = "DisableBonus";
        public static readonly string RequestBonusCardInfo = "RequestBonus";
        public static readonly string DisplayText = "DisplayText";
        public static readonly string ClearDisplayText = "ClearDisplay";
        public static readonly string EnableAuxiliaryMode = "EnableAuxMode";
        public static readonly string DisableAuxiliaryMode = "DisableAuxMode";
        public static readonly string AcceptTransaction = "Accept";
        public static readonly string RejectTransaction = "Reject";
        public static readonly string RequestTerminalStatus = "Status";
        public static readonly string RequestTerminalVersion = "Version";
        public static readonly string RetrieveTCSMessage = "RetrieveTCS";
    }

}
