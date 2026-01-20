using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WPFAPP.Managers;
using WPFAPP.Pages;

namespace WPFAPP
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer _statusTimer;

        public MainWindow()
        {
            InitializeComponent();
            LoadWhiteListPage();
            InitializeStatusTimer();
            UpdateServiceStatus();
        }

        private void InitializeStatusTimer()
        {
            _statusTimer = new DispatcherTimer();
            _statusTimer.Interval = TimeSpan.FromSeconds(5);
            _statusTimer.Tick += (s, e) => UpdateServiceStatus();
            _statusTimer.Start();
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
        private void UpdateServiceStatus()
        {
            try
            {
                string status = ServiceManager.GetServiceStatus();
                ServiceStatusText.Text = status;

                switch (status)
                {
                    case "Running":
                        StatusDot.Fill = Brushes.LimeGreen;
                        break;
                    case "Stopped":
                        StatusDot.Fill = Brushes.Orange;
                        break;
                    case "Not Installed":
                        StatusDot.Fill = Brushes.Red;
                        break;
                    default:
                        StatusDot.Fill = Brushes.Gray;
                        break;
                }
            }
            catch (Exception)
            {
                ServiceStatusText.Text = "Ошибка";
                StatusDot.Fill = Brushes.Gray;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _statusTimer?.Stop();
            base.OnClosed(e);
        }
    }
}