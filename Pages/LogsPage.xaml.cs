using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using WPFAPP.Managers;

namespace WPFAPP.Pages
{
    public partial class LogsPage : Page
    {
        public LogsPage()
        {
            InitializeComponent();
            UpdateLogPath();
        }

        private void UpdateLogPath()
        {
            try
            {
                string logDir = ServiceManager.GetLogDirectory();
                string detailedLogPath = Path.Combine(logDir, "detailed.log");

                var textBlock = this.FindName("LogPathText") as TextBlock;
                if (textBlock != null)
                {
                    textBlock.Text = detailedLogPath;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateLogPath error: {ex.Message}");
            }
        }

        private void OpenLogsBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                bool success = ServiceManager.OpenLogsInNotepad();
                if (!success)
                {
                    MessageBox.Show("Не удалось открыть логи. Возможно, директория логов недоступна.",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearLogsBtn_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Очистить все логи? Это действие нельзя отменить.",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    ServiceManager.ClearAllLogs();
                    MessageBox.Show("Логи очищены", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка очистки логов: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}