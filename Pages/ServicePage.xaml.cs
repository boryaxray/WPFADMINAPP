using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WPFAPP.Managers;

namespace WPFAPP.Pages
{
    public partial class ServicePage : Page
    {
        private string _logsPath = null;
        private string _whiteListPath = null;
        private DispatcherTimer _statusTimer;


        public ServicePage()
        {
            InitializeComponent();

            _statusTimer = new DispatcherTimer();
            _statusTimer.Interval = TimeSpan.FromSeconds(3);
            _statusTimer.Tick += (s, e) => LoadStatus();
            _statusTimer.Start();

            LoadStatus();
            LoadPaths(); // Загружаем сохраненные пути
        }
        private void LoadPaths()
        {
            try
            {
                // Загружаем пути из настроек или используем по умолчанию
                string logsPath = Properties.Settings.Default.LogsPath;
                string whiteListPath = Properties.Settings.Default.WhiteListPath;

                // Проверяем что пути существуют
                if (string.IsNullOrEmpty(logsPath) || !Directory.Exists(Path.GetDirectoryName(logsPath)))
                {
                    logsPath = @"C:\ProgramData\AppControl\Logs";
                    Properties.Settings.Default.LogsPath = logsPath;
                }

                if (string.IsNullOrEmpty(whiteListPath) || !Directory.Exists(Path.GetDirectoryName(whiteListPath)))
                {
                    whiteListPath = @"C:\ProgramData\AppControl\WhiteList";
                    Properties.Settings.Default.WhiteListPath = whiteListPath;
                }

                Properties.Settings.Default.Save();

                // Устанавливаем в TextBox
                LogsPathTextBox.Text = logsPath;
                WhiteListPathTextBox.Text = whiteListPath;

                _logsPath = logsPath;
                _whiteListPath = whiteListPath;
            }
            catch
            {
                // Значения по умолчанию
                LogsPathTextBox.Text = @"C:\ProgramData\AppControl\Logs";
                WhiteListPathTextBox.Text = @"C:\ProgramData\AppControl\WhiteList";
            }
        }

        private void LoadStatus()
        {
            try
            {
                string status = ServiceManager.GetServiceStatus();
                StatusText.Text = $"Статус: {status}";

                switch (status)
                {
                    case "Running":
                        StatusIcon.Fill = Brushes.Green;
                        break;
                    case "Stopped":
                        StatusIcon.Fill = Brushes.Orange;
                        break;
                    case "Not Installed":
                        StatusIcon.Fill = Brushes.Red;
                        break;
                    default:
                        StatusIcon.Fill = Brushes.Gray;
                        break;
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Ошибка: {ex.Message}";
                StatusIcon.Fill = Brushes.Red;
            }
        }

        private async void InstallBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Берем пути из TextBox
                _logsPath = LogsPathTextBox.Text.Trim();
                _whiteListPath = WhiteListPathTextBox.Text.Trim();

                if (string.IsNullOrEmpty(_logsPath) || string.IsNullOrEmpty(_whiteListPath))
                {
                    MessageBox.Show("Укажите оба пути", "Внимание",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Сохраняем в настройки админ-утилиты
                Properties.Settings.Default.LogsPath = _logsPath;
                Properties.Settings.Default.WhiteListPath = _whiteListPath;
                Properties.Settings.Default.Save();

                MessageBoxResult result = MessageBox.Show(
                    $"Установить службу с путями:\n\n" +
                    $"Логи: {_logsPath}\n" +
                    $"Белый список: {_whiteListPath}\n\n" +
                    $"Появится запрос прав администратора.",
                    "Подтверждение установки",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    InstallBtn.Content = "Установка...";
                    InstallBtn.IsEnabled = false;

                    try
                    {
                        await System.Threading.Tasks.Task.Run(() =>
                        {
                            ServiceManager.InstallService(_logsPath, _whiteListPath);
                        });

                        await System.Threading.Tasks.Task.Delay(5000);
                        LoadStatus();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка установки: {ex.Message}", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    finally
                    {
                        InstallBtn.Content = "Установить";
                        InstallBtn.IsEnabled = true;
                    }
                }
                await System.Threading.Tasks.Task.Run(() =>
                {
                    ServiceManager.InstallService(_logsPath, _whiteListPath);
                });

                await System.Threading.Tasks.Task.Delay(5000);
                LoadStatus();

                // ДОБАВЛЯЕМ: Проверяем настройки восстановления
                await System.Threading.Tasks.Task.Delay(2000);
                string recoveryInfo = ServiceManager.GetServiceRecoveryInfo();

                if (recoveryInfo.Contains("restart/5000"))
                {
                    MessageBox.Show(
                        "Служба успешно установлена!\n\nСамовосстановление настроено:\n" +
                        "• 3 попытки перезапуска при сбое\n" +
                        "• Интервал: 5 секунд",
                        "Успех",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                ConfigureRecoveryViaRegistry();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                InstallBtn.Content = "Установить";
                InstallBtn.IsEnabled = true;
            }
        }

        private static void ConfigureRecoveryViaRegistry()
        {
            try
            {
                string servicePath = @"SYSTEM\CurrentControlSet\Services\AppControlService";

                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(servicePath, true))
                {
                    if (key != null)
                    {
                        // Настройка действий при сбое
                        // 3 попытки перезапуска с интервалом 1000 мс (1 секунда)
                        byte[] failureActions = new byte[]
                        {
                    0x00, 0x00, 0x00, 0x00, // Reset period (0 = never)
                    0x00, 0x00, 0x00, 0x00, // Reboot message (unused)
                    0x03, 0x00, 0x00, 0x00, // 3 actions
                    0x01, 0x00, 0x00, 0x00, // Action 1: SC_ACTION_RESTART
                    0xE8, 0x03, 0x00, 0x00, // Delay: 1000 ms (0x3E8)
                    0x01, 0x00, 0x00, 0x00, // Action 2: SC_ACTION_RESTART
                    0xE8, 0x03, 0x00, 0x00, // Delay: 1000 ms
                    0x01, 0x00, 0x00, 0x00, // Action 3: SC_ACTION_RESTART
                    0xE8, 0x03, 0x00, 0x00  // Delay: 1000 ms
                        };

                        key.SetValue("FailureActions", failureActions, Microsoft.Win32.RegistryValueKind.Binary);
                        key.SetValue("FailureActionsOnNonCrashFailures", 1, Microsoft.Win32.RegistryValueKind.DWord);

                        Console.WriteLine("✓ Настройки восстановления записаны в реестр");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка записи в реестр: {ex.Message}");
            }
        }

        private async void UninstallBtn_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult confirm = MessageBox.Show(
                "Удалить службу?\n\nДля удаления требуются права администратора.",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                try
                {
                    // Показываем прогресс
                    UninstallBtn.Content = "Удаление...";
                    UninstallBtn.IsEnabled = false;

                    try
                    {
                        // Запускаем в фоновом потоке
                        await System.Threading.Tasks.Task.Run(() =>
                        {
                            ServiceManager.UninstallService();
                        });

                        // Ждем немного и обновляем статус
                        await System.Threading.Tasks.Task.Delay(3000);
                        LoadStatus();

                        MessageBox.Show("Команда удаления отправлена.",
                            "Информация",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    finally
                    {
                        UninstallBtn.Content = "Удалить";
                        UninstallBtn.IsEnabled = true;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    UninstallBtn.Content = "Удалить";
                    UninstallBtn.IsEnabled = true;
                }
            }
        }

        private void DebugBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logsPath = LogsPathTextBox.Text;
                _whiteListPath = WhiteListPathTextBox.Text;

                ServiceManager.StartDebugMode(_logsPath, _whiteListPath);
                MessageBox.Show("Режим отладки запущен в отдельном окне.",
                    "Информация",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void BrowseLogsPathBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OpenFileDialog();
                dialog.Title = "Выберите папку для хранения логов";
                dialog.Filter = "Папка|.";
                dialog.FileName = "Выбрать папку";
                dialog.CheckFileExists = false;
                dialog.CheckPathExists = true;
                dialog.ValidateNames = false;

                if (dialog.ShowDialog() == true)
                {
                    // Получаем директорию из выбранного пути
                    string selectedPath = Path.GetDirectoryName(dialog.FileName);
                    if (!string.IsNullOrEmpty(selectedPath))
                    {
                        _logsPath = selectedPath;
                        LogsPathTextBox.Text = _logsPath;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BrowseWhiteListPathBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OpenFileDialog();
                dialog.Title = "Выберите папку для хранения белого списка";
                dialog.Filter = "Папка|.";
                dialog.FileName = "Выбрать папку";
                dialog.CheckFileExists = false;
                dialog.CheckPathExists = true;
                dialog.ValidateNames = false;

                if (dialog.ShowDialog() == true)
                {
                    // Получаем директорию из выбранного пути
                    string selectedPath = Path.GetDirectoryName(dialog.FileName);
                    if (!string.IsNullOrEmpty(selectedPath))
                    {
                        _whiteListPath = selectedPath;
                        WhiteListPathTextBox.Text = _whiteListPath;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyLogsPathBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logsPath = LogsPathTextBox.Text;
                if (!string.IsNullOrEmpty(_logsPath))
                {
                    Properties.Settings.Default.LogsPath = _logsPath;
                    Properties.Settings.Default.Save();

                    MessageBox.Show("Путь для логов сохранен: " + _logsPath, "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyWhiteListPathBtn_Click(object sender, RoutedEventArgs e)
        {
            /*            try
                        {
                            _whiteListPath = WhiteListPathTextBox.Text.Trim();

                            if (string.IsNullOrEmpty(_whiteListPath))
                            {
                                MessageBox.Show("Укажите путь для белого списка", "Ошибка",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                                return;
                            }

                            // Создаем директорию если не существует
                            if (!Directory.Exists(_whiteListPath))
                            {
                                try
                                {
                                    Directory.CreateDirectory(_whiteListPath);
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show($"Не удалось создать директорию: {ex.Message}", "Ошибка",
                                        MessageBoxButton.OK, MessageBoxImage.Error);
                                    return;
                                }
                            }

                            // Сохраняем в настройки
                            Properties.Settings.Default.WhiteListPath = _whiteListPath;
                            Properties.Settings.Default.Save();

                            // Обновляем путь в WhiteListManager
                            WhiteListManager.SetConfigPath(_whiteListPath);

                            MessageBox.Show($"Путь для белого списка сохранен:\n{_whiteListPath}", "Успех",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }*/
            try
            {
                _whiteListPath = WhiteListPathTextBox.Text.Trim();

                if (string.IsNullOrEmpty(_whiteListPath))
                {
                    MessageBox.Show("Укажите путь для белого списка", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Создаем директорию если не существует
                if (!Directory.Exists(_whiteListPath))
                {
                    try
                    {
                        Directory.CreateDirectory(_whiteListPath);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Не удалось создать директорию: {ex.Message}", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                // Сохраняем в настройки
                Properties.Settings.Default.WhiteListPath = _whiteListPath;
                Properties.Settings.Default.Save();

                // Обновляем путь в WhiteListManager
                WhiteListManager.SetConfigPath(_whiteListPath);

                // Создаем конфиг и добавляем админ-утилиту
                string configPath = Path.Combine(_whiteListPath, "config.json");
                if (!File.Exists(configPath))
                {
                    // Создаем пустой конфиг
                    WhiteListManager.SaveConfig(new List<WhiteListItem>());
                }

                // Добавляем админ-утилиту в белый список
                AddAdminUtilityToWhiteList(_whiteListPath);

                MessageBox.Show($"Путь для белого списка сохранен:\n{_whiteListPath}", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void AddAdminUtilityToWhiteList(string whiteListPath)
        {
            try
            {
                string adminUtilPath = Process.GetCurrentProcess().MainModule.FileName;
                WhiteListManager.SetConfigPath(whiteListPath);

                var result = WhiteListManager.AddApplication(adminUtilPath);
                if (result.Success)
                {
                    Debug.WriteLine("Админ-утилита добавлена в белый список при смене пути");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка добавления админ-утилиты: {ex.Message}");
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _statusTimer?.Stop();
        }
    }
}