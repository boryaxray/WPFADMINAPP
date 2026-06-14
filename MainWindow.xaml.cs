using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WPFAPP.Managers;
using WPFAPP.Pages;
using WPFAPP.Utils;

namespace WPFAPP
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer _statusTimer;
        private bool _adminUtilityAdded = false;

        public MainWindow()
        {
            System.Diagnostics.PresentationTraceSources.DataBindingSource.Switch.Level = System.Diagnostics.SourceLevels.Critical;
            InitializeComponent();
            LoadWhiteListPage();
            InitializeStatusTimer();
            UpdateServiceStatus();
            this.Loaded += MainWindow_Loaded;

        }

        private void InitializeStatusTimer()
        {
            _statusTimer = new DispatcherTimer();
            _statusTimer.Interval = TimeSpan.FromSeconds(2); // Каждые 2 секунды
            _statusTimer.Tick += (s, e) => UpdateServiceStatus();
            _statusTimer.Start();

            UpdateServiceStatus();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Добавляем админ-утилиту в белый список
            await AddAdminUtilityToWhiteList();
        }

        private async Task AddAdminUtilityToWhiteList()
        {
            try
            {
                string adminUtilPath = Process.GetCurrentProcess().MainModule.FileName;

                // Проверяем, есть ли уже в белом списке
                var whiteListItems = WhiteListManager.LoadFromConfig();
                string adminHash = HashUtils.CalculateSHA256(adminUtilPath);

                if (!whiteListItems.Any(item => item.Hash.Equals(adminHash, StringComparison.OrdinalIgnoreCase)))
                {
                    var result = WhiteListManager.AddApplication(adminUtilPath);
                    if (result.Success)
                    {
                        _adminUtilityAdded = true;
                        Debug.WriteLine("Админ-утилита добавлена в белый список");
                    }
                }
                else
                {
                    _adminUtilityAdded = true;
                    Debug.WriteLine("Админ-утилита уже в белом списке");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка добавления админ-утилиты: {ex.Message}");
            }
        }

        // Обработчики кнопок навигации
        private void WhiteListBtn_Click(object sender, RoutedEventArgs e)
        {
            LoadWhiteListPage();
        }

        private void LogsBtn_Click(object sender, RoutedEventArgs e)
        {
            LoadLogsPage();
        }

        private void ServiceBtn_Click(object sender, RoutedEventArgs e)
        {
            LoadServicePage();
        }

        // Методы загрузки страниц
        private void LoadWhiteListPage()
        {
            if (MainFrame != null)
            {
                MainFrame.Navigate(new WhiteListPage());
                UpdateButtonStates("WhiteList");
            }
        }

        private void LoadLogsPage()
        {
            if (MainFrame != null)
            {
                MainFrame.Navigate(new LogsPage());
                UpdateButtonStates("Logs");
            }
        }

        private void LoadServicePage()
        {
            if (MainFrame != null)
            {
                MainFrame.Navigate(new ServicePage());
                UpdateButtonStates("Service");
            }
        }

        // Обновление состояния кнопок
        private void UpdateButtonStates(string activePage)
        {
            Color defaultColor = Color.FromRgb(103, 58, 183);
            Color activeColor = Color.FromRgb(74, 20, 140);

            WhiteListBtn.Background = new SolidColorBrush(defaultColor);
            LogsBtn.Background = new SolidColorBrush(defaultColor);
            ServiceBtn.Background = new SolidColorBrush(defaultColor);

            switch (activePage)
            {
                case "WhiteList":
                    WhiteListBtn.Background = new SolidColorBrush(activeColor);
                    break;
                case "Logs":
                    LogsBtn.Background = new SolidColorBrush(activeColor);
                    break;
                case "Service":
                    ServiceBtn.Background = new SolidColorBrush(activeColor);
                    break;
            }
        }

        // Обновление статуса службы
        private async void UpdateServiceStatus()
        {
            try
            {
                // Запускаем в отдельном потоке
                string status = await Task.Run(() => ServiceManager.GetServiceStatus());

                // Обновляем UI в главном потоке
                await Dispatcher.InvokeAsync(() =>
                {
                    string displayText = status switch
                    {
                        "Running" => "Работает",
                        "Stopped" => "Остановлена",
                        "StopPending" => "Останавливается",
                        "StartPending" => "Запускается",
                        "Not Installed" => "Не установлена",
                        "Error" => "Ошибка",
                        _ => status
                    };

                    ServiceStatusText.Text = displayText;

                    switch (status)
                    {
                        case "Running":
                            StatusDot.Fill = Brushes.LimeGreen;
                            ProtectionStatusText.Text = "🛡️ Защита активна";
                            ProtectionStatusText.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                            break;
                        case "Stopped":
                            StatusDot.Fill = Brushes.Orange;
                            ProtectionStatusText.Text = "⚠️ Служба остановлена";
                            ProtectionStatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 152, 0));
                            break;
                        case "Not Installed":
                            StatusDot.Fill = Brushes.Red;
                            ProtectionStatusText.Text = "❌ Служба не установлена";
                            ProtectionStatusText.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                            break;
                        default:
                            StatusDot.Fill = Brushes.Gray;
                            ProtectionStatusText.Text = "❓ Статус защиты неизвестен";
                            ProtectionStatusText.Foreground = new SolidColorBrush(Color.FromRgb(158, 158, 158));
                            break;
                    }

                    Debug.WriteLine($"Status updated: {status}");
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateServiceStatus error: {ex.Message}");
                await Dispatcher.InvokeAsync(() =>
                {
                    ServiceStatusText.Text = "Ошибка";
                    StatusDot.Fill = Brushes.Gray;
                });
            }
        }

        private void UpdateProtectionStatus()
        {
            try
            {
                string serviceStatus = ServiceManager.GetServiceStatus();

                if (serviceStatus == "Running")
                {
                    ProtectionStatusText.Text = "🛡️ Защита активна";
                    ProtectionStatusText.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                }
                else if (serviceStatus == "Stopped")
                {
                    ProtectionStatusText.Text = "⚠️ Служба остановлена";
                    ProtectionStatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 152, 0));
                }
                else if (serviceStatus == "Not Installed")
                {
                    ProtectionStatusText.Text = "❌ Служба не установлена";
                    ProtectionStatusText.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                }
                else
                {
                    ProtectionStatusText.Text = "❓ Статус защиты неизвестен";
                    ProtectionStatusText.Foreground = new SolidColorBrush(Color.FromRgb(158, 158, 158));
                }
            }
            catch
            {
                ProtectionStatusText.Text = "❌ Ошибка проверки защиты";
                ProtectionStatusText.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _statusTimer?.Stop();
            base.OnClosed(e);
        }

       
    }
}