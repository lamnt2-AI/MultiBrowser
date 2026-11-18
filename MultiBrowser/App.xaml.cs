using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace MultiBrowser
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogException("DispatcherUnhandledException", e.Exception);
            MessageBox.Show($"Đã xảy ra lỗi:\n{e.Exception.Message}\n\nChi tiết:\n{e.Exception.StackTrace}", "Lỗi ứng dụng", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogException("CurrentDomain_UnhandledException", ex);
                MessageBox.Show($"Lỗi nghiêm trọng:\n{ex.Message}", "Lỗi ứng dụng", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void LogException(string category, Exception ex)
        {
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_error.log");
                string logContent = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{category}] {ex}\n" +
                                   $"--------------------------------------------------\n";
                File.AppendAllText(logPath, logContent);
            }
            catch
            {
                // Tránh throw exception khi đang xử lý unhandled exception
            }
        }
    }
}
