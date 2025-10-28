using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace Verifone.ECRTerminal
{
    /// <summary>
    /// Provides serialization and deserialization for <see cref="TransactionResultEventArgs"/> 
    /// to and from a custom INI-like text format. 
    /// 
    /// <para>
    /// The format is divided into sections:
    /// <list type="bullet">
    ///   <item>
    ///     <description>[TransactionInfo] – core transaction fields</description>
    ///   </item>
    ///   <item>
    ///     <description>[BonusInfo] – optional customer/bonus data</description>
    ///   </item>
    ///    <item>
    ///     <description>[ExtraInfo] – optional extra data</description>
    ///   </item>
    /// </list>
    /// </para>
    /// 
    /// <para>
    /// Values are encoded as key=value pairs with the following rules:
    /// <list type="bullet">
    ///   <item>
    ///     <description><c>null:</c> → preserved as <c>null</c></description>
    ///   </item>
    ///   <item>
    ///     <description>empty string (<c>""</c>) → written as nothing after '='</description>
    ///   </item>
    ///   <item>
    ///     <description>values containing newlines, '=' or leading/trailing spaces → 
    ///     Base64 encoded with <c>b64:</c> prefix</description>
    ///   </item>
    ///   <item>
    ///     <description>all other values → written as plain text</description>
    ///   </item>
    /// </list>
    /// </para>
    /// 
    /// <para>
    /// This serializer ensures round-trip fidelity between object instances and text files, 
    /// while keeping most fields human-readable and providing light obfuscation for verbose 
    /// or multiline fields such as receipts.
    /// </para>
    /// 
    /// <remarks>
    /// This format is intended for durable persistence of pending transaction data 
    /// that may need to be retried or manually processed later. 
    /// It is not meant as a secure storage mechanism: Base64 encoding is only a 
    /// readability barrier, not encryption.
    /// </remarks>
    /// </summary>
    public static class TransactionResultTextSerializer
    {
        private const string SecTransactionInfo = "[TransactionInfo]";
        private const string SecBonusInfo = "[BonusInfo]";
        private const string SecExtraInfo = "[ExtraInfo]";

        // For fields that may contain newlines/separators/leading-trailing spaces, use "b64:" + base64(UTF-8)
        private static string Encode(string value)
        {
            if (value == null)
                return "null:";   // explicit marker for null

            if (value.Length == 0)
                return string.Empty; // empty string stays empty

            if (value.IndexOfAny(new[] { '\r', '\n', '=' }) >= 0 || value.Length != value.Trim().Length)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(value);
                return "b64:" + Convert.ToBase64String(bytes);
            }

            return value; // safe plain text
        }

        private static string Decode(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty; // empty string

            if (value == "null:")
                return null; // restore null

            if (value.StartsWith("b64:", StringComparison.Ordinal))
            {
                string b64 = value.Substring(4);
                byte[] bytes = Convert.FromBase64String(b64);
                return Encoding.UTF8.GetString(bytes);
            }

            return value; // plain text
        }

        public static string Serialize(TransactionResultEventArgs data)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(SecTransactionInfo);
            Append(sb, "MessageId", data.TransactionInfo.MessageId);
            Append(sb, "TransactionType", data.TransactionInfo.TransactionType);
            Append(sb, "PaymentMethod", data.TransactionInfo.PaymentMethod);
            Append(sb, "CardType", data.TransactionInfo.CardType);
            Append(sb, "TransactionUsage", data.TransactionInfo.TransactionUsage);
            Append(sb, "SettlementId", data.TransactionInfo.SettlementId);
            Append(sb, "MaskedCardNumber", data.TransactionInfo.MaskedCardNumber);
            Append(sb, "Aid", data.TransactionInfo.Aid);
            Append(sb, "TransactionCertificate", data.TransactionInfo.TransactionCertificate);
            Append(sb, "Tvr", data.TransactionInfo.Tvr);
            Append(sb, "Tsi", data.TransactionInfo.Tsi);
            Append(sb, "TransactionId", data.TransactionInfo.TransactionId);
            Append(sb, "FilingCode", data.TransactionInfo.FilingCode);
            sb.AppendLine("TransactionDateTime=" + data.TransactionInfo.TransactionDateTime.ToString("o", CultureInfo.InvariantCulture));
            sb.AppendLine("Amount=" + data.TransactionInfo.Amount.ToString(CultureInfo.InvariantCulture));
            Append(sb, "Currency", data.TransactionInfo.Currency);
            Append(sb, "ReaderSerialNumber", data.TransactionInfo.ReaderSerialNumber);
            sb.AppendLine("PrintPayeeReceipt=" + data.TransactionInfo.PrintPayeeReceipt.ToString(CultureInfo.InvariantCulture));
            // Multiline / verbose fields (encode safely)
            Append(sb, "Flags", data.TransactionInfo.Flags, encodeAlways: true);
            Append(sb, "PayerReceiptText", data.TransactionInfo.PayerReceiptText, encodeAlways: true);
            Append(sb, "PayeeReceiptText", data.TransactionInfo.PayeeReceiptText, encodeAlways: true);

            if (data.BonusInfo != null)
            {
                sb.AppendLine(SecBonusInfo);
                Append(sb, "CustomerNumber", data.BonusInfo.CustomerNumber);
                Append(sb, "MemberClass", data.BonusInfo.MemberClass);
                Append(sb, "StatusText", data.BonusInfo.StatusText, encodeAlways: true);
            }

            sb.AppendLine(SecExtraInfo);
            Append(sb, "SessionId", data.SessionId);

            return sb.ToString();
        }

        private static void Append(StringBuilder sb, string key, string value, bool encodeAlways = false)
        {
            string encoded = encodeAlways
                ? "b64:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty))
                : Encode(value);
            sb.Append(key).Append('=').AppendLine(encoded);
        }

        public static TransactionResultEventArgs Deserialize(string content)
        {
            Dictionary<string, string> ti = new Dictionary<string, string>(StringComparer.Ordinal);
            Dictionary<string, string> bi = new Dictionary<string, string>(StringComparer.Ordinal);
            Dictionary<string, string> xi = new Dictionary<string, string>(StringComparer.Ordinal);
            Dictionary<string, string> current = null;

            using (StringReader reader = new StringReader(content))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Length == 0 || line[0] == '#')
                        continue;

                    if (string.Equals(line, SecTransactionInfo, StringComparison.Ordinal))
                    {
                        current = ti; continue;
                    }
                    if (string.Equals(line, SecBonusInfo, StringComparison.Ordinal))
                    {
                        current = bi; continue;
                    }
                    if (string.Equals(line, SecExtraInfo, StringComparison.Ordinal))
                    {
                        current = xi; continue;
                    }
                    if (line.Length > 0 && line[0] == '[')
                    {
                        current = null; continue; // ignore unknown sections
                    }

                    if (current == null) continue;

                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq);
                    string val = line.Substring(eq + 1);
                    current[key] = val;
                }
            }

            try
            {
                string messageId = Decode(Get(ti, "MessageId"));
                string transactionType = Decode(Get(ti, "TransactionType"));
                string paymentMethod = Decode(Get(ti, "PaymentMethod"));
                string cardType = Decode(Get(ti, "CardType"));
                string transactionUsage = Decode(Get(ti, "TransactionUsage"));
                string settlementId = Decode(Get(ti, "SettlementId"));
                string maskedCardNumber = Decode(Get(ti, "MaskedCardNumber"));
                string aid = Decode(Get(ti, "Aid"));
                string transactionCertificate = Decode(Get(ti, "TransactionCertificate"));
                string tvr = Decode(Get(ti, "Tvr"));
                string tsi = Decode(Get(ti, "Tsi"));
                string transactionId = Decode(Get(ti, "TransactionId"));
                string filingCode = Decode(Get(ti, "FilingCode"));
                DateTime transactionDateTime = DateTime.Parse(Get(ti, "TransactionDateTime"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                decimal amount = decimal.Parse(Get(ti, "Amount"), CultureInfo.InvariantCulture);
                string currency = Decode(Get(ti, "Currency"));
                string readerSerialNumber = Decode(Get(ti, "ReaderSerialNumber"));
                int printPayeeReceipt = int.Parse(Get(ti, "PrintPayeeReceipt"), CultureInfo.InvariantCulture);
                string flags = Decode(Get(ti, "Flags"));
                string payerReceiptText = Decode(Get(ti, "PayerReceiptText"));
                string payeeReceiptText = Decode(Get(ti, "PayeeReceiptText"));

                TransactionResultExEventArgs trx =
                    new TransactionResultExEventArgs(
                        messageId,
                        transactionType,
                        paymentMethod,
                        cardType,
                        transactionUsage,
                        settlementId,
                        maskedCardNumber,
                        aid,
                        transactionCertificate,
                        tvr,
                        tsi,
                        transactionId,
                        filingCode,
                        transactionDateTime,
                        amount,
                        currency,
                        readerSerialNumber,
                        printPayeeReceipt,
                        flags,
                        payerReceiptText,
                        payeeReceiptText);

                CustomerRequestResultEventArgs bonus = null;
                if (bi.Count > 0)
                {
                    string customerNumber = Decode(Get(bi, "CustomerNumber"));
                    string memberClass = Decode(Get(bi, "MemberClass"));
                    string statusText = Decode(Get(bi, "StatusText"));
                    bonus = new CustomerRequestResultEventArgs(customerNumber, memberClass, statusText);
                }
                
                string sessionId = Decode(Get(xi, "SessionId"));

                TransactionResultEventArgs result =
                    new TransactionResultEventArgs(trx, bonus, sessionId, null);

                return result;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"{nameof(Deserialize)}:\n{ex}", typeof(TransactionResultTextSerializer).FullName);
                return null;
            }
        }

        private static string Get(Dictionary<string, string> dict, string key)
        {
            string val;
            return dict.TryGetValue(key, out val) ? val : string.Empty;
        }
    }
}
