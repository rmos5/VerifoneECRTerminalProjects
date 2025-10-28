using System.Windows;

namespace VerifonePaymentTerminal
{
    public partial class UserPromptDialog : Window
    {
        public string UserInput { get; private set; }

        public bool IsEditable
        {
            get { return (bool)GetValue(IsReadOnlyProperty); }
            set { SetValue(IsReadOnlyProperty, value); }
        }

        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register("IsEditable", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

        public UserPromptDialog(string prompt)
        {
            InitializeComponent();
            promptLabel.Content = prompt;
            promptInput.Focus();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            UserInput = promptInput.Text;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
