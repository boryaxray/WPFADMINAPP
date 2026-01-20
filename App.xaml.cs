using System;
using System.Windows;

namespace WPFAPP
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Обработка непойманных исключений
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                MessageBox.Show(
                    $"Непредвиденная ошибка:\n{args.ExceptionObject}",
                    "Критическая ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            };

            DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show(
                    $"Ошибка в интерфейсе:\n{args.Exception.Message}",
                    "Ошибка интерфейса",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                args.Handled = true;
            };
        }
    }
}