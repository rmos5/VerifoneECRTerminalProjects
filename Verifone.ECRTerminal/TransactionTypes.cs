namespace Verifone.ECRTerminal
{
    public static class TransactionTypes
    {
        public const string Purchase = "0";
        public const string Withdrawal = "1";
        public const string Reversal = "2";
        public const string Refund = "3";
        public const string QuasiCash = "5";
        public const string Cashback = "6";
        public const string Preauthorization = "P";
        public const string PreauthorizedTransaction = "F";
        public const string PreauthCancel = "V";


        public static string GetTypeString(string value)
        {
            string result = value?.ToString() ?? string.Empty;
            switch (result)
            {
                case Cashback: result = "Cashback"; break;
                case Preauthorization: result = "Preauth"; break;
                case PreauthorizedTransaction: result = "Preauth tx"; break;
                case PreauthCancel: result = "Preauth cancel"; break;
                case QuasiCash: result = "QuasiCash"; break;
                case Purchase: result = "Purhchase"; break;
                case Refund: result = "Refund"; break;
                case Reversal: result = "Reversal"; break;
                case Withdrawal: result = "Withdrawal"; break;
            }

            return result;
        }
    }
}