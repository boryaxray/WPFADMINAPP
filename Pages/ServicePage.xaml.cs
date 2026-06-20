using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Text;
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

        // Ключ для сохранения статуса установки службы
        private const string SERVICE_INSTALLED_KEY = "ServiceInstalled";

        public ServicePage()
        {
            InitializeComponent();

            _statusTimer = new DispatcherTimer();
            _statusTimer.Interval = TimeSpan.FromSeconds(3);
            _statusTimer.Tick += (s, e) => LoadStatus();
            _statusTimer.Start();

            LoadPaths(); // Загружаем сохраненные пути
            LoadStatus();
        }

        private void SetServiceInstalledStatus(bool isInstalled)
        {
            try
            {
                Properties.Settings.Default.ServiceInstalled = isInstalled;
                Properties.Settings.Default.Save();
                Debug.WriteLine($"Статус установки службы сохранен: {(isInstalled ? "Установлена" : "Не установлена")}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка сохранения статуса установки: {ex.Message}");
            }
        }

        private bool GetSavedServiceInstalledStatus()
        {
            try
            {
                return Properties.Settings.Default.ServiceInstalled;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка чтения статуса установки: {ex.Message}");
                return false;
            }
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

        private async void LoadStatus()
        {
            try
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    // Проверяем, установлена ли служба
                    bool isInstalled = ServiceManager.IsServiceInstalled();

                    // Проверяем наличие процесса
                    bool processExists = Process.GetProcessesByName("ApplicationControlService").Length > 0;

                    if (!isInstalled)
                    {
                        if (processExists)
                        {
                            // Есть процесс, но служба не зарегистрирована
                            StatusText.Text = "Статус: Запущен (процесс без службы)";
                            StatusIcon.Fill = Brushes.Orange;
                            SetServiceInstalledStatus(false);
                        }
                        else
                        {
                            StatusText.Text = "Статус: Не установлена";
                            StatusIcon.Fill = Brushes.Red;
                            SetServiceInstalledStatus(false);
                        }
                        return;
                    }

                    // Служба установлена - получаем её статус
                    string status = ServiceManager.GetServiceStatus();

                    if (status == "Running")
                    {
                        StatusText.Text = "Статус: работает";
                        StatusIcon.Fill = Brushes.Green;
                        SetServiceInstalledStatus(true);
                    }
                    else if (status == "Stopped" || status == "StopPending")
                    {
                        StatusText.Text = "Статус: Работает";
                        StatusIcon.Fill = Brushes.Green;
                        SetServiceInstalledStatus(true);
                    }
                    else if (status.Contains("orphan"))
                    {
                        StatusText.Text = "Статус: Работает";
                        StatusIcon.Fill = Brushes.Green;
                        SetServiceInstalledStatus(false);
                    }
                    else
                    {
                        StatusText.Text = $"Статус: {status}";
                        StatusIcon.Fill = Brushes.Gray;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadStatus error: {ex.Message}");
                await Dispatcher.InvokeAsync(() =>
                {
                    bool savedStatus = GetSavedServiceInstalledStatus();
                    if (savedStatus)
                    {
                        StatusText.Text = "Статус: Установлена (ошибка подключения)";
                        StatusIcon.Fill = Brushes.Orange;
                    }
                    else
                    {
                        StatusText.Text = "Статус: Не установлена";
                        StatusIcon.Fill = Brushes.Red;
                    }
                });
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

                        // УСПЕШНАЯ УСТАНОВКА - СОХРАНЯЕМ СТАТУС
                        SetServiceInstalledStatus(true);

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
                    await System.Threading.Tasks.Task.Delay(5000);
                    LoadStatus();
                    ConfigureRecoveryViaRegistry();

                    // Проверяем настройки восстановления
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
                }
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
                string servicePath = @"SYSTEM\CurrentControlSet\Services\ApplicationControlService";

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
                "УДАЛЕНИЕ СЛУЖБЫ\n\n" +
                "Процесс службы будет принудительно завершен.\n" +
                "Продолжить?",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm == MessageBoxResult.Yes)
            {
                try
                {
                    UninstallBtn.Content = "Удаление...";
                    UninstallBtn.IsEnabled = false;

                    // Показываем прогресс
                    StatusText.Text = "Статус: Остановка службы...";
                    StatusIcon.Fill = Brushes.Orange;

                    await Task.Run(() => ServiceManager.UninstallService());

                    // УСПЕШНОЕ УДАЛЕНИЕ - СОХРАНЯЕМ СТАТУС
                    SetServiceInstalledStatus(false);

                    // Проверяем результат
                    await Task.Delay(2000);
                    bool processStillRunning = await Task.Run(() => ServiceManager.IsServiceProcessRunning());

                    if (processStillRunning)
                    {
                        ForceKillServiceProcess();
                        MessageBox.Show("Служба удалена");
                    }
                    else
                    {
                        MessageBox.Show("Служба успешно удалена", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }

                    // Обновляем статус в UI
                    LoadStatus();
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
        }

        public static void ForceKillServiceProcess()
        {
            try
            {
                // Используем taskkill с флагом /F для принудительного завершения
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = "taskkill.exe";
                    process.StartInfo.Arguments = "/F /IM ApplicationControlService.exe /T";
                    process.StartInfo.UseShellExecute = true;
                    process.StartInfo.Verb = "runas";
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();
                    process.WaitForExit(10000);
                    Debug.WriteLine("taskkill выполнен");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"taskkill error: {ex.Message}");
            }
        }

        private void RunScCommand(string arguments)
        {
            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = "sc.exe";
                    process.StartInfo.Arguments = arguments;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.Start();
                    process.WaitForExit(10000);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SC ошибка: {ex.Message}");
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