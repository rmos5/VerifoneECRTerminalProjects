using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Verifone.ECRTerminal;
using VerifonePaymentTerminal.Properties;

namespace VerifonePaymentTerminal
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IUserPromptHandler, MainWindow.IPortTraceMessages
    {
        public MainWindow()
        {
            InitializeComponent();
            Transactions = new ObservableCollection<TransactionResultEventArgs>();
            InitPortTrace();
            PortName = Settings.Default.PortName;
            IsBonusAutoReply = Settings.Default.IsBonusAutoReply;
            IsBonusStopActive = Settings.Default.IsBonusStopActive;
            IsDisplayTextBigFont = Settings.Default.IsDisplayTextBigFont;
            if (Settings.Default.DataDirectory?.Length > 1)
                DataDirectory = Settings.Default.DataDirectory;
            else
                DataDirectory = @".\";
            Settings.Default.PropertyChanged += Default_PropertyChanged;
        }

        private void Default_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            try
            {
                Settings.Default.Save();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"{nameof(Default_PropertyChanged)}: {ex.ToString()}", GetType().FullName);
            }
        }

        private static TraceListener _portTraceListener;

        private static readonly object _lock = new object();

        private static IECRTerminalManager _ecrTerminalManager = null;

        private IECRTerminalManager ECRTerminalManager
        {
            get
            {
                lock (_lock)
                {
                    if (_ecrTerminalManager == null
                        || _ecrTerminalManager.IsDisposed)
                    {
                        _ecrTerminalManager = new PaymentTerminalManager(PortName, this, dataDirectoryPath: DataDirectory);
                        AddEcrProtocolEvents(_ecrTerminalManager);
                    }

                    return _ecrTerminalManager;
                }
            }
        }

        public string PortName
        {
            get { return (string)GetValue(PortNameProperty); }
            set { SetValue(PortNameProperty, value); }
        }

        public static readonly DependencyProperty PortNameProperty =
            DependencyProperty.Register("PortName", typeof(string), typeof(MainWindow), new PropertyMetadata(string.Empty, OnPortNameChanged));

        private static void OnPortNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Settings.Default.PortName = e.NewValue as string ?? "";
        }

        public DeviceStatus DeviceStatus
        {
            get { return (DeviceStatus)GetValue(DeviceStatusProperty); }
            private set { SetValue(DeviceStatusProperty, value); }
        }

        public static readonly DependencyProperty DeviceStatusProperty =
            DependencyProperty.Register("DeviceStatus", typeof(DeviceStatus), typeof(MainWindow), new PropertyMetadata(null));

        public string PaymentAmount
        {
            get { return (string)GetValue(PaymentAmountProperty); }
            set { SetValue(PaymentAmountProperty, value); }
        }

        public static readonly DependencyProperty PaymentAmountProperty =
            DependencyProperty.Register("PaymentAmount", typeof(string), typeof(MainWindow), new PropertyMetadata(string.Empty));

        public string TransactionId
        {
            get { return (string)GetValue(TransactionIdProperty); }
            set { SetValue(TransactionIdProperty, value); }
        }

        public static readonly DependencyProperty TransactionIdProperty =
            DependencyProperty.Register("TransactionId", typeof(string), typeof(MainWindow), new PropertyMetadata(string.Empty));

        public DateTime TransactionDateTime
        {
            get { return (DateTime)GetValue(TransactionDateTimeProperty); }
            set { SetValue(TransactionDateTimeProperty, value); }
        }

        public static readonly DependencyProperty TransactionDateTimeProperty =
            DependencyProperty.Register("TransactionDateTime", typeof(DateTime), typeof(MainWindow), new PropertyMetadata(default));

        public bool IsDeclineTransaction
        {
            get { return (bool)GetValue(IsDeclineTransactionProperty); }
            set { SetValue(IsDeclineTransactionProperty, value); }
        }

        public static readonly DependencyProperty IsDeclineTransactionProperty =
            DependencyProperty.Register("IsDeclineTransaction", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

        public bool IsAuxiliaryAuto
        {
            get { return (bool)GetValue(IsAuxiliaryAutoProperty); }
            set { SetValue(IsAuxiliaryAutoProperty, value); }
        }

        public static readonly DependencyProperty IsAuxiliaryAutoProperty =
            DependencyProperty.Register("IsAuxiliaryAuto", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

        public bool IsBonusHandled
        {
            get { return (bool)GetValue(IsBonusHandledProperty); }
            set { SetValue(IsBonusHandledProperty, value); }
        }

        public static readonly DependencyProperty IsBonusHandledProperty =
            DependencyProperty.Register("IsBonusHandled", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

        public bool IsBonusAutoReply
        {
            get { return (bool)GetValue(IsBonusAutoReplyProperty); }
            set { SetValue(IsBonusAutoReplyProperty, value); }
        }

        public static readonly DependencyProperty IsBonusAutoReplyProperty =
            DependencyProperty.Register("IsBonusAutoReply", typeof(bool), typeof(MainWindow), new PropertyMetadata(false, OnIsBonusAutoReplyChanged));

        private static void OnIsBonusAutoReplyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Settings.Default.IsBonusAutoReply = (bool)e.NewValue;
        }

        public bool IsBonusStopActive
        {
            get { return (bool)GetValue(IsBonusStopActiveProperty); }
            set { SetValue(IsBonusStopActiveProperty, value); }
        }

        public static readonly DependencyProperty IsBonusStopActiveProperty =
            DependencyProperty.Register("IsBonusStopActive", typeof(bool), typeof(MainWindow), new PropertyMetadata(false, OnIsBonusStopActiveChanged));

        private static void OnIsBonusStopActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Settings.Default.IsBonusStopActive = (bool)e.NewValue;
        }

        public string DisplayText1
        {
            get { return (string)GetValue(DisplayText1Property); }
            private set { SetValue(DisplayText1Property, value); }
        }

        public static readonly DependencyProperty DisplayText1Property =
            DependencyProperty.Register("DisplayText1", typeof(string), typeof(MainWindow), new PropertyMetadata(string.Empty));

        public string DisplayText2
        {
            get { return (string)GetValue(DisplayText2Property); }
            private set { SetValue(DisplayText2Property, value); }
        }

        public static readonly DependencyProperty DisplayText2Property =
            DependencyProperty.Register("DisplayText2", typeof(string), typeof(MainWindow), new PropertyMetadata(string.Empty));

        public bool IsDisplayTextBigFont
        {
            get { return (bool)GetValue(IsDisplayTextBigFontProperty); }
            set { SetValue(IsDisplayTextBigFontProperty, value); }
        }

        public static readonly DependencyProperty IsDisplayTextBigFontProperty =
            DependencyProperty.Register("IsDisplayTextBigFont", typeof(bool), typeof(MainWindow), new PropertyMetadata(false, OnIsDisplayTextBigFontChanged));

        private static void OnIsDisplayTextBigFontChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Settings.Default.IsDisplayTextBigFont = (bool)e.NewValue;
        }

        public string DataDirectory
        {
            get { return (string)GetValue(DataDirectoryProperty); }
            set { SetValue(DataDirectoryProperty, value); }
        }

        public static readonly DependencyProperty DataDirectoryProperty =
            DependencyProperty.Register("DataDirectory", typeof(string), typeof(MainWindow), new PropertyMetadata(null, OnDataDirectoryChanged));

        private static void OnDataDirectoryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Settings.Default.DataDirectory = e.NewValue as string;
        }

        public ObservableCollection<TransactionResultEventArgs> Transactions
        {
            get { return (ObservableCollection<TransactionResultEventArgs>)GetValue(TransactionsProperty); }
            private set { SetValue(TransactionsProperty, value); }
        }

        public static readonly DependencyProperty TransactionsProperty =
            DependencyProperty.Register("Transactions", typeof(ObservableCollection<TransactionResultEventArgs>), typeof(MainWindow), new PropertyMetadata(null));


        private List<TransactionResultEventArgs> SelectedTransactions { get; } = new List<TransactionResultEventArgs>();


        public TransactionResultEventArgs SelectedTransaction
        {
            get { return (TransactionResultEventArgs)GetValue(SelectedTransactionProperty); }
            set { SetValue(SelectedTransactionProperty, value); }
        }

        public static readonly DependencyProperty SelectedTransactionProperty =
            DependencyProperty.Register("SelectedTransaction", typeof(TransactionResultEventArgs), typeof(MainWindow), new PropertyMetadata(null, OnSelectedTransactionChanged));

        private static void OnSelectedTransactionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            MainWindow obj = (MainWindow)d;
            TransactionResultEventArgs item = e.NewValue as TransactionResultEventArgs;
            if (item != null)
                obj.UpdateTransactionUIProperties(item);
        }

        public string LastPayerReceiptText
        {
            get { return (string)GetValue(LastPayerReceiptTextProperty); }
            private set { SetValue(LastPayerReceiptTextProperty, value); }
        }

        public static readonly DependencyProperty LastPayerReceiptTextProperty =
            DependencyProperty.Register("LastPayerReceiptText", typeof(string), typeof(MainWindow), new PropertyMetadata(string.Empty));

        public string LastPayeeReceiptText
        {
            get { return (string)GetValue(LastPayeeReceiptTextProperty); }
            private set { SetValue(LastPayeeReceiptTextProperty, value); }
        }

        public static readonly DependencyProperty LastPayeeReceiptTextProperty =
            DependencyProperty.Register("LastPayeeReceiptText", typeof(string), typeof(MainWindow), new PropertyMetadata(string.Empty));

        public string LastBonusText
        {
            get { return (string)GetValue(LastBonusTextProperty); }
            private set { SetValue(LastBonusTextProperty, value); }
        }

        public static readonly DependencyProperty LastBonusTextProperty =
            DependencyProperty.Register("LastBonusText", typeof(string), typeof(MainWindow), new PropertyMetadata(string.Empty));

        private bool IsAuxiliaryModeOn => setAuxiliaryAcceptModeCommand.IsChecked == true;

        public ObservableCollection<string> TransactionStatusMessages { get; } = new ObservableCollection<string>();
        public int TransactionStatusMessagesLinesMax { get; set; } = 20;

        public ObservableCollection<string> PortTraceMessages { get; } = new ObservableCollection<string>();
        public int PortTraceMessagesLinesMax { get; set; } = 30;

        private static void RemoveListenersOfType<T>() where T : TraceListener
        {
            for (int i = Trace.Listeners.Count - 1; i >= 0; i--)
            {
                if (Trace.Listeners[i] is T)
                {
                    Trace.Listeners.RemoveAt(i);
                }
            }
        }
        private void InitPortTrace()
        {
            RemoveListenersOfType<FilteredTraceListener>();
            _portTraceListener = new FilteredTraceListener(new[] { "ECR >", "ECR <" }, this);
            Trace.Listeners.Add(_portTraceListener);
            Trace.AutoFlush = true;
        }

        private void AddEcrProtocolEvents(IECRTerminalManager ecrTerminalManager)
        {
            ecrTerminalManager.WakeupECRReceived += OnWakeupECRReceived;
            ecrTerminalManager.TerminalCommandAccepted += OnTerminalCommandAccepted;
            ecrTerminalManager.DeviceControlResultReceived += OnDeviceControlResultReceived;
            ecrTerminalManager.TransactionStatusChanged += OnTransactionStatusChanged;
            ecrTerminalManager.TransactionInitialized += OnTransactionInitialized;
            ecrTerminalManager.TransactionTerminalAbortReceived += OnTransactionTerminalAbortReceived;
            ecrTerminalManager.TransactionResultReceived += OnTransactionResultReceived;
            ecrTerminalManager.PurchaseCreated += OnPurchaseCreated;
            ecrTerminalManager.ReversalCreated += OnReversalCreated;
            ecrTerminalManager.RefundCreated += OnRefundCreated;
            ecrTerminalManager.TransactionRetrieved += OnTransactionRetrieved;
            ecrTerminalManager.BonusResultReceived += OnBonusResultReceived;
            ecrTerminalManager.AbortTransactionResultReceived += OnAbortTransactionResultReceived;
            ecrTerminalManager.TerminalError += OnTerminalError;
        }

        private void RemoveEcrProtocolEvents(IECRTerminalManager ecrTerminalManager)
        {
            ecrTerminalManager.WakeupECRReceived -= OnWakeupECRReceived;
            ecrTerminalManager.TerminalCommandAccepted -= OnTerminalCommandAccepted;
            ecrTerminalManager.DeviceControlResultReceived -= OnDeviceControlResultReceived;
            ecrTerminalManager.TransactionStatusChanged -= OnTransactionStatusChanged;
            ecrTerminalManager.TransactionInitialized -= OnTransactionInitialized;
            ecrTerminalManager.TransactionTerminalAbortReceived -= OnTransactionTerminalAbortReceived;
            ecrTerminalManager.TransactionResultReceived -= OnTransactionResultReceived;
            ecrTerminalManager.PurchaseCreated -= OnPurchaseCreated;
            ecrTerminalManager.ReversalCreated -= OnReversalCreated;
            ecrTerminalManager.RefundCreated -= OnRefundCreated;
            ecrTerminalManager.TransactionRetrieved -= OnTransactionRetrieved;
            ecrTerminalManager.BonusResultReceived -= OnBonusResultReceived;
            ecrTerminalManager.AbortTransactionResultReceived -= OnAbortTransactionResultReceived;
            ecrTerminalManager.TerminalError -= OnTerminalError;
        }

        private void DisposeEcrTerminalManager()
        {
            if (_ecrTerminalManager != null)
            {
                try
                {
                    RemoveEcrProtocolEvents(_ecrTerminalManager);
                    _ecrTerminalManager.Dispose();
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex.ToString());
                }

                _ecrTerminalManager = null;
            }
        }

        private MessageBoxResult ShowMessage(string message, string caption = "Terminal", MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.Information)
        {
            return Dispatcher.Invoke(() => MessageBox.Show(this, message, caption, buttons, icon));
        }

        private void HandleError(Exception error, string title = "Error")
        {
            Dispatcher.Invoke(() => MessageBox.Show(this, error.GetAllMessages(), title, MessageBoxButton.OK, MessageBoxImage.Error));
        }

        private void HandleECRProtocolError(Exception error)
        {
            if (error is ECRTerminalException etx
                && etx.InnerException is IOException)
                ECRTerminalManager.Disconnect();
            HandleError(error);
        }

        public bool ShowUserPromptDialog(string promptMessage)
        {
            return Dispatcher.Invoke(() =>
            {
                UserPromptDialog dialog = new UserPromptDialog(promptMessage);
                dialog.Owner = this;
                dialog.IsEditable = false;
                return dialog.ShowDialog() == true;
            });
        }

        public bool ShowUserPromptDialog(string promptMessage, out string userInput)
        {
            userInput = string.Empty;
            string dialogString = null;
            bool result = Dispatcher.Invoke(() =>
            {
                UserPromptDialog dialog = new UserPromptDialog(promptMessage);
                dialog.Owner = this;
                dialog.IsEditable = true;
                if (dialog.ShowDialog() == true)
                {
                    dialogString = dialog.UserInput;
                    return true;
                }
                else
                {
                    return false;
                }
            });

            if (result)
            {
                userInput = dialogString;
            }

            return result;
        }

        private decimal ParsePaymentAmount()
        {
            if (!decimal.TryParse(PaymentAmount, out decimal paymentAmount))
            {
                string amount = PaymentAmount;
                PaymentAmount = "";
                throw new ArgumentException($"Payment amount must be decimal, received '{amount}'.");
            }

            return paymentAmount;
        }

        private void UpdateTransactionUIProperties(TransactionResultEventArgs e)
        {
            Dispatcher.Invoke(
                () =>
                {
                    PaymentAmount = e.TransactionInfo.Amount.ToString("F2");
                    TransactionDateTime = default; //note: a little trick, sometimes UI don't get updated
                    TransactionDateTime = e.TransactionInfo.TransactionDateTime;
                    TransactionId = e.TransactionInfo.TransactionId;
                    LastPayeeReceiptText = e.TransactionInfo.PayeeReceiptText;
                    LastPayerReceiptText = e.TransactionInfo.PayerReceiptText;
                    if (e.TransactionFilePath?.Length > 0)
                        DataDirectory = Path.GetDirectoryName(e.TransactionFilePath);

                    if (e.HasBonusInfo)
                    {
                        UpdateBonusInfo(e.BonusInfo);
                    }
                });
        }

        private void UpdateBonusInfo(CustomerRequestResultEventArgs e)
        {
            Dispatcher.Invoke(() => LastBonusText = $"Customer number:{e.CustomerNumber} Member class:{e.MemberClass} Status:{e.StatusText} ");
        }

        private async void AddStatusMessage(string message)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                while (TransactionStatusMessages.Count >= TransactionStatusMessagesLinesMax)
                    TransactionStatusMessages.RemoveAt(TransactionStatusMessages.Count - 1);

                TransactionStatusMessages.Insert(0, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}");
            });
        }

        public async void AddPortTraceMessage(string message)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                while (PortTraceMessages.Count >= PortTraceMessagesLinesMax)
                    PortTraceMessages.RemoveAt(PortTraceMessages.Count - 1);

                PortTraceMessages.Insert(0, message);
            });
        }


        private void DeleteTransaction(string transactionFilePath, bool reloadItems)
        {
            try
            {
                Dispatcher.Invoke(
                    () =>
                    {
                        File.Delete(transactionFilePath);
                        if (reloadItems)
                            LoadTransactions();
                    });
            }
            catch (Exception ex)
            {
                HandleECRProtocolError(ex);
            }
        }

        private void OnWakeupECRReceived(object sender, EventArgs e)
        {
            AddStatusMessage("WakeupECR received.");
        }

        private void OnTerminalCommandAccepted(object sender, TerminalCommandAcceptedEventArgs e)
        {
            AddStatusMessage($"Terminal command accepted '{e}'.");
        }

        private async void OnDeviceControlResultReceived(object sender, DeviceControlResultEventArgs e)
        {
            await Dispatcher.InvokeAsync(() => DeviceStatus = e.DeviceStatus);
        }

        private async void OnTransactionStatusChanged(object sender, TransactionStatusEventArgs e)
        {
            AddStatusMessage($"{e.StatusPhase}->{e.StatusPhaseMessage} {e.StatusResultCode}->{e.StatusResultCodeMessage}");

            await Dispatcher.InvokeAsync(() =>
            {
                switch (e.StatusResultCode)
                {
                    case "0000": // Status is OK, no errors
                        break;
                    case "0001": // Bonus card detected (status OK)
                        break;
                    case "0002": // Card read failed, fallback continues
                        break;
                    case "0003": // Blacklist missing or incorrect (inform operator)
                        break;
                    case "0004": // CAPK missing or incorrect (inform operator)
                        break;
                    case "0005": // Date of birth included (YYMMDD in ExtraInfo)
                        break;
                    case "0014": // Using mag.stripe of chip card before chip
                        break;
                    case "0015": // Incorrect PIN given, retry possible
                        break;
                    case "0016": // Authorization authorizationCode checksum error, retry needed
                        break;
                    case "0017": // PIN bypassed
                        break;
                    case "0018": // PIN blocked
                        break;
                    case "0019": // Authorization cancel failed, cashier must call
                        break;
                    case "0020": // Surcharge amount and group in ExtraInfo
                        break;
                    case "0021": // NFC processing failed, fallback to chip
                        break;
                    case "0022": // Offline transaction queue, ExtraInfo = count
                        break;
                    case "0023": // Unknown NFC card, request new card
                        break;
                    case "0024": // Amount over NFC card limit
                        break;

                    // --- 1000–1999: Transaction must stop ---
                    case "1001": // Invalid or unknown card
                        break;
                    case "1002": // Card read failed
                        break;
                    case "1003": // Card removed
                        break;
                    case "1004": // Stop key pressed
                        break;
                    case "1005": // Invalid card
                        break;
                    case "1006": // Card expired
                        break;
                    case "1007": // Card blacklisted (warning in ExtraInfo)
                        break;
                    case "1008": // Original transaction not found
                        break;
                    case "1009": // Reversal/refund not allowed
                        break;
                    case "1010": // Message syntax error (e.g. zero amount)
                        break;
                    case "1012": // Terminal config error
                        break;
                    case "1013": // Timeout (application selection or PIN)
                        break;
                    case "1014": // Magstripe used instead of chip
                        break;
                    case "1015": // Incorrect PIN, last attempt
                        break;
                    case "1016": // App not allowed
                        break;
                    case "1017": // PIN bypass not allowed
                        break;
                    case "1018": // Auth authorizationCode error, abort
                        break;
                    case "1019": // Below application min amount
                        break;
                    case "1020": // Above application max amount
                        break;
                    case "1021": // Service forbidden by app (e.g. cashback)
                        break;
                    case "1022": // Transaction auto-cancelled (missing ACK)
                        break;
                    case "1024": // Card can't be processed, manual fallback
                        break;

                    // --- 1100+ ---
                    case "1100": // No connection to Point
                        break;
                    case "1102": // Preauthorization not found
                        break;
                    case "1103": // Invalid new preauth expiration date
                        break;

                    // --- 2000–2999: Transaction paused, needs ECR confirmation ---
                    case "2001": // Bonus card found, continue with BonusHandled = 1
                        IsBonusHandled = true;
                        LastBonusText = e.Info?.Trim();
                        break;
                    case "2002": // Bonus card only (no payment), abort
                        IsBonusHandled = true;
                        AbortTransaction();
                        break;
                    case "2003": // Manual authorization required
                        break;
                    case "2004": // PIN bypass needs ECR confirmation
                        break;
                    case "2005": // ID check required (manual confirmation)
                        break;
                    case "2006": // Chip read failed, confirm fallback to magstripe
                        break;
                    case "2007": // Swedbank use: enter 4 digits
                        break;
                    case "2008": // Reserved
                        break;
                    case "2012": // PIN blocked, retry with verified customer ID
                        break;
                    case "2022": // Waiting for AcceptTransaction
                        break;

                    // --- 9000–9999: Authorization declined ---
                    case "91Z3": // Declined before online
                        break;
                    case "91Z1": // Card app expired
                        break;
                    case "9400": // Card declined after successful authorization
                        break;
                    default:
                        break;
                }

                switch (e.StatusPhase)
                {
                    case "0": // Waiting for card
                        break;
                    case "1": // Chip card inserted
                        break;
                    case "2": // Waiting for magstripe fallback
                        break;
                    case "3": // Magstripe card read
                        break;
                    case "4": // Manual card number entry
                        break;
                    case "5": // Language selection
                        break;
                    case "6": // Application selection
                        break;
                    case "7": // Cardholder verification (e.g. PIN)
                        break;
                    case "8": // Authorization in progress
                        break;
                    case "9": // Contactless card read
                        break;
                    case "A": // Transaction initialized
                        break;
                    case "B": // Terminal reports blacklist missing
                        break;
                    case "C": // Terminal reports CAPK missing
                        break;
                    case "#": // Preauthorization ID provided
                        break;
                    case "$": // Waiting for AcceptOrDecleanTransaction
                        break;
                    case "Q": // ECR confirmation required (fallback, ID check, etc.)
                        break;
                    case "R": // Transaction complete, waiting for card removal
                        break;
                    default: // Unknown transaction phase
                        break;
                }
            });
        }

        private async void OnTransactionInitialized(object sender, TransactionEventArgs e)
        {
            await Dispatcher.InvokeAsync(
                () =>
                {
                    TransactionId = e.TransactionId;
                    TransactionDateTime = e.TransactionDateTime;
                });
        }

        private void OnTransactionTerminalAbortReceived(object sender, TransactionStatusEventArgs e)
        {
            ShowMessage($"{e.StatusResultCodeMessage}\n{e.Info}", "Terminal", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void HandleTransactionResult(TransactionResultEventArgs e, string message)
        {
            Dispatcher.Invoke(
                async () =>
                {
                    UpdateTransactionUIProperties(e);
                    //note: should show confirmation dialog if auto mode
                    if (IsAuxiliaryModeOn
                        && !IsAuxiliaryAuto)
                        await Task.Run(
                            async () =>
                            {
                                await Task.Delay(200);
                                await Task.Run(RetrieveTerminalStatus);
                            });

                    AddStatusMessage(message);
                    await Task.Run(LoadTransactions);
                });
        }

        private void OnTransactionResultReceived(object sender, TransactionResultEventArgs e)
        {
            HandleTransactionResult(e, $"Tapahtuma {TransactionTypes.GetTypeString(e.TransactionInfo.TransactionType)} {e.TransactionInfo.TransactionId} {e.TransactionInfo.Amount:C}.");
        }

        private void OnPurchaseCreated(object sender, TransactionResultEventArgs e)
        {
            HandleTransactionResult(e, $"Maksu suoritettu {e.TransactionInfo.TransactionId} {e.TransactionInfo.Amount:C}.");
        }

        private void OnReversalCreated(object sender, TransactionResultEventArgs e)
        {
            HandleTransactionResult(e, $"Maksu peruutettu {e.TransactionInfo.TransactionId} {e.TransactionInfo.Amount:C}.");
        }

        private void OnRefundCreated(object sender, TransactionResultEventArgs e)
        {
            HandleTransactionResult(e, $"Maksu hyvitetty {e.TransactionInfo.TransactionId} {e.TransactionInfo.Amount:C}.");
        }

        private void OnTransactionRetrieved(object sender, TransactionResultEventArgs e)
        {
            HandleTransactionResult(e, $"Tapahtuma haettu {TransactionTypes.GetTypeString(e.TransactionInfo.TransactionType)} {e.TransactionInfo.TransactionId} {e.TransactionInfo.Amount:C}.");
        }

        private async void OnBonusResultReceived(object sender, CustomerRequestResultEventArgs e)
        {
            await Dispatcher.InvokeAsync(
                () =>
                {
                    try
                    {
                        UpdateBonusInfo(e);
                        //_ecrTerminalManager.DisableBonusCardMode();
                        IsBonusHandled = true;
                    }
                    catch (Exception ex)
                    {
                        HandleECRProtocolError(ex);
                    }
                });
        }

        private void OnAbortTransactionResultReceived(object sender, AbortTransactionResultEventArgs e)
        {
            AddStatusMessage(e.Message);
            if (!e.IsAborted)
                ShowMessage(e.Message, icon: MessageBoxImage.Warning);
        }

        private void OnTerminalError(object sender, ExceptionEventArgs e)
        {
            HandleECRProtocolError(e.Exception);
        }

        private void refundPaymentCommand_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                decimal paymentAmount = ParsePaymentAmount();
                ECRTerminalManager.Refund(paymentAmount);
            }
            catch (Exception ex)
            {
                HandleECRProtocolError(ex);
            }
        }

        private void reverseTransactionCommand_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ECRTerminalManager.Reversal(TransactionId, TransactionDateTime);
            }
            catch (Exception ex)
            {
                HandleECRProtocolError(ex);
            }
        }

        private void RunNewPayment()
        {
            try
            {
                decimal paymentAmount = ParsePaymentAmount();
                RunPayment(paymentAmount, IsBonusHandled);
            }
            catch (Exception ex)
            {
                HandleECRProtocolError(ex);
            }
        }

        private void RunPayment(decimal paymentAmount, bool isBonusHandled)
        {
            try
            {
                ECRTerminalManager.RunPayment(paymentAmount, isBonusHandled);
            }
            catch (Exception ex)
            {
                HandleECRProtocolError(ex);
            }
        }

        private void runPaymentCommand_Click(object sender, RoutedEventArgs e)
        {
            RunNewPayment();
        }

        private void abortTransactionCommand_Click(object sender, RoutedEventArgs e)
        {
            AbortTransaction();
        }

        private void AbortTransaction()
        {
            try
            {
                ECRTerminalManager.AbortTransaction();
            }
            catch (Exception ex)
            {
                HandleECRProtocolError(ex);
            }
        }

        private void disconnectCommand_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ECRTerminalManager.Disconnect();
                DisposeEcrTerminalManager();
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        private void testTerminalReadyCommand_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ECRTerminalManager.TestTerminal();
            }
            catch (Exception ex)
            {
                HandleECRProtocolError(ex);
            }
        }

        private void clearDisplayCommand_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ECRTerminalManager.ClearDisplayText();
            }
            catch (Exception ex)
            {
                HandleECRProtocolError(ex);
            }
        }

        private void displayTextCommand_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ECRTerminalManager.DisplayText(DisplayText1, DisplayText2, IsDisplayTextBigFont);
            }
            catch (Exception ex)
            {
                HandleECRProtocolError(ex);
            }
        }

        private void activateCustomerCardModeCommand_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ECRTerminalManager.EnableBonusCardMode(IsBonusAutoReply);
            }
            catch (Exception ex)
            {
                HandleECRProtocolError(ex);
            }
        }


        private void deactivateCustomerCardModeCommand_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ECRTerminalManager.DisableBonusCardMode();
            }
            catch (Exception ex)
            {
                HandleECRProtocolError(ex);
            }
        }

        private void sendCustomerRequestCommand_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ECRTerminalManager.RequestBonusCardInfo(IsBonusStopActive);
            }
            catch (Exception ex)
            {
                HandleECRProtocolError(ex);
            }
        }

        private void RetrieveTerminalStatus()
        {
            ECRTerminalManager.RequestTerminalStatus();
        }

        private void getStatusCommand_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                RetrieveTerminalStatus();
            }
            catch (Exception ex)
            {
                HandleECRProtocolError(ex);
            }
        }

        private void getTCSMessageCommand_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ECRTerminalManager.RetrieveTCSMessage();
            }
            catch (Exception ex)
            {
                HandleECRProtocolError(ex);
            }
        }

        private void getTerminalVersionCommand_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ECRTerminalManager.RequestTerminalVersion();
            }
            catch (Exception ex)
            {
                HandleECRProtocolError(ex);
            }
        }

        private void AddSetAuxiliaryAcceptModeCommandEvents()
        {
            setAuxiliaryAcceptModeCommand.Checked += setAuxiliaryAcceptModeCommand_Checked;
            setAuxiliaryAcceptModeCommand.Unchecked += setAuxiliaryAcceptModeCommand_Unchecked;
        }

        private void RemoveSetAuxiliaryAcceptModeCommandEvents()
        {
            setAuxiliaryAcceptModeCommand.Checked -= setAuxiliaryAcceptModeCommand_Checked;
            setAuxiliaryAcceptModeCommand.Unchecked -= setAuxiliaryAcceptModeCommand_Unchecked;
        }

        private void setAuxiliaryAcceptModeCommand_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                RemoveSetAuxiliaryAcceptModeCommandEvents();
                ECRTerminalManager.EnableAuxiliaryMode();
            }
            catch (Exception ex)
            {
                setAuxiliaryAcceptModeCommand.IsChecked = false;
                HandleECRProtocolError(ex);
            }
            finally
            {
                AddSetAuxiliaryAcceptModeCommandEvents();
            }
        }

        private void setAuxiliaryAcceptModeCommand_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                RemoveSetAuxiliaryAcceptModeCommandEvents();
                ECRTerminalManager.DisableAuxiliaryMode();
            }
            catch (Exception ex)
            {

                setAuxiliaryAcceptModeCommand.IsChecked = true;
                HandleECRProtocolError(ex);
            }
            finally
            {
                AddSetAuxiliaryAcceptModeCommandEvents();
            }
        }

        private void AcceptOrDecleanTransaction(string transactionId, bool accept)
        {
            try
            {
                if (accept)
                    ECRTerminalManager.AcceptTransaction(transactionId);
                else
                    ECRTerminalManager.RejectTransaction(transactionId);
            }
            catch (Exception ex)
            {
                HandleECRProtocolError(ex);
            }
        }

        private void acceptTransactionCommand_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AcceptOrDecleanTransaction(TransactionId, !IsDeclineTransaction);
            }
            catch (Exception ex)
            {
                HandleECRProtocolError(ex);
            }
        }
        private void retrieveTransactionCommand_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ECRTerminalManager.RetrieveTransaction(TransactionId, TransactionDateTime);
            }
            catch (Exception ex)
            {
                HandleECRProtocolError(ex);
            }
        }

        private void LoadTransactions()
        {
            try
            {
                Dispatcher.Invoke(
                    () =>
                    {
                        Transactions.Clear();
                        foreach (TransactionResultEventArgs obj in ECRTerminalManager.GetTransactions().OrderByDescending(o => o.TransactionInfo.TransactionDateTime))
                            Transactions.Add(obj);
                    });
            }
            catch (Exception ex)
            {
                HandleECRProtocolError(ex);
            }
        }

        private void loadTransactionsCommand_Click(object sender, RoutedEventArgs e)
        {
            LoadTransactions();
        }

        private void deleteTransactionsCommand_Click(object sender, RoutedEventArgs e)
        {
            foreach (TransactionResultEventArgs obj in SelectedTransactions)
            {
                DeleteTransaction(obj.TransactionFilePath, false);
            }

            if (SelectedTransactions.Count > 0)
            {
                SelectedTransactions.Clear();
                LoadTransactions();
            }
        }

        private async void transactionsSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await Dispatcher.InvokeAsync(
                () =>
                {
                    // Remove deselected items
                    foreach (var item in e.RemovedItems)
                    {
                        SelectedTransactions.Remove((TransactionResultEventArgs)item);
                    }

                    // Add newly selected items
                    foreach (var item in e.AddedItems)
                    {
                        SelectedTransactions.Add((TransactionResultEventArgs)item);
                    }
                });
        }
    }
}