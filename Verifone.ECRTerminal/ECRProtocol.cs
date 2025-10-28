using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Verifone.ECRTerminal
{
    internal partial class ECRProtocol : IDisposable
    {
        private const byte STX = 0x02;
        private const byte ETB = 0x17;
        private const byte ETX = 0x03;
        private const byte ACK = 0x06;
        private const byte NAK = 0x15;
        private const byte ENQ = 0x05;

        internal const string RetryTransactionCode = "A000";
        public const string EmptyTransactionId = "00000";

        private Encoding _encoding;
        private SerialPort _port;
        private int _ackDelayMs { get; } = 100;
        private bool _traceSerialBytes;
        private string _ecrOutPrefix = "ECR >";
        private string _ecrInPrefix = "ECR <";
        private int _portBytesToRead => _port.BytesToRead;
        internal bool IsOpen => _port.IsOpen;
        internal string PortName => _port.PortName;

        /// <summary>
        /// Identifies the ECR device (field 11). Used for auditing and tracing transactions from specific registers.
        /// </summary>
        private readonly string _serialNumber;
        /// <summary>
        /// Identifies the ECR number (field 20) in multi-ECR setups. Required if multiple registers share one terminal.
        /// </summary>
        private readonly string _ecrNumber;

        private Thread _readerThread;
        private volatile bool _readerRunning;
        internal readonly string DateTimeFormatLong = "yyMMddHHmmss";
        internal readonly string DateTimeFormatShort = "yyMMdd";

        internal bool IsDisposed { get; private set; } = false;

        internal event EventHandler WakeupECRReceived;
        internal event EventHandler<TransactionStatusEventArgs> TransactionStatusChanged;
        internal event EventHandler<TransactionStatusEventArgs> TransactionTerminalAbort;
        internal event EventHandler<AbortTransactionResultEventArgs> AbortTransactionResult;
        internal event EventHandler<UserPromptEventArgs> UserPromptRequired;
        internal event EventHandler<DeviceControlResultEventArgs> DeviceControlResult;
        internal event EventHandler<TransactionResultExEventArgs> TransactionResultEx;
        internal event EventHandler<CustomerRequestResultEventArgs> CustomerRequestResult;
        /// <summary>
        /// Occurs when the terminal acknowledges receipt of a command frame (<c>ACK</c>).
        /// </summary>
        internal event EventHandler<TerminalCommandAcceptedEventArgs> TerminalCommandAccepted;
        internal event EventHandler<ExceptionEventArgs> ProtocolError;

        /// <summary>
        /// Initializes a new instance of the <see cref="ECRProtocol"/> class with the specified serial communication parameters.
        /// </summary>
        /// <param name="portName">The name of the serial port to use for terminal communication (e.g., "COM1").</param>
        /// <param name="baudRate">The baud rate for the serial connection, default is 19200.</param>
        /// <param name="parity">The parity-checking protocol used by the serial port, default is <see cref="Parity.None"/>.</param>
        /// <param name="dataBits">The standard length of data bits per byte, default is 8.</param>
        /// <param name="stopBits">The number of stop bits used to indicate the end of a byte, default is <see cref="StopBits.One"/>.</param>
        /// <param name="readTimeout">The read timeout in milliseconds for incoming data, default is 3000.</param>
        /// <param name="writeTimeout">The write timeout in milliseconds for outgoing data, default is 3000.</param>
        /// <param name="serialNumber">The payment terminal’s serial number, used in transaction messages. Default is "000000000".</param>
        /// <param name="ecrNumber">The ECR identifier used in transaction references. Default is "001".</param>
        /// <param name="ackDelayMs">The delay, in milliseconds, before sending ACK responses to terminal messages. Default is 100.</param>
        /// <param name="traceSerialBytes">If <c>true</c>, raw ECR message bytes are written to trace output for diagnostics.</param>
        /// <remarks>
        /// The constructor initializes communication encoding as ISO-8859-15 and falls back to ISO-8859-1 if unavailable.
        /// It then establishes the serial connection and prepares the protocol handler for message exchange with the Verifone terminal.
        /// </remarks>
        internal ECRProtocol(string portName, int baudRate = 19200, Parity parity = Parity.None,
                           int dataBits = 8, StopBits stopBits = StopBits.One,
                           int readTimeout = 3000, int writeTimeout = 3000,
                           string serialNumber = "000000000", string ecrNumber = "001",
                           int ackDelayMs = 100, bool traceSerialBytes = false)
        {
            try
            {
                _encoding = Encoding.GetEncoding("ISO-8859-15");
            }
            catch (ArgumentException)
            {
                _encoding = Encoding.GetEncoding(28591); // ISO-8859-1 fallback
            }

            _serialNumber = serialNumber;
            _ecrNumber = ecrNumber;
            _ackDelayMs = ackDelayMs;
            _traceSerialBytes = traceSerialBytes;
            Initialize(portName, baudRate, parity, dataBits, stopBits, readTimeout, writeTimeout);
        }

        public void Dispose()
        {
            Trace.WriteLine($"{nameof(Dispose)}", GetType().FullName);
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void DisposeLocalResources()
        {
            _readerRunning = false;
            try
            {
                if (_readerThread != null
                    && _readerThread.IsAlive)
                    _ = _readerThread.Join(1000);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"{nameof(DisposeLocalResources)}: Closing reader thread.\n{ex}", GetType().FullName);
            }

            try
            {
                if (IsOpen)
                    _port.Close();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"{nameof(DisposeLocalResources)}: Closing port.\n{ex}", GetType().FullName);
            }

            try
            {
                _port.Dispose();
                Trace.WriteLine("Port disposed", GetType().FullName);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"{nameof(DisposeLocalResources)}: Disposing port.\n{ex}", GetType().FullName);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            Trace.WriteLine($"{nameof(Dispose)}:{disposing}", GetType().FullName);
            if (!IsDisposed)
            {
                if (disposing)
                {
                    DisposeLocalResources();
                }

                IsDisposed = true;
            }
        }

        private void Initialize(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits, int readTimeout, int writeTimeout)
        {
            Trace.WriteLine(
                $"{nameof(Initialize)}:" +
                $"{nameof(portName)}={portName};" +
                $"{nameof(baudRate)}={baudRate};" +
                $"{nameof(parity)}={parity};" +
                $"{nameof(dataBits)}={dataBits};" +
                $"{nameof(stopBits)}={stopBits}" +
                $"{nameof(readTimeout)}={readTimeout};" +
                $"{nameof(writeTimeout)}={writeTimeout}",
                GetType().FullName);

            try
            {
                _port = new SerialPort(portName, baudRate, parity, dataBits, stopBits)
                {
                    ReadTimeout = readTimeout,
                    WriteTimeout = writeTimeout,
                    Encoding = _encoding
                };

                EnsurePortActive();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"{nameof(Initialize)}:\n{ex}", GetType().FullName);
                _readerRunning = false;
                throw;
            }
        }

        private void EnsurePortActive()
        {
            if (!IsOpen)
                _port.Open();

            if (!_readerRunning)
                StartReaderThread();
        }

        private void DiscardPortInBuffer()
        {
            _port.DiscardInBuffer();
        }

        private void WritePort(byte[] buffer)
        {
            EnsurePortActive();
            DiscardPortInBuffer();
            _port.Write(buffer, 0, buffer.Length);
            TraceECRBytes(buffer, _ecrOutPrefix);
        }

        private byte ReadPortByte()
        {
            EnsurePortActive();
            return (byte)_port.ReadByte();
        }

        /// <summary>
        /// Converts a byte array into a readable hexadecimal string representation.
        /// </summary>
        /// <param name="message">The byte array to format.</param>
        /// <returns>
        /// A string containing the hexadecimal values of the bytes in <paramref name="message"/>, 
        /// with each byte represented as a two-character uppercase hex value (e.g., "0A", "FF").
        /// </returns>
        private static string FormatReadable(byte[] message)
        {
            StringBuilder sb = new StringBuilder();
            foreach (byte b in message)
            {
                _ = sb.Append($"{b:X2}");
            }
            return sb.ToString().Trim();
        }

        internal string FormatAmount(decimal value) => ((long)Math.Round(value * 100)).ToString("D12");

        /// <summary>
        /// Writes a human-readable representation of raw ECR message bytes to the trace or debug output.
        /// </summary>
        /// <param name="data">The ECR message data as a byte array.</param>
        /// <param name="messagePrefix">A prefix text (e.g., "TX >>" or "RX <<") to identify message direction or context.</param>
        /// <remarks>
        /// When <see cref="_traceSerialBytes"/> is <c>true</c>, output is written to <see cref="Trace"/> with the current <see cref="PortName"/> as category;
        /// otherwise, the data is written to <see cref="Debug"/>. This method is intended for diagnostic and low-level protocol tracing.
        /// </remarks>
        private void TraceECRBytes(byte[] data, string messagePrefix)
        {
            string message = $"{messagePrefix} {FormatReadable(data)}";
            if (_traceSerialBytes)
                Trace.WriteLine(message, PortName);
            else
                Debug.WriteLine(message, PortName);
        }

        private void OnWakeupECRReceived()
        {
            EventHandler h = WakeupECRReceived;
            h?.Invoke(this, EventArgs.Empty);
        }

        private void OnTransactionStatusChanged(string phase, string phaseMessage, string resultCode, string resultCodeMessage, string info)
        {
            EventHandler<TransactionStatusEventArgs> h = TransactionStatusChanged;
            h?.Invoke(this, new TransactionStatusEventArgs(phase, phaseMessage, resultCode, resultCodeMessage, info));
        }

        private void OnTransactionTerminalAbort(string phase, string phaseMessage, string resultCode, string resultCodeMessage, string info)
        {
            EventHandler<TransactionStatusEventArgs> h = TransactionTerminalAbort;
            h?.Invoke(this, new TransactionStatusEventArgs(phase, phaseMessage, resultCode, resultCodeMessage, info));
        }

        private void OnAbortTransactionResult(string resultCode)
        {
            EventHandler<AbortTransactionResultEventArgs> h = AbortTransactionResult;
            AbortTransactionResultEventArgs args = new AbortTransactionResultEventArgs(resultCode);
            string message = args.IsAborted
                ? StringResources.GetCommonString(StringResources.MessageTransactionAborted)
                : StringResources.GetCommonString(StringResources.MessageTransactionAbortFailed);
            Trace.WriteLine($"{nameof(OnAbortTransactionResult)}:{message}", GetType().FullName);
            h?.Invoke(this, new AbortTransactionResultEventArgs(resultCode));
        }

        private void OnUserPromptRequired(string resultCode, string info)
        {
            EventHandler<UserPromptEventArgs> h = UserPromptRequired;
            h?.Invoke(this, new UserPromptEventArgs(resultCode, StringResources.GetTransactionStatusResultCodeUserPromptMessage(resultCode), info));
        }

        private void OnDeviceControlResultReceived(DeviceStatus deviceStatus)
        {
            EventHandler<DeviceControlResultEventArgs> h = DeviceControlResult;
            h?.Invoke(this, new DeviceControlResultEventArgs(deviceStatus));
        }

        private void OnTransactionResultExReceived(TransactionResultExEventArgs e)
        {
            EventHandler<TransactionResultExEventArgs> h = TransactionResultEx;
            h?.Invoke(this, e);
        }

        private void OnCustomerRequestResultReceived(string customerNumber, string memberClass, string statusText)
        {
            EventHandler<CustomerRequestResultEventArgs> h = CustomerRequestResult;
            h?.Invoke(this, new CustomerRequestResultEventArgs(customerNumber, memberClass, statusText));
        }

        protected virtual void OnTerminalCommandAccepted(string commandId)
        {
            EventHandler<TerminalCommandAcceptedEventArgs> h = TerminalCommandAccepted;
            h?.Invoke(this, new TerminalCommandAcceptedEventArgs(commandId));
        }

        private void OnProtocolError(Exception exception)
        {
            EventHandler<ExceptionEventArgs> h = ProtocolError;
            h?.Invoke(this, new ExceptionEventArgs(exception));
        }

        private void HandleTransactionStatus(byte[] data)
        {
            string msg = _encoding.GetString(data);
            if (msg.Length < 6) return;

            string phase = msg.Substring(1, 1);
            string phaseMessage = StringResources.GetTransactionStatusPhaseMessage(phase);
            string resultCode = msg.Substring(2, 4);
            string resultCodeMessage = StringResources.GetTransactionStatusResultCodeMessage(resultCode);
            string info = msg.Length > 6 ? msg.Substring(6) : string.Empty;

            Trace.WriteLine($"{nameof(HandleTransactionStatus)}: Phase={phase}, ResultCode={resultCode}, Info='{info}'", GetType().FullName);
            Trace.WriteLine($"{nameof(HandleTransactionStatus)}: {phaseMessage} {resultCodeMessage}", GetType().FullName);

            OnTransactionStatusChanged(phase, phaseMessage, resultCode, resultCodeMessage, info);

            switch (resultCode)
            {
                // ---  0000–0999, informational, transaction carries on to next phase ---
                case "0000": // Status is OK, no errors
                case "0001": // Bonus card detected (status OK)
                case "0002": // Card read failed, fallback continues
                case "0003": // Blacklist missing or incorrect (inform operator)
                case "0004": // CAPK missing or incorrect (inform operator)
                case "0005": // Date of birth included (YYMMDD in ExtraInfo)
                case "0014": // Using mag.stripe of chip card before chip
                case "0015": // Incorrect PIN given, retry possible
                case "0016": // Authorization authorizationCode checksum error, retry needed
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
                case "1010": // Message syntax error (e.g. zero amount)
                case "1012": // Terminal config error
                case "1013": // Timeout (application selection or PIN)
                case "1014": // Magstripe used instead of chip
                case "1015": // Incorrect PIN, last attempt
                case "1016": // App not allowed
                case "1017": // PIN bypass not allowed
                case "1018": // Auth authorizationCode error, abort
                case "1019": // Below application min amount
                case "1020": // Above application max amount
                case "1021": // Service forbidden by app (e.g. cashback)
                case "1022": // Transaction auto-cancelled (missing ACK)
                case "1024": // Card can't be processed, manual fallback
                // --- 1100+ ---
                case "1100": // No connection to Point
                case "1102": // Preauthorization not found
                case "1103": // Invalid new preauth expiration date
                    OnTransactionTerminalAbort(phase, phaseMessage, resultCode, resultCodeMessage, info);
                    break;

                // --- 2000–2999: Transaction paused, needs ECR confirmation ---
                case "2001": // Bonus card found, continue with BonusHandled = 1
                    break;
                case "2002": // Bonus card only (no payment), abort
                    break;
                case "2003": // Manual authorization required
                case "2004": // PIN bypass needs ECR confirmation
                case "2005": // ID check required (manual confirmation)
                case "2006": // Chip read failed, confirm fallback to magstripe
                case "2007": // Swedbank use: enter 4 digits
                case "2012": // PIN blocked, retry with verified customer ID
                case "2022": // Waiting for AcceptTransaction
                    OnUserPromptRequired(resultCode, info);
                    break;
                case "2008": // Reserved
                    break;

                // --- 9000–9999: Authorization declined ---
                case "91Z3": // Declined before online
                case "91Z1": // Card app expired
                case "9400": // Card declined after successful authorization
                    OnTransactionTerminalAbort(phase, phaseMessage, resultCode, resultCodeMessage, info);
                    break;
                default:
                    //note: there might be undocumented 1xxx and 9xxx codes from device and library user must be notified
                    if (resultCode.StartsWith("1")
                        || resultCode.StartsWith("9"))
                        OnTransactionTerminalAbort(phase, phaseMessage, resultCode, resultCodeMessage, info);
                    else if (resultCode.StartsWith("2"))
                        OnUserPromptRequired(resultCode, info);
                    break;
            }

            switch (phase)
            {
                case "0": // Waiting for card
                case "1": // Chip card inserted
                case "2": // Waiting for magstripe fallback
                case "3": // Magstripe card read
                case "4": // Manual card number entry
                case "5": // Language selection
                case "6": // Application selection
                case "7": // Cardholder verification (e.g. PIN)
                case "8": // Authorization in progress
                case "9": // Contactless card read
                case "A": // Transaction initialized
                case "B": // Terminal reports blacklist missing
                case "C": // Terminal reports CAPK missing
                case "#": // Preauthorization ID provided
                case "$": // Waiting for AcceptTransaction
                case "Q": // ECR confirmation required (fallback, ID check, etc.)
                case "R": // Transaction complete, waiting for card removal
                default: // Unknown transaction phase
                    break;
            }
        }

        private void HandleWakeupECR()
        {
            Trace.WriteLine($"{nameof(HandleWakeupECR)}", GetType().FullName);
            OnWakeupECRReceived();
        }

        private void HandleAbortTransactionResult(byte[] data)
        {
            string msg = _encoding.GetString(data);
            Trace.WriteLine($"{nameof(HandleAbortTransactionResult)}: TransactionResult='{msg}'", GetType().FullName);
            OnAbortTransactionResult(msg);
        }

        /// <summary>
        /// Parses and handles both standard (TransactionResult) and extended (TransactionResultEx)
        /// transaction result messages from the payment terminal. It extracts all relevant fields
        /// and raises the TransactionResultEx event using a unified event args object.
        /// </summary>
        /// <param name="data">Raw message data from the terminal.</param>
        private void HandleTransactionResult(byte[] data)
        {
            string msg = _encoding.GetString(data);
            Trace.WriteLine($"{nameof(HandleTransactionResult)}: TransactionResult='{msg}'", GetType().FullName);

            if (msg.Length < 137)
            {
                Trace.WriteLine($"{nameof(HandleTransactionResult)}: Invalid data length {msg.Length}", GetType().FullName);
                return;
            }

            bool isExtended = msg.Length > 137;

            string messageId = msg.Substring(0, 1);
            string transactionType = msg.Substring(1, 1);

            string paymentMethod = msg.Substring(2, 1);
            string cardType = msg.Substring(3, 1);
            string transactionUsage = msg.Substring(4, 1);
            string settlementId = msg.Substring(5, 2);
            string maskedCardNumber = msg.Substring(7, 19).Trim();
            string aid = msg.Substring(26, 32).Trim();
            string transactionCertificate = msg.Substring(58, 16).Trim();
            string tvr = msg.Substring(74, 10).Trim();
            string tsi = msg.Substring(84, 4).Trim();
            string transactionId = msg.Substring(88, 5).Trim();
            string filingCode = msg.Substring(93, 12).Trim();

            // DateTime: YYMMDDhhmmss
            string dateTimeStr = msg.Substring(105, 12);
            DateTime transactionDateTime;
            if (!DateTime.TryParseExact(dateTimeStr, "yyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out transactionDateTime))
            {
                transactionDateTime = DateTime.MinValue;
            }

            // Amount
            string amountStr = isExtended
                ? msg.Substring(117, 12)
                : msg.Substring(117, 7).PadLeft(12, '0');

            decimal amount = 0;
            if (long.TryParse(amountStr, out long cents))
            {
                amount = cents / 100m;
            }

            string currency = msg.Substring(isExtended ? 129 : 124, 3).Trim();
            string readerSerialNumber = msg.Substring(isExtended ? 132 : 127, 9).Trim();
            int printPayeeReceipt = msg[isExtended ? 141 : 136] - '0';
            string flags = msg.Substring(isExtended ? 142 : 137, 1);

            // Receipt texts
            string tail = msg.Substring(isExtended ? 143 : 138);
            string[] parts = tail.Split((char)0x1E);

            string payerReceiptText = parts.Length > 0 ? parts[0].TrimEnd('\x03') : "";
            string payeeReceiptText = parts.Length > 1 ? parts[1].TrimEnd('\x03') : "";

            TransactionResultExEventArgs args = new TransactionResultExEventArgs(
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
                payeeReceiptText
            );

            Trace.WriteLine($"{nameof(HandleTransactionResult)}:TransactionId={transactionId};Amount={amount:F2}, TransactionDateTime={transactionDateTime.ToString(DateTimeFormatLong)};ReaderSerialNumber={readerSerialNumber}", GetType().FullName);
            OnTransactionResultExReceived(args);
            //_ = SendRequestStatus();//To force status event in aux mode
        }

        private void HandleDeviceControlResult(byte[] data)
        {
            string status = _encoding.GetString(data);
            Trace.WriteLine($"{nameof(HandleDeviceControlResult)}: {status}", GetType().FullName);
            OnDeviceControlResultReceived(new DeviceStatus(status));
        }

        private void HandleCustomerRequestResult(byte[] data)
        {
            if (data.Length < 32)
            {
                Trace.WriteLine($"{nameof(HandleCustomerRequestResult)}: Invalid length {data.Length}", GetType().FullName);
                return;
            }

            byte status = data[1];
            string customerNumber = _encoding.GetString(data, 2, 20).Trim();
            string memberClass = _encoding.GetString(data, 22, 2);
            string statusText = StringResources.GetCustomerBonusStatusMessage(_encoding.GetString(new[] { status }));

            Trace.WriteLine($"{nameof(HandleCustomerRequestResult)}:{nameof(status)}={status} ({statusText});{nameof(customerNumber)}={customerNumber};{nameof(memberClass)}={memberClass}", GetType().FullName);

            OnCustomerRequestResultReceived(customerNumber, memberClass, statusText);
        }

        /// <summary>
        /// Handles the VerifySignature (message ID 'F') message, sent in some NFC cases where a signature check is required by ECR.
        /// </summary>
        private void HandleVerifySignature(byte[] data)
        {
            string msg = _encoding.GetString(data);
            string promptMessage = $"{StringResources.GetTransactionStatusResultCodeUserPromptMessage(RetryTransactionCode)}\n{msg}";
            Trace.WriteLine($"{nameof(HandleVerifySignature)}: {promptMessage}", GetType().FullName);
            OnUserPromptRequired(promptMessage, string.Empty);
        }

        private void StartReaderThread()
        {
            Trace.WriteLine($"{nameof(StartReaderThread)}", GetType().FullName);
            if (_readerRunning)
                return;
            _readerThread = new Thread(ReadLoop) { IsBackground = true };
            _readerThread.Start();
            _readerRunning = true;
        }

        // === Shared signaling between reader and sender ===

        // Signals sender thread when first response byte (ACK/NAK/STX) is seen by reader
        private readonly ManualResetEventSlim _firstByteEvt = new ManualResetEventSlim(false);

        // Stores the first byte value received from terminal (0 = none)
        private int _firstByteKind;

        /// <summary>
        /// Store and signal the first byte received from terminal.
        /// Used to synchronize sender with ACK/NAK/STX responses.
        /// </summary>
        private void NotifyFirstByte(byte @byte)
        {
            Trace.WriteLine($"{nameof(NotifyFirstByte)}:{@byte:X2}", GetType().FullName);
            _ = Interlocked.Exchange(ref _firstByteKind, @byte);
            _firstByteEvt.Set();
        }

        /// <summary>
        /// Continuously reads and processes messages from the payment terminal until stopped or an error occurs.
        /// <para>
        /// Responsibilities:
        /// <list type="bullet">
        /// <item><description>Reads raw bytes from the serial port and logs traffic for tracing.</description></item>
        /// <item><description>Handles control bytes (ACK, NAK, STX) and re-synchronizes on framing boundaries.</description></item>
        /// <item><description>Assembles multi-part frames, validates LRC checksums, and sends ACK/NAK responses as required by the ECR protocol.</description></item>
        /// <item><description>Dispatches complete message payloads to the appropriate message handlers (status, result, abort, etc.).</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// The loop terminates when the port is closed, <c>_readerRunning</c> is set to <c>false</c>,
        /// or an exception is raised.  
        /// On exit due to an exception, the error is logged and <see cref="OnProtocolError(ECRTerminalException)"/>
        /// is invoked to raise the <c>ProtocolError</c> event.
        /// </para>
        /// </summary>
        private void ReadLoop()
        {
            Trace.WriteLine($"{nameof(ReadLoop)} started.", GetType().FullName);

            Exception error = null;

            try
            {
                while (IsOpen)
                {
                    if (_portBytesToRead == 0)
                    {
                        Thread.Sleep(150); // avoid busy-wait if no data
                        continue;
                    }

                    // --- Always log the first byte we read if not STX ---
                    byte b = ReadPortByte();

                    // Handle single-byte flow control (ACK/NAK after send)
                    if (b == ACK 
                        || b == NAK)
                    {
                        TraceECRBytes(new byte[] { b }, _ecrInPrefix);
                        NotifyFirstByte(b);
                        continue;
                    }

                    // Only process framed messages starting with STX
                    if (b != STX)
                    {
                        TraceECRBytes(new byte[] { b }, _ecrInPrefix);
                        continue;
                    }

                    // New frame started (or continuation of multi-part)
                    NotifyFirstByte(b);

                    // Collect logical payload (data bytes only)
                    List<byte> payload = new List<byte>();
                    bool assembling = true;

                    while (assembling)
                    {
                        // --- Read one frame part: data ... ETX LRC ---
                        List<byte> partData = new List<byte>();

                        // Read until ETX marker (0x03)
                        while (true)
                        {
                            byte nextByte = ReadPortByte();

                            if (nextByte == ETX)
                            {
                                // ETX belongs to LRC domain, not to data
                                break;
                            }

                            partData.Add(nextByte);
                        }

                        // Read trailing LRC byte
                        byte lrc = ReadPortByte();

                        // Calculate expected LRC over [data + ETX]
                        List<byte> dataPlusEtx = new List<byte>(partData) { ETX };
                        byte calculated = CalculateLRC(dataPlusEtx.ToArray());

                        // Log this part as full frame (STX...ETX+LRC)
                        List<byte> logBuf = new List<byte> { STX };
                        logBuf.AddRange(partData);
                        logBuf.Add(ETX);
                        logBuf.Add(lrc);
                        TraceECRBytes(logBuf.ToArray(), _ecrInPrefix);

                        if (lrc != calculated)
                        {
                            // Invalid LRC → send NAK, wait for retransmit
                            SendNak();
                            Trace.WriteLine($"{nameof(ReadLoop)}: LRC mismatch -> NAK sent (waiting retransmit of this part).", GetType().FullName);

                            // Resync: read until next STX
                            byte syncByte = ReadPortByte();
                            TraceECRBytes(new byte[] { syncByte }, _ecrInPrefix);
                            while (syncByte != STX)
                            {
                                syncByte = ReadPortByte();
                                TraceECRBytes(new byte[] { syncByte }, _ecrInPrefix);
                            }

                            NotifyFirstByte(syncByte);

                            // Restart this part from scratch
                            partData.Clear();
                            continue;
                        }

                        // Valid part → ACK terminal
                        SendAck();

                        // Multi-part handling: ETB (0x17) means more blocks follow
                        bool trailingEtb = partData.Count >= 1 && partData[partData.Count - 1] == ETB;
                        if (trailingEtb)
                        {
                            // Append all data except trailing ETB
                            if (partData.Count > 1)
                                payload.AddRange(partData.GetRange(0, partData.Count - 1));

                            // Next part must start with STX
                            byte nextStart = ReadPortByte();
                            TraceECRBytes(new byte[] { nextStart }, _ecrInPrefix);
                            while (nextStart != STX)
                            {
                                nextStart = ReadPortByte();
                                TraceECRBytes(new byte[] { nextStart }, _ecrInPrefix);
                            }

                            NotifyFirstByte(nextStart);

                            continue; // keep assembling
                        }
                        else
                        {
                            // Final part
                            payload.AddRange(partData);
                            assembling = false;
                        }
                    }

                    // --- Dispatch completed payload by Message ID ---
                    if (payload.Count > 0)
                    {
                        char statusByte = (char)payload[0];
                        byte[] statusData = payload.ToArray();

                        switch (statusByte)
                        {
                            case '2': HandleTransactionStatus(statusData); break;
                            case '4':
                            case '5': HandleTransactionResult(statusData); break;
                            case '7': HandleAbortTransactionResult(statusData); break;
                            case 'D': HandleCustomerRequestResult(statusData); break;
                            case 'F': HandleVerifySignature(statusData); break;
                            case 'W': HandleWakeupECR(); break;
                            case 'S': HandleDeviceControlResult(statusData); break;
                            default:
                                Trace.WriteLine("Unhandled status received: '" + statusByte + "' " + FormatReadable(statusData), GetType().FullName);
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Terminate loop on error
                error = ex;
                Trace.WriteLine($"{nameof(ReadLoop)}\n{ex}", GetType().FullName);
            }
            finally
            {
                _readerRunning = false;
                Trace.WriteLine($"{nameof(ReadLoop)} exited.", GetType().FullName);
                if (error == null)
                {
                    //when port is closed, we don't have an exception object
                    error = new System.IO.IOException();
                }
                error = new ECRTerminalException(StringResources.GetCommonString(StringResources.MessageTerminalCommunicationError), error);
                OnProtocolError(error);
            }
        }

        /// <summary>
        /// Sends a protocol message to the payment terminal with retry logic and timeout handling.
        /// <para>
        /// The message is transmitted up to <paramref name="maxRetries"/> times until the terminal
        /// responds with <c>ACK</c> (0x06) or <c>STX</c> (0x02). A <c>NAK</c> (0x15) response triggers
        /// a retransmission of the same message unless the retry limit is reached.
        /// </para>
        /// <para>
        /// The method waits for the first byte response signaled by the reader thread through
        /// <see cref="_firstByteEvt"/>. When a valid acknowledgment is received, the 
        /// <see cref="OnTerminalCommandAccepted"/> event is raised to notify that the terminal has
        /// accepted the command or has already begun responding.
        /// </para>
        /// <para>
        /// If no valid response is received within <paramref name="timeoutMs"/> or after
        /// <paramref name="maxRetries"/> attempts, the function returns <c>false</c> and
        /// triggers <see cref="OnProtocolError(Exception)"/> with a <see cref="TimeoutException"/>.
        /// </para>
        /// </summary>
        /// <param name="message">The full protocol message to send, including framing and LRC.</param>
        /// <param name="commandId">The identifier of the sent command for event correlation.</param>
        /// <param name="maxRetries">The maximum number of resend attempts on timeout or NAK (default is 3).</param>
        /// <param name="timeoutMs">The maximum wait time for an ACK/NAK/STX response in milliseconds (default is 3000 ms).</param>
        /// <returns>
        /// <c>true</c> if an ACK or STX is received (command accepted or response started);
        /// otherwise <c>false</c> if all retry attempts fail or an error occurs.
        /// </returns>
        private bool SendWithRetry(byte[] message, string commandId, int maxRetries = 3, int timeoutMs = 3000)
        {
            Trace.WriteLine($"{nameof(SendWithRetry)}:{nameof(maxRetries)}={maxRetries};{nameof(timeoutMs)}={timeoutMs}", GetType().FullName);
            Exception error = null;
            try
            {
                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    Trace.WriteLine($"{nameof(SendWithRetry)}: Attempt {attempt} of {maxRetries}...", GetType().FullName);
                    // Reset signaling state
                    _ = Interlocked.Exchange(ref _firstByteKind, 0);
                    _firstByteEvt.Reset();

                    WritePort(message);

                    // Wait for ACK/NAK/STX (signaled by reader)
                    if (!_firstByteEvt.Wait(timeoutMs))
                    {
                        Trace.WriteLine($"{nameof(SendWithRetry)}: Timeout waiting first byte", GetType().FullName);
                        continue; // retry
                    }

                    byte first = (byte)Interlocked.Exchange(ref _firstByteKind, 0);

                    if (first == NAK)
                    {
                        // Retry same frame if NAK
                        if (attempt == maxRetries)
                            return false;
                        continue;
                    }

                    if (first == ACK || first == STX)
                    {
                        // ACK = terminal accepted; STX = response started immediately
                        OnTerminalCommandAccepted(commandId);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"{nameof(SendWithRetry)}:\n{ex}", GetType().FullName);
                error = ex;
            }

            if (error == null)
                error = new TimeoutException($"{string.Format(StringResources.GetCommonString(StringResources.PatternAllOfNAttemptsFailed), maxRetries)}");
            Trace.WriteLine($"{nameof(SendWithRetry)}:\n{error}", GetType().FullName);
            Task.Run(() => OnProtocolError(error));
            return false;
        }

        /// <summary>
        /// Calculates the LRC (Longitudinal Redundancy Check) for a data block.
        /// - LRC is defined in the Point ECR spec as the XOR of all bytes in the frame
        ///   starting after STX, including the data and ETX, but excluding STX and the LRC itself.
        /// - This is used by both ECR and terminal to validate message integrity.
        /// </summary>
        /// <param name="data">The byte array to calculate LRC over (typically [data + ETX]).</param>
        /// <returns>The computed LRC byte.</returns>
        internal static byte CalculateLRC(byte[] data)
        {
            // XOR all bytes together to produce LRC
            byte result = data.Aggregate((a, b) => (byte)(a ^ b));
            return result;
        }

        private byte[] BuildTerminalMessage(byte[] payload)
        {
            List<byte> result = new List<byte> { STX };
            result.AddRange(payload);
            result.Add(ETX);
            result.Add(CalculateLRC(result.Skip(1).ToArray()));
            return result.ToArray();
        }

        private byte[] BuildTransactionRequestEx2(decimal amount, string transactionId, bool bonusHandled, string authorizationCode)
        {
            string txId = transactionId.Length > 5 ? transactionId.Substring(transactionId.Length - 5) : transactionId.PadLeft(5, '0');
            string authCode = $"{authorizationCode ?? ""}{(char)0x1C}";

            List<string> fields = new List<string>
            {
                "y", // Message ID
                "0", // Type: purchase
                FormatAmount(amount),
                FormatAmount(0),
                txId,
                "0", // Force Online Auth
                "0", // Manual card
                bonusHandled ? "1" : "0", // Bonus handled flag
                authCode.PadRight(7), // 4-6 characters authorizationCode with FCS terminator, no authorizationCode given, if FCS is first
                DateTime.Now.ToString(DateTimeFormatLong), // Timestamp
                _serialNumber.PadLeft(9, '0'),
                "0", // Payment restriction
                "0", // Surcharge handled
                "0", // LookForDOB
                "0", // Flags
                "0", // RFU
                "978", // Currency (EUR)
                DateTime.Now.ToString(DateTimeFormatShort), // Accounting date
                "0", // Accounting sequence
                _ecrNumber.PadLeft(3, '0') // ECR number
            };

            byte[] payload = _encoding.GetBytes(string.Join("", fields));
            Debug.Assert(payload.Length == 80, "Payload must be exactly 80 bytes before framing");

            return BuildTerminalMessage(payload);
        }

        private byte[] BuildTransactionRequest(string type, string transactionId, DateTime timestamp)
        {
            string tsl = timestamp == default ? "000000000000" : timestamp.ToString(DateTimeFormatLong);
            string tss = timestamp == default ? "000000" : timestamp.ToString(DateTimeFormatShort);
            List<string> fields = new List<string>
            {
                "y",
                type,
                FormatAmount(0),
                FormatAmount(0),
                transactionId.PadLeft(5, '0'),
                "0","0","0",
                ("" + (char)0x1C).PadRight(7),
                tsl,
                _serialNumber.PadLeft(9, '0'),
                "0","0","0","0","0",
                "978",
                tss,
                "0",
                _ecrNumber.PadLeft(3, '0')
            };

            byte[] payload = _encoding.GetBytes(string.Join("", fields));
            Debug.Assert(payload.Length == 80, "Payload must be exactly 80 bytes before framing");

            return BuildTerminalMessage(payload);
        }

        private byte[] BuildTransactionRequest(string type, decimal amount)
        {
            DateTime dateTime = DateTime.Now;
            List<string> fields = new List<string>
            {
                "y",
                type,
                FormatAmount(amount),
                FormatAmount(0),
                EmptyTransactionId,
                "0","0","0",
                ("" + (char)0x1C).PadRight(7),
                dateTime.ToString(DateTimeFormatLong),
                _serialNumber.PadLeft(9, '0'),
                "0","0","0","0","0",
                "978",
                dateTime.ToString(DateTimeFormatShort),
                "0",
                _ecrNumber.PadLeft(3, '0')
            };

            byte[] payload = _encoding.GetBytes(string.Join("", fields));
            Debug.Assert(payload.Length == 80, "Payload must be exactly 80 bytes before framing");

            return BuildTerminalMessage(payload);
        }

        internal void SendHandshake()
        {
            Trace.WriteLine($"{nameof(SendHandshake)}", GetType().FullName);
            byte[] request = new byte[] { ENQ };
            SendWithRetry(request, CommandId.TestTerminal);
        }

        private void SendAck()
        {
            byte[] request = new[] { ACK };
            Thread.Sleep(_ackDelayMs);
            WritePort(request);
        }

        private void SendNak()
        {
            byte[] request = new[] { NAK };
            Thread.Sleep(_ackDelayMs);
            WritePort(request);
        }

        private void SendRunPayment(decimal amount, string transactionId, bool bonusHandled, string authorizationCode)
        {
            Trace.WriteLine($"{nameof(SendRunPayment)}:{nameof(amount)}={amount};{nameof(transactionId)}={transactionId};{nameof(bonusHandled)}={bonusHandled};{nameof(authorizationCode)}={authorizationCode}", GetType().FullName);
            byte[] request = BuildTransactionRequestEx2(amount, transactionId, bonusHandled, authorizationCode);
            SendWithRetry(request, CommandId.PurchaseTransaction);
        }

        internal void SendRunPayment(decimal amount, string transactionId, bool bonusHandled)
        {
            Trace.WriteLine($"{nameof(SendRunPayment)}:{nameof(amount)}={amount};{nameof(transactionId)}={transactionId};{nameof(bonusHandled)}={bonusHandled}", GetType().FullName);
            byte[] request = BuildTransactionRequestEx2(amount, transactionId, bonusHandled, string.Empty);
            SendWithRetry(request, CommandId.PurchaseTransaction);
        }

        /// <summary>
        /// Sends a reversal transaction. Must use the same day and exact TransactionId and timestamp.
        /// </summary>
        internal void SendReversal(string transactionId, DateTime originalTimestamp)
        {
            SendExtendedTransaction(TransactionRequestTypes.Reversal, transactionId, originalTimestamp, CommandId.ReversalTransaction);
        }

        /// <summary>
        /// Sends a refund transaction. Not tied to original transaction, but some cards may reject it.
        /// </summary>
        internal void SendRefund(decimal amount)
        {
            SendSimpleTransaction(TransactionRequestTypes.Refund, amount, CommandId.RefundTransaction);
        }

        private void SendSimpleTransaction(string type, decimal amount, string commandId)
        {
            byte[] request = BuildTransactionRequest(type, amount);
            SendWithRetry(request, commandId);
        }

        private void SendExtendedTransaction(string type, string transactionId, DateTime timestamp, string commandId)
        {
            byte[] request = BuildTransactionRequest(type, transactionId, timestamp);
            SendWithRetry(request, commandId);
        }

        /// <summary>
        /// Retrieves a transaction by transactionId and timestamp, or last if transactionId is "00000".
        /// </summary>
        internal void SendRetrieveTransaction(string transactionId, DateTime timestamp)
        {
            SendExtendedTransaction(TransactionRequestTypes.Retrieve, transactionId, timestamp, CommandId.RetrieveTransaction);
        }

        internal void SendManualAuthorization(decimal amount, string transactionId, bool bonusHandled, string authorizationCode)
        {
            SendRunPayment(amount, transactionId, bonusHandled, authorizationCode);
        }

        internal static bool ValidateAuthorizationCode(string authorizationCode, string pattern)
        {
            return Regex.IsMatch(authorizationCode?.Trim() ?? string.Empty, pattern);
        }

        internal void SendAuxiliaryAcceptMode(bool enable)
        {
            Trace.WriteLine($"{nameof(SendAuxiliaryAcceptMode)}:{nameof(enable)}={enable}", GetType().FullName);

            byte[] payload = new[]
            {
                (byte)'S', // DeviceControl message
                (byte)'2',
                (byte)(enable ? '1' : '0') // 21 = set, 20 = reset
            };

            byte[] request = BuildTerminalMessage(payload);

            SendWithRetry(request, enable ? CommandId.EnableAuxiliaryMode : CommandId.DisableAuxiliaryMode);
        }

        internal void SendTransactionAbort()
        {
            Trace.WriteLine($"{nameof(SendTransactionAbort)}", GetType().FullName);
            byte[] payload = new[]
            {
                (byte)'7',
                (byte)'2'
            };

            byte[] request = BuildTerminalMessage(payload);

            SendWithRetry(request, CommandId.AbortTransaction);
        }

        internal void SendTransactionAccept(string transactionId, bool accept)
        {
            Trace.WriteLine($"{nameof(SendTransactionAccept)}:{nameof(transactionId)}={transactionId}, {nameof(accept)}={accept}", GetType().FullName);

            if (string.IsNullOrWhiteSpace(transactionId)
                || transactionId.Length < 4)
            {
                Trace.WriteLine($"{nameof(SendTransactionAccept)}: Invalid TransactionId", GetType().FullName);
                return;
            }

            string payloadStr = $"${transactionId.Substring(0, 5)}{(accept ? "1" : "9")}" + new string('0', 9);
            byte[] request = BuildTerminalMessage(_encoding.GetBytes(payloadStr));

            SendWithRetry(request, CommandId.AcceptTransaction);
        }

        private void SendCustomerCardMode(char activationType)
        {
            Trace.WriteLine($"{nameof(SendCustomerCardMode)}:{nameof(activationType)}={activationType}", GetType().FullName);

            byte[] payload = new byte[]
            {
                (byte)'C',              // MessageID
                (byte)activationType,   // Activation type 0 = stop, 1 = start, 2 = start autoreply 
                (byte)'0',              // 0 = accept all S-group bonus cards, other = undefined 
                (byte)'0',              // RFU
                (byte)'0'               // RFU
            };

            byte[] request = BuildTerminalMessage(payload);

            string commandId = StringResources.NoValueString;
            switch (activationType)
            {
                case '0':
                    commandId = CommandId.DisableBonusCardMode;
                    break;
                case '1':
                case '2':
                    commandId = CommandId.EnableBonusCardMode;
                    break;
            }

            SendWithRetry(request, commandId);
        }

        internal void SendCustomerCardModeStart(bool autoReply)
        {
            char activationType = autoReply ? '2' : '1';
            SendCustomerCardMode(activationType);
        }

        internal void SendCustomerCardModeStop()
        {
            SendCustomerCardMode('0');
        }

        internal void SendCustomerRequest(bool stopActive)
        {
            Trace.WriteLine($"{nameof(SendCustomerRequest)}:{nameof(stopActive)}={stopActive}", GetType().FullName);
            //0 = stop, 1 = remain active, doesn't work for all firmwares, stopActive is ignored and bonus read message is cleared
            byte activationType = (byte)(stopActive ? 0 : 1);
            byte[] payload = new[]
            {
                (byte)'D', // CustomerRequest
                activationType
            };

            byte[] request = BuildTerminalMessage(payload);

            SendWithRetry(request, CommandId.RequestBonusCardInfo);
        }

        internal void SendRequestStatus()
        {
            Trace.WriteLine($"{nameof(SendRequestStatus)}", GetType().FullName);
            byte[] payload = new byte[]
            {
                (byte)'s', // DeviceControl
                (byte)'0', // Control option high nibble
                (byte)'0'  // Control option low nibble (00 = Return Status information)
            };

            byte[] request = BuildTerminalMessage(payload);

            SendWithRetry(request, CommandId.RequestTerminalStatus);
        }

        internal void SendTCSMessageRequest()
        {
            Trace.WriteLine($"{nameof(SendTCSMessageRequest)}", GetType().FullName);
            byte[] payload = new byte[]
            {
                (byte)'s', // DeviceControl
                (byte)'0', // Control option high nibble
                (byte)'1'  // Control option low nibble (01 = Return TCS message, if present)
            };

            byte[] request = BuildTerminalMessage(payload);

            SendWithRetry(request, CommandId.RetrieveTCSMessage);
        }

        internal void SendTerminalVersionRequest()
        {
            Trace.WriteLine($"{nameof(SendTerminalVersionRequest)}", GetType().FullName);
            byte[] payload = new byte[]
            {
                (byte)'s', // DeviceControl
                (byte)'0', // Control option high nibble
                (byte)'2'  // Control option low nibble (02 = Return Terminal application version)
            };

            byte[] request = BuildTerminalMessage(payload);

            SendWithRetry(request, CommandId.RequestTerminalVersion);
        }

        internal void SendDisplayText(string line1, string line2, bool bigFont)
        {
            Trace.WriteLine($"{nameof(SendDisplayText)}: '{line1}' / '{line2}'", GetType().FullName);
            byte[] text1 = _encoding.GetBytes(line1);

            if (text1.Length > 21)
                text1 = text1.Take(21).ToArray();
            else if (text1.Length < 21)
                text1 = text1.Concat(Enumerable.Repeat((byte)' ', 21 - text1.Length)).ToArray();

            byte[] text2 = _encoding.GetBytes(line2);
            if (text1.Length > 21
                || text2.Length > 21)
            {
                bigFont = false; // fallback to small font if any line doesn't fit
            }

            if (text2.Length > 21)
                text2 = text2.Take(21).ToArray();
            else if (text2.Length < 21)
                text2 = text2.Concat(Enumerable.Repeat((byte)' ', 21 - text2.Length)).ToArray();

            byte[] payload = new byte[1 + 1 + 21 + 21 + 4];
            payload[0] = (byte)'Z';
            payload[1] = (byte)(bigFont ? 2 : 1); // display option
            Array.Copy(text1, 0, payload, 2, 21);
            Array.Copy(text2, 0, payload, 23, 21);
            Array.Copy(new byte[] { (byte)' ', (byte)' ', (byte)' ', (byte)' ' }, 0, payload, 44, 4);

            byte[] request = BuildTerminalMessage(payload);

            SendWithRetry(request, CommandId.DisplayText);
        }

        internal void SendClearDisplay()
        {
            Trace.WriteLine($"{nameof(SendClearDisplay)}", GetType().FullName);

            byte[] payload = new byte[1 + 1 + 21 + 21 + 4];
            payload[0] = (byte)'Z';
            payload[1] = 0; // display option = clear
            for (int i = 2; i < payload.Length; i++)
                payload[i] = (byte)' ';

            byte[] request = BuildTerminalMessage(payload);

            SendWithRetry(request, CommandId.ClearDisplayText);
        }
    }
}