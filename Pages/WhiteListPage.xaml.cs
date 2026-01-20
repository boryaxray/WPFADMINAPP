using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace WPFAPP.Pages
{
    public partial class WhiteListPage : Page
    {
        private ObservableCollection<WhiteListItem> _items;
        private string _configPath = null;
        private DispatcherTimer _refreshTimer;

        public WhiteListPage()
        {
            InitializeComponent();

            // Устанавливаем большое значение для AlternationCount
            WhiteListListView.AlternationCount = int.MaxValue;

            LoadWhiteList();

            // Таймер для автообновления (каждые 30 секунд)
            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(30);
            _refreshTimer.Tick += (s, e) => RefreshWhiteList();
            _refreshTimer.Start();
        }

        private void LoadWhiteList()
        {
            try
            {
                // Обновляем статистику перед загрузкой
                string configPath = Managers.WhiteListManager.GetActiveConfigPath();
                StatsText.Text = $"Загрузка из: {configPath}";

                var items = Managers.WhiteListManager.LoadFromConfig();
                _items = new ObservableCollection<WhiteListItem>(items);

                WhiteListListView.ItemsSource = null;
                WhiteListListView.ItemsSource = _items;

                UpdateStats();
                UpdateEmptyListVisibility();

                // Показываем актуальную информацию
                StatsText.Text = $"Приложений: {_items.Count} | Путь: {Path.GetDirectoryName(configPath)}";

                // Для отладки
                System.Diagnostics.Debug.WriteLine($"Загружено {_items.Count} записей из {configPath}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки белого списка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatsText.Text = $"Ошибка: {ex.Message}";
            }
        }

        private void UpdateStats()
        {
            if (_items == null)
                return;

            string configPath = Managers.WhiteListManager.GetActiveConfigPath();
            StatsText.Text = $"Приложений: {_items.Count} | Конфиг: {Path.GetFileName(configPath)}";
        }

        private void RefreshWhiteList()
        {
            try
            {
                var items = Managers.WhiteListManager.LoadFromConfig();
                if (_items == null || items.Count != _items.Count ||
                    !items.Select(i => i.Hash).SequenceEqual(_items.Select(i => i.Hash)))
                {
                    LoadWhiteList();
                }
            }
            catch { }
        }


        private void UpdateEmptyListVisibility()
        {
            if (_items == null || _items.Count == 0)
            {
                EmptyListPanel.Visibility = Visibility.Visible;
                WhiteListListView.Visibility = Visibility.Collapsed;
            }
            else
            {
                EmptyListPanel.Visibility = Visibility.Collapsed;
                WhiteListListView.Visibility = Visibility.Visible;
            }
        }

        //Добавление приложения в белый список
        private void AddAppBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string filePath = Managers.WhiteListManager.SelectFile();
                if (!string.IsNullOrEmpty(filePath))
                {
                    // Показываем прогресс
                    AddAppBtn.Content = "Добавление...";
                    AddAppBtn.IsEnabled = false;

                    try
                    {
                        // Получаем путь конфигурации
                        string configDir = Managers.WhiteListManager.GetConfigDirectory();

                        // Добавляем приложение
                        var result = Managers.WhiteListManager.AddApplication(filePath, configDir);

                        if (result.Success)
                        {
                            MessageBox.Show(
                                $"Приложение успешно добавлено в белый список!\n\n" +
                                $"Имя: {System.IO.Path.GetFileNameWithoutExtension(filePath)}\n" +
                                $"Хэш: {result.Hash.Substring(0, 32)}...",
                                "Успех",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);

                            LoadWhiteList();
                        }
                        else
                        {
                            MessageBox.Show(
                                $"Не удалось добавить приложение:\n{result.Error}",
                                "Ошибка",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        }
                    }
                    finally
                    {
                        AddAppBtn.Content = "Добавить приложение";
                        AddAppBtn.IsEnabled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                AddAppBtn.Content = "Добавить приложение";
                AddAppBtn.IsEnabled = true;
            }
        }

        private void RemoveAppBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = WhiteListListView.SelectedItem as WhiteListItem;
                if (selectedItem == null)
                {
                    MessageBox.Show("Выберите приложение для удаления", "Внимание",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show($"Удалить приложение '{selectedItem.Name}' из белого списка?",
                    "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var itemsToRemove = new List<WhiteListItem> { selectedItem };

                    // Получаем путь конфигурации
                    string configDir = Managers.WhiteListManager.GetConfigDirectory();

                    // Показываем прогресс
                    RemoveAppBtn.Content = "Удаление...";
                    RemoveAppBtn.IsEnabled = false;

                    try
                    {
                        if (Managers.WhiteListManager.RemoveApplications(itemsToRemove, configDir))
                        {
                            MessageBox.Show("Приложение успешно удалено", "Успех",
                                MessageBoxButton.OK, MessageBoxImage.Information);

                            // Полностью перезагружаем список
                            LoadWhiteList();
                        }
                        else
                        {
                            MessageBox.Show("Не удалось удалить приложение", "Ошибка",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    finally
                    {
                        RemoveAppBtn.Content = "Удалить выбранное";
                        RemoveAppBtn.IsEnabled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            LoadWhiteList();
        }

        private void WhiteListListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var selectedItem = WhiteListListView.SelectedItem as WhiteListItem;
            if (selectedItem != null)
            {
                // Показать детальную информацию
                MessageBox.Show(
                    $"Приложение: {selectedItem.Name}\n\n" +
                    $"Хэш SHA-256:\n{selectedItem.FullHash}",
                    "Информация о приложении",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            _refreshTimer?.Stop();
        }
    }
}