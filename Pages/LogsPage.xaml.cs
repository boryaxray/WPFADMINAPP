using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using WPFAPP.Managers;

namespace WPFAPP.Pages
{
    public partial class LogsPage : Page
    {
        private string _logPath = @"C:\ProgramData\AppControl\";

        public LogsPage()
        {
            InitializeComponent();
            UpdateLogPath();
        }

        private void UpdateLogPath()
        {
            // Можно обновить путь, если был выбран другой каталог
            _logPath = Path.Combine(LogReader.GetLogDirectory(), "detailed.log");

            // Обновляем TextBlock с путем к логам
            var textBlock = this.FindName("LogPathText") as TextBlock;
            if (textBlock != null)
            {
                textBlock.Text = _logPath;
            }
        }

        private void OpenLogsBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogReader.OpenLogsInNotepad();
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
                    LogReader.ClearLogs();
                    MessageBox.Show("Логи очищены", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}