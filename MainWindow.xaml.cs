using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Text;
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
                string status = await Task.Run(() => ServiceManager.GetServiceStatus());

                await Dispatcher.InvokeAsync(() =>
                {
                    if (status == "Not Installed")
                    {
                        ServiceStatusText.Text = "Не установлена";
                        StatusDot.Fill = Brushes.Red;
                        ProtectionStatusText.Text = "❌ Служба не установлена";
                        ProtectionStatusText.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                    }
                    else if (status == "Running")
                    {
                        ServiceStatusText.Text = "Работает";
                        StatusDot.Fill = Brushes.LimeGreen;
                        ProtectionStatusText.Text = "🛡️ Защита активна";
                        ProtectionStatusText.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                    }
                    else if (status == "Stopped")
                    {
                        ServiceStatusText.Text = "Остановлена";
                        StatusDot.Fill = Brushes.Orange;
                        ProtectionStatusText.Text = "⚠️ Служба остановлена";
                        ProtectionStatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 152, 0));
                    }
                    else
                    {
                        ServiceStatusText.Text = status;
                        StatusDot.Fill = Brushes.Gray;
                        ProtectionStatusText.Text = "❓ Статус неизвестен";
                        ProtectionStatusText.Foreground = new SolidColorBrush(Color.FromRgb(158, 158, 158));
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateServiceStatus error: {ex.Message}");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _statusTimer?.Stop();
            base.OnClosed(e);
        }

       
    }
}