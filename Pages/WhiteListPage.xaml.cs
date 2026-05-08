using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WPFAPP.Managers;

namespace WPFAPP.Pages
{
    public partial class WhiteListPage : Page
    {
        private ObservableCollection<WhiteListItem> _items;
        private DispatcherTimer _refreshTimer;
        private List<HashCheckResult> _checkResults;

        public WhiteListPage()
        {
            InitializeComponent();
            WhiteListListView.AlternationCount = int.MaxValue;
            LoadWhiteList();

            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(30);
            _refreshTimer.Tick += (s, e) => RefreshWhiteList();
            _refreshTimer.Start();

            this.Loaded += WhiteListPage_Loaded;
        }

        private async void WhiteListPage_Loaded(object sender, RoutedEventArgs e)
        {
            await CheckHashesAsync();
        }

        private void LoadWhiteList()
        {
            try
            {
                string configPath = WhiteListManager.GetActiveConfigPath();
                var items = WhiteListManager.LoadFromConfig();

                // Удаляем дубликаты по имени (оставляем последний)
                var uniqueItems = items
                    .GroupBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.Last())
                    .ToList();

                // Если были дубликаты - сохраняем очищенный список
                if (items.Count != uniqueItems.Count)
                {
                    WhiteListManager.SaveConfig(uniqueItems);
                    Debug.WriteLine($"Удалено {items.Count - uniqueItems.Count} дубликатов при загрузке");
                }

                _items = new ObservableCollection<WhiteListItem>(uniqueItems);

                WhiteListListView.ItemsSource = null;
                WhiteListListView.ItemsSource = _items;
                WhiteListListView.Items.Refresh();

                UpdateStats();
                UpdateEmptyListVisibility();

                StatsText.Text = $"Приложений: {_items.Count} | Путь: {Path.GetDirectoryName(configPath)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки белого списка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                _items = new ObservableCollection<WhiteListItem>();
                WhiteListListView.ItemsSource = _items;
                StatsText.Text = $"Ошибка загрузки: {ex.Message}";
            }
        }

        private void UpdateStats()
        {
            if (_items == null) return;
            string configPath = WhiteListManager.GetActiveConfigPath();
            StatsText.Text = $"Приложений: {_items.Count} | Конфиг: {Path.GetFileName(configPath)}";
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

        private void RefreshWhiteList()
        {
            try
            {
                var items = WhiteListManager.LoadFromConfig();
                if (_items == null || items.Count != _items.Count ||
                    !items.Select(i => i.Hash).SequenceEqual(_items.Select(i => i.Hash)))
                {
                    LoadWhiteList();
                }
            }
            catch { }
        }

        private async Task CheckHashesAsync()
        {
            try
            {
                CheckHashesBtn.Content = "Проверка...";
                CheckHashesBtn.IsEnabled = false;
                StatsText.Text = "Проверка хешей приложений...";

                _checkResults = await HashChecker.CheckAllHashesAsync(_items.ToList());

                int changedCount = 0;
                int notFoundCount = 0;
                int okCount = 0;

                foreach (var result in _checkResults)
                {
                    // Ищем по имени БЕЗ учета регистра
                    var item = _items.FirstOrDefault(i =>
                        string.Equals(i.Name, result.Item.Name, StringComparison.OrdinalIgnoreCase));

                    if (item != null)
                    {
                        if (!result.FileExists)
                        {
                            item.Status = "Не найден";
                            item.StatusColor = "#F44336";
                            item.HashChanged = false;
                            item.NewHash = null;
                            notFoundCount++;
                        }
                        else if (result.HashChanged)
                        {
                            item.Status = "Изменен";
                            item.StatusColor = "#FF9800";
                            item.HashChanged = true;
                            item.NewHash = result.NewHash;
                            changedCount++;
                        }
                        else
                        {
                            item.Status = "OK";
                            item.StatusColor = "#4CAF50";
                            item.HashChanged = false;
                            item.NewHash = null;
                            okCount++;
                        }
                    }
                }

                UpdateAllBtn.Visibility = changedCount > 0 ? Visibility.Visible : Visibility.Collapsed;

                WhiteListListView.Items.Refresh();

                StatsText.Text = $"Приложений: {_items.Count} | " +
                               $"OK: {okCount} | " +
                               $"Изменено: {changedCount} | " +
                               $"Не найдено: {notFoundCount}";
            }
            catch (Exception ex)
            {
                StatsText.Text = $"Ошибка проверки: {ex.Message}";
                MessageBox.Show($"Ошибка проверки хешей: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                CheckHashesBtn.Content = "Проверить хеши";
                CheckHashesBtn.IsEnabled = true;
            }
        }

        private void AddAppBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var pickerWindow = new ApplicationPickerWindow();
                pickerWindow.Owner = Window.GetWindow(this);

                if (pickerWindow.ShowDialog() == true)
                {
                    LoadWhiteList();
                    // Не показываем MessageBox, окно само закроется
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RemoveAppBtn_Click(object sender, RoutedEventArgs e)
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
                    string configDir = WhiteListManager.GetConfigDirectory();

                    RemoveAppBtn.Content = "Удаление...";
                    RemoveAppBtn.IsEnabled = false;

                    try
                    {
                        bool success = await Task.Run(() =>
                            WhiteListManager.RemoveApplications(itemsToRemove, configDir));

                        if (success)
                        {
                            _items.Remove(selectedItem);
                            UpdateStats();
                            UpdateEmptyListVisibility();

                            MessageBox.Show("Приложение успешно удалено", "Успех",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            LoadWhiteList();
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
                LoadWhiteList();
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                RemoveAppBtn.Content = "Удалить выбранное";
                RemoveAppBtn.IsEnabled = true;
            }
        }

        private void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            LoadWhiteList();
        }

        private async void CheckHashesBtn_Click(object sender, RoutedEventArgs e)
        {
            await CheckHashesAsync();
        }

        private async void UpdateAllBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateAllBtn.Content = "Обновление...";
                UpdateAllBtn.IsEnabled = false;

                int updatedCount = 0;
                int failedCount = 0;

                foreach (var result in _checkResults.Where(r => r.HashChanged && !string.IsNullOrEmpty(r.NewHash)))
                {
                    bool success = await HashChecker.UpdateApplicationHash(result.Item, result.NewHash);
                    if (success)
                    {
                        // Обновляем хеш в существующем элементе коллекции
                        var item = _items.FirstOrDefault(i =>
                            string.Equals(i.Name, result.Item.Name, StringComparison.OrdinalIgnoreCase));
                        if (item != null)
                        {
                            item.Hash = result.NewHash;
                            item.Status = "OK";
                            item.StatusColor = "#4CAF50";
                            item.HashChanged = false;
                            item.NewHash = null;
                        }
                        updatedCount++;
                    }
                    else
                    {
                        failedCount++;
                    }
                }

                UpdateAllBtn.Visibility = Visibility.Collapsed;
                WhiteListListView.Items.Refresh();
                UpdateStats();

                string message = $"Обновлено: {updatedCount}";
                if (failedCount > 0)
                    message += $"\nНе удалось обновить: {failedCount}";

                MessageBox.Show(message, "Результат обновления",
                    MessageBoxButton.OK,
                    updatedCount > 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);

                // НЕ перезагружаем список и НЕ проверяем хеши повторно!
                // Всё уже обновлено в существующей коллекции
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка обновления: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                UpdateAllBtn.Content = "Обновить всё";
                UpdateAllBtn.IsEnabled = true;
            }
        }

        private async void UpdateSingleHashBtn_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var item = button?.Tag as WhiteListItem;
            if (item == null) return;

            try
            {
                button.Content = "...";
                button.IsEnabled = false;

                var checkResult = _checkResults?.FirstOrDefault(r =>
                    string.Equals(r.Item.Name, item.Name, StringComparison.OrdinalIgnoreCase));

                if (checkResult != null && checkResult.HashChanged && !string.IsNullOrEmpty(checkResult.NewHash))
                {
                    bool success = await HashChecker.UpdateApplicationHash(item, checkResult.NewHash);
                    if (success)
                    {
                        // Обновляем хеш в текущем элементе (НЕ перезагружаем список)
                        item.Hash = checkResult.NewHash;
                        item.Status = "OK";
                        item.StatusColor = "#4CAF50";
                        item.HashChanged = false;
                        item.NewHash = null;

                        WhiteListListView.Items.Refresh();
                        UpdateStats();

                        // Обновляем _checkResults чтобы убрать этот элемент из измененных
                        checkResult.HashChanged = false;
                        checkResult.OldHash = checkResult.NewHash;
                    }
                    else
                    {
                        MessageBox.Show("Не удалось обновить хеш в конфигурации", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка обновления: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                button.Content = "Обновить";
                button.IsEnabled = true;
            }
        }

        private void WhiteListListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var selectedItem = WhiteListListView.SelectedItem as WhiteListItem;
            if (selectedItem != null)
            {
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