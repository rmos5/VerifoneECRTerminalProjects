using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace VerifonePaymentTerminal
{
    public partial class App : Application
    {
        public App()
        {
            CultureInfo cultureInfo = new CultureInfo("fi-FI");
            Thread.CurrentThread.CurrentCulture = cultureInfo;
            Thread.CurrentThread.CurrentUICulture = cultureInfo;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            FrameworkElement.LanguageProperty.OverrideMetadata(
               typeof(FrameworkElement),
               new FrameworkPropertyMetadata(
                   System.Windows.Markup.XmlLanguage.GetLanguage("fi-FI")));

            // UI thread exceptions
            DispatcherUnhandledException += App_DispatcherUnhandledException;

            // Non-UI thread exceptions
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // Unobserved task exceptions
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            HandleError(e.Exception);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                HandleError(ex);
            }
            else
            {
                HandleError(new Exception("Unknown unhandled exception"));
            }
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            HandleError(e.Exception);
            e.SetObserved();
        }

        private void HandleError(Exception error)
        {
            Dispatcher.Invoke(
                () =>
                {
                    if (MainWindow == null)
                        throw error;
                    MessageBox.Show($"{error.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
        }
    }
}
