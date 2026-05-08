using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using WPFAPP.Managers;
using WPFAPP.Utils;

namespace WPFAPP.Pages
{
    public partial class ApplicationPickerWindow : Window, INotifyPropertyChanged
    {
        private ObservableCollection<ApplicationInfo> _allApplications;
        private ObservableCollection<ApplicationInfo> _filteredApplications;
        private HashSet<string> _processedPaths;
        private HashSet<string> _existingHashes;
        private GridViewColumnHeader _lastHeaderClicked = null;
        private ListSortDirection _lastDirection = ListSortDirection.Ascending;
        private Dictionary<GridViewColumnHeader, string> _originalHeaders = new Dictionary<GridViewColumnHeader, string>();
        public event PropertyChangedEventHandler PropertyChanged;

        public ApplicationPickerWindow()
        {
            InitializeComponent();
            _allApplications = new ObservableCollection<ApplicationInfo>();
            _filteredApplications = new ObservableCollection<ApplicationInfo>();
            ApplicationsListView.ItemsSource = _filteredApplications;
            ApplicationsListView.AddHandler(GridViewColumnHeader.ClickEvent,
       new RoutedEventHandler(GridViewColumnHeader_Click));
            ApplicationInfo.SelectionChanged += () =>
            {
                Dispatcher.Invoke(() => UpdateSelectionCount());
            };

            LoadApplicationsAsync();
        }

        private async void LoadApplicationsAsync()
        {
            try
            {
                ShowProgress(true, "Сканирование системы...");
                AddSelectedBtn.IsEnabled = false;

                var whiteListItems = WhiteListManager.LoadFromConfig();
                _existingHashes = new HashSet<string>(
                    whiteListItems.Select(i => i.Hash),
                    StringComparer.OrdinalIgnoreCase);

                _processedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var applications = await Task.Run(() => ScanAllApplications());

                await Dispatcher.InvokeAsync(() =>
                {
                    _allApplications.Clear();
                    _filteredApplications.Clear();

                    foreach (var app in applications.OrderBy(a => a.Name))
                    {
                        _allApplications.Add(app);
                        _filteredApplications.Add(app);
                    }

                    // Сразу включаем кнопку
                    AddSelectedBtn.IsEnabled = false;

                    UpdateSelectionCount();
                    ShowProgress(false);

                    StatsText.Text = $"Найдено: {_allApplications.Count:N0} | " +
                                    $"В белом списке: {whiteListItems.Count:N0}";
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"Ошибка загрузки приложений: {ex.Message}",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    ShowProgress(false);
                    AddSelectedBtn.IsEnabled = true;
                });
            }
        }

        private List<ApplicationInfo> ScanAllApplications()
        {
            var allApps = new List<ApplicationInfo>();

            allApps.AddRange(ScanInstalledFromRegistry());
            allApps.AddRange(ScanProgramFiles());
            allApps.AddRange(ScanStartMenu());
            allApps.AddRange(ScanDesktop());
            allApps.AddRange(ScanRunningProcesses());
            allApps.AddRange(ScanSystemApplications());
            allApps.AddRange(ScanAppData());
            allApps.AddRange(ScanStoreApps());
            allApps.AddRange(ScanPathDirectories());

            return allApps
                .GroupBy(a => a.FilePath, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(a => a.Name)
                .ToList();
        }


        private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            var headerClicked = e.OriginalSource as GridViewColumnHeader;

            if (headerClicked == null || headerClicked.Role == GridViewColumnHeaderRole.Padding)
                return;

            // Сохраняем оригинальный заголовок при первом клике
            if (!_originalHeaders.ContainsKey(headerClicked))
            {
                _originalHeaders[headerClicked] = headerClicked.Column.Header as string ?? "";
            }

            string originalHeader = _originalHeaders[headerClicked];

            ListSortDirection direction;

            if (headerClicked != _lastHeaderClicked)
            {
                direction = ListSortDirection.Ascending;
            }
            else
            {
                direction = _lastDirection == ListSortDirection.Ascending
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending;
            }

            // Сортируем
            Sort(originalHeader, direction);

            // Сначала убираем стрелку с предыдущего заголовка
            if (_lastHeaderClicked != null && _lastHeaderClicked != headerClicked)
            {
                if (_originalHeaders.ContainsKey(_lastHeaderClicked))
                {
                    _lastHeaderClicked.Column.Header = _originalHeaders[_lastHeaderClicked];
                }
            }

            // Добавляем стрелку к текущему заголовку
            string arrow = direction == ListSortDirection.Ascending ? " ▲" : " ▼";
            headerClicked.Column.Header = originalHeader + arrow;

            _lastHeaderClicked = headerClicked;
            _lastDirection = direction;
        }

        private void Sort(string sortBy, ListSortDirection direction)
        {
            // Убираем возможные стрелки из имени колонки
            sortBy = sortBy.Replace(" ▲", "").Replace(" ▼", "").Trim();

            ICollectionView dataView = CollectionViewSource.GetDefaultView(_filteredApplications);

            dataView.SortDescriptions.Clear();

            switch (sortBy)
            {
                case "Приложение":
                    dataView.SortDescriptions.Add(new SortDescription("Name", direction));
                    break;
                case "Файл":
                    dataView.SortDescriptions.Add(new SortDescription("FileName", direction));
                    break;
                case "Путь":
                    dataView.SortDescriptions.Add(new SortDescription("FilePath", direction));
                    break;
                case "Размер":
                    dataView.SortDescriptions.Add(new SortDescription("Size", direction));
                    break;
                case "Статус":
                    dataView.SortDescriptions.Add(new SortDescription("Status", direction));
                    break;
                default:
                    dataView.SortDescriptions.Add(new SortDescription("Name", direction));
                    break;
            }

            dataView.Refresh();
        }


        private List<ApplicationInfo> ScanInstalledFromRegistry()
        {
            var apps = new List<ApplicationInfo>();

            string[] registryKeys = {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            foreach (var registryKey in registryKeys)
            {
                try
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(registryKey))
                    {
                        if (key != null)
                        {
                            foreach (string subKeyName in key.GetSubKeyNames())
                            {
                                try
                                {
                                    using (RegistryKey subKey = key.OpenSubKey(subKeyName))
                                    {
                                        if (subKey == null) continue;

                                        string displayName = subKey.GetValue("DisplayName") as string;
                                        string displayIcon = subKey.GetValue("DisplayIcon") as string;
                                        string installLocation = subKey.GetValue("InstallLocation") as string;

                                        string exePath = CleanExePath(displayIcon ?? installLocation);

                                        if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                                        {
                                            AddUniqueApp(apps,
                                                displayName ?? Path.GetFileNameWithoutExtension(exePath),
                                                exePath);
                                        }
                                        else if (!string.IsNullOrEmpty(installLocation) && Directory.Exists(installLocation))
                                        {
                                            try
                                            {
                                                var exeFiles = Directory.GetFiles(installLocation, "*.exe",
                                                    SearchOption.TopDirectoryOnly).Take(3);
                                                foreach (var exe in exeFiles)
                                                {
                                                    AddUniqueApp(apps,
                                                        displayName ?? Path.GetFileNameWithoutExtension(exe),
                                                        exe);
                                                }
                                            }
                                            catch { }
                                        }
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                }
                catch { }
            }

            // Current User
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                {
                    if (key != null)
                    {
                        foreach (string subKeyName in key.GetSubKeyNames())
                        {
                            try
                            {
                                using (RegistryKey subKey = key.OpenSubKey(subKeyName))
                                {
                                    if (subKey == null) continue;

                                    string displayName = subKey.GetValue("DisplayName") as string;
                                    string installLocation = subKey.GetValue("InstallLocation") as string;
                                    string displayIcon = subKey.GetValue("DisplayIcon") as string;

                                    string exePath = CleanExePath(displayIcon ?? installLocation);

                                    if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                                    {
                                        AddUniqueApp(apps,
                                            displayName ?? Path.GetFileNameWithoutExtension(exePath),
                                            exePath);
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }

            return apps;
        }

        private List<ApplicationInfo> ScanProgramFiles()
        {
            var apps = new List<ApplicationInfo>();

            string[] programPaths = {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };

            foreach (var programPath in programPaths)
            {
                if (string.IsNullOrEmpty(programPath) || !Directory.Exists(programPath))
                    continue;

                try
                {
                    var exeFiles = Directory.GetFiles(programPath, "*.exe", SearchOption.AllDirectories)
                        .Where(f => !f.Contains(@"\Windows\", StringComparison.OrdinalIgnoreCase) &&
                                   !f.Contains(@"\Temp\", StringComparison.OrdinalIgnoreCase) &&
                                   !f.Contains(@"\Uninstall\", StringComparison.OrdinalIgnoreCase) &&
                                   !f.Contains(@"\Setup\", StringComparison.OrdinalIgnoreCase) &&
                                   !f.Contains(@"\Installer\", StringComparison.OrdinalIgnoreCase))
                        .Take(5000);

                    foreach (var exeFile in exeFiles)
                    {
                        try
                        {
                            var fileInfo = new FileInfo(exeFile);
                            if (fileInfo.Length < 1024) continue;

                            string appName = Path.GetFileNameWithoutExtension(exeFile);
                            AddUniqueApp(apps, appName, exeFile);
                        }
                        catch { }
                    }
                }
                catch { }
            }

            return apps;
        }

        private List<ApplicationInfo> ScanStartMenu()
        {
            var apps = new List<ApplicationInfo>();

            string[] startMenuPaths = {
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu)
            };

            foreach (var startPath in startMenuPaths)
            {
                if (string.IsNullOrEmpty(startPath) || !Directory.Exists(startPath))
                    continue;

                try
                {
                    foreach (var lnkFile in Directory.GetFiles(startPath, "*.lnk", SearchOption.AllDirectories))
                    {
                        try
                        {
                            string targetPath = ResolveShortcut(lnkFile);
                            if (!string.IsNullOrEmpty(targetPath) &&
                                targetPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                                File.Exists(targetPath))
                            {
                                string appName = Path.GetFileNameWithoutExtension(lnkFile);
                                AddUniqueApp(apps, appName, targetPath);
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }

            return apps;
        }

        private List<ApplicationInfo> ScanDesktop()
        {
            var apps = new List<ApplicationInfo>();

            string[] desktopPaths = {
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
            };

            foreach (var desktopPath in desktopPaths)
            {
                if (string.IsNullOrEmpty(desktopPath) || !Directory.Exists(desktopPath))
                    continue;

                try
                {
                    foreach (var lnkFile in Directory.GetFiles(desktopPath, "*.lnk", SearchOption.TopDirectoryOnly))
                    {
                        try
                        {
                            string targetPath = ResolveShortcut(lnkFile);
                            if (!string.IsNullOrEmpty(targetPath) &&
                                targetPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                                File.Exists(targetPath))
                            {
                                string appName = Path.GetFileNameWithoutExtension(lnkFile);
                                AddUniqueApp(apps, appName, targetPath);
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }

            return apps;
        }

        private List<ApplicationInfo> ScanRunningProcesses()
        {
            var apps = new List<ApplicationInfo>();

            try
            {
                var processes = Process.GetProcesses();
                foreach (var process in processes)
                {
                    try
                    {
                        if (process.SessionId == 0) continue;

                        string filePath = null;
                        try
                        {
                            filePath = process.MainModule?.FileName;
                        }
                        catch { }

                        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                        {
                            string appName = process.ProcessName;
                            try
                            {
                                var fileVersionInfo = FileVersionInfo.GetVersionInfo(filePath);
                                if (!string.IsNullOrEmpty(fileVersionInfo.FileDescription))
                                {
                                    appName = fileVersionInfo.FileDescription;
                                }
                            }
                            catch { }

                            AddUniqueApp(apps, appName, filePath);
                        }
                    }
                    catch { }
                }
            }
            catch { }

            return apps;
        }

        private List<ApplicationInfo> ScanSystemApplications()
        {
            var apps = new List<ApplicationInfo>();

            string[] systemPaths = {
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SysWOW64")
            };

            string[] knownSystemApps = {
                "notepad.exe", "calc.exe", "mspaint.exe", "write.exe",
                "wordpad.exe", "cmd.exe", "powershell.exe", "powershell_ise.exe",
                "control.exe", "regedit.exe", "taskmgr.exe", "explorer.exe",
                "mmc.exe", "perfmon.exe", "resmon.exe", "msconfig.exe",
                "dxdiag.exe", "cleanmgr.exe", "msinfo32.exe", "mstsc.exe",
                "snippingtool.exe", "wscript.exe", "cscript.exe"
            };

            foreach (var path in systemPaths)
            {
                if (!Directory.Exists(path)) continue;

                foreach (string appName in knownSystemApps)
                {
                    string fullPath = Path.Combine(path, appName);
                    if (File.Exists(fullPath))
                    {
                        AddUniqueApp(apps, Path.GetFileNameWithoutExtension(fullPath), fullPath);
                    }
                }
            }

            return apps;
        }

        private List<ApplicationInfo> ScanAppData()
        {
            var apps = new List<ApplicationInfo>();

            string[] appDataPaths = {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            };

            foreach (var appDataPath in appDataPaths)
            {
                if (!Directory.Exists(appDataPath)) continue;

                try
                {
                    var exeFiles = Directory.GetFiles(appDataPath, "*.exe", SearchOption.AllDirectories)
                        .Where(f => !f.Contains(@"\Temp\", StringComparison.OrdinalIgnoreCase) &&
                                   !f.Contains(@"\Cache\", StringComparison.OrdinalIgnoreCase) &&
                                   !f.Contains(@"\Installer\", StringComparison.OrdinalIgnoreCase))
                        .Take(2000);

                    foreach (var exeFile in exeFiles)
                    {
                        try
                        {
                            var fileInfo = new FileInfo(exeFile);
                            if (fileInfo.Length < 1024 * 50) continue;

                            string appName = Path.GetFileNameWithoutExtension(exeFile);

                            try
                            {
                                var fileVersionInfo = FileVersionInfo.GetVersionInfo(exeFile);
                                if (!string.IsNullOrEmpty(fileVersionInfo.FileDescription))
                                {
                                    appName = fileVersionInfo.FileDescription;
                                }
                            }
                            catch { }

                            AddUniqueApp(apps, appName, exeFile);
                        }
                        catch { }
                    }
                }
                catch { }
            }

            return apps;
        }

        private List<ApplicationInfo> ScanStoreApps()
        {
            var apps = new List<ApplicationInfo>();

            try
            {
                string packagesPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "WindowsApps");

                if (Directory.Exists(packagesPath))
                {
                    try
                    {
                        var exeFiles = Directory.GetFiles(packagesPath, "*.exe", SearchOption.AllDirectories)
                            .Take(500);

                        foreach (var exeFile in exeFiles)
                        {
                            try
                            {
                                string appName = Path.GetFileNameWithoutExtension(exeFile);
                                AddUniqueApp(apps, appName, exeFile);
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            }
            catch { }

            return apps;
        }

        private List<ApplicationInfo> ScanPathDirectories()
        {
            var apps = new List<ApplicationInfo>();

            string pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                var paths = pathEnv.Split(';');
                foreach (var path in paths)
                {
                    if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                        continue;

                    try
                    {
                        foreach (var exeFile in Directory.GetFiles(path, "*.exe"))
                        {
                            try
                            {
                                string appName = Path.GetFileNameWithoutExtension(exeFile);
                                AddUniqueApp(apps, appName, exeFile);
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            }

            return apps;
        }

        private string CleanExePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            path = path.Trim('"', '\'');

            int exeIndex = path.ToLower().IndexOf(".exe");
            if (exeIndex > 0)
            {
                path = path.Substring(0, exeIndex + 4);
            }

            int commaIndex = path.IndexOf(',');
            if (commaIndex > 0)
            {
                path = path.Substring(0, commaIndex);
            }

            return path.Trim();
        }

        private string ResolveShortcut(string shortcutPath)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return null;

                dynamic shell = Activator.CreateInstance(shellType);
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                string targetPath = shortcut.TargetPath;

                System.Runtime.InteropServices.Marshal.ReleaseComObject(shortcut);
                System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);

                return targetPath;
            }
            catch
            {
                return null;
            }
        }

        private void AddUniqueApp(List<ApplicationInfo> apps, string name, string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            if (!_processedPaths.Add(path))
                return;

            string hash = HashUtils.CalculateSHA256(path);
            if (!string.IsNullOrEmpty(hash) && _existingHashes.Contains(hash))
                return;

            apps.Add(new ApplicationInfo
            {
                Name = name ?? Path.GetFileNameWithoutExtension(path),
                FileName = Path.GetFileName(path),
                FilePath = path,
                Hash = hash,
                Size = new FileInfo(path).Length,
                LastModified = File.GetLastWriteTime(path)
            });
        }

        private void ShowProgress(bool show, string text = "")
        {
            ProgressBar.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            ProgressText.Text = text;
            ProgressText.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_allApplications == null) return;

            _filteredApplications.Clear();

            string search = SearchTextBox.Text?.ToLower() ?? "";

            if (string.IsNullOrWhiteSpace(search))
            {
                foreach (var app in _allApplications)
                {
                    _filteredApplications.Add(app);
                }
            }
            else
            {
                foreach (var app in _allApplications.Where(a =>
                    a.Name.ToLower().Contains(search) ||
                    a.FileName.ToLower().Contains(search) ||
                    a.FilePath.ToLower().Contains(search)))
                {
                    _filteredApplications.Add(app);
                }
            }

            UpdateSelectionCount();
        }


        private void FilterApplications(string searchText)
        {
            if (_allApplications == null) return;

            _filteredApplications.Clear();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                foreach (var app in _allApplications)
                {
                    _filteredApplications.Add(app);
                }
            }
            else
            {
                string search = searchText.ToLower();
                var filtered = _allApplications.Where(a =>
                    a.Name.ToLower().Contains(search) ||
                    a.FileName.ToLower().Contains(search) ||
                    a.FilePath.ToLower().Contains(search)
                ).ToList();

                foreach (var app in filtered)
                {
                    _filteredApplications.Add(app);
                }
            }

            UpdateSelectionCount();
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            UpdateSelectionCount();
        }

        private void SelectAllBtn_Click(object sender, RoutedEventArgs e)
        {
            foreach (var app in _filteredApplications)
            {
                app.IsSelected = true;
            }
            UpdateSelectionCount();
        }

        private void DeselectAllBtn_Click(object sender, RoutedEventArgs e)
        {
            foreach (var app in _filteredApplications)
            {
                app.IsSelected = false;
            }
            UpdateSelectionCount();
        }

        private void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Text = "";
            LoadApplicationsAsync();
        }

        private async void AddSelectedBtn_Click(object sender, RoutedEventArgs e)
        {
            // Считаем выбранные из основной коллекции
            var selectedApps = _allApplications.Where(a => a.IsSelected).ToList();

            if (selectedApps.Count == 0)
            {
                MessageBox.Show("Выберите хотя бы одно приложение", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                int totalToAdd = selectedApps.Count;
                ShowProgress(true, $"Добавление {totalToAdd} приложений...");
                AddSelectedBtn.IsEnabled = false;

                int addedCount = 0;
                int skippedCount = 0;
                var errors = new List<string>();

                await Task.Run(() =>
                {
                    for (int i = 0; i < selectedApps.Count; i++)
                    {
                        var app = selectedApps[i];
                        try
                        {
                            var result = WhiteListManager.AddApplication(app.FilePath);

                            Dispatcher.Invoke(() =>
                            {
                                if (result.Success)
                                {
                                    addedCount++;
                                    app.Status = "Добавлено ✓";
                                    app.StatusColor = "Green";
                                    app.IsSelected = false; // Снимаем выбор
                                }
                                else
                                {
                                    skippedCount++;
                                    app.Status = result.Error ?? "Ошибка";
                                    app.StatusColor = "Red";
                                    if (!string.IsNullOrEmpty(result.Error))
                                        errors.Add($"{app.Name}: {result.Error}");
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                skippedCount++;
                                app.Status = ex.Message;
                                app.StatusColor = "Red";
                                errors.Add($"{app.Name}: {ex.Message}");
                            });
                        }

                        // Обновляем прогресс
                        int progress = (int)((double)(i + 1) / totalToAdd * 100);
                        Dispatcher.Invoke(() =>
                        {
                            ProgressBar.Value = progress;
                            ProgressText.Text = $"Добавлено: {addedCount} из {totalToAdd}";
                        });
                    }
                });

                // Обновляем счетчик после добавления
                UpdateSelectionCount();

                string message = $"✓ Добавлено: {addedCount}\n✗ Пропущено: {skippedCount}";
                if (errors.Count > 0)
                {
                    message += $"\n\nОшибки ({Math.Min(errors.Count, 10)}):\n" +
                              string.Join("\n", errors.Take(10));
                    if (errors.Count > 10)
                        message += $"\n...и ещё {errors.Count - 10}";
                }

                MessageBox.Show(message, "Результат",
                    MessageBoxButton.OK,
                    addedCount > 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);

                if (addedCount > 0)
                {
                    DialogResult = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ShowProgress(false);
                AddSelectedBtn.IsEnabled = true;
                UpdateSelectionCount();
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void UpdateSelectionCount()
        {
            if (_allApplications != null && _allApplications.Count > 0)
            {
                int selected = 0;

                // Считаем выбранные из ВСЕХ приложений
                foreach (var app in _allApplications)
                {
                    if (app.IsSelected)
                        selected++;
                }

                int total = _allApplications.Count;
                int visible = 0;

                // Считаем видимые элементы
                var view = CollectionViewSource.GetDefaultView(_filteredApplications);
                foreach (var item in view)
                {
                    visible++;
                }

                int inWhiteList = _existingHashes?.Count ?? 0;

                AddSelectedBtn.Content = $"Добавить выбранные ({selected:N0})";
                StatsText.Text = $"Найдено: {total:N0} | " +
                                $"В белом списке: {inWhiteList:N0} | " +
                                $"Выбрано: {selected:N0} | " +
                                $"Показано: {visible:N0}";

                // Отключаем кнопку если ничего не выбрано
                AddSelectedBtn.IsEnabled = selected > 0;
            }
            else
            {
                AddSelectedBtn.Content = "Добавить выбранные (0)";
                AddSelectedBtn.IsEnabled = false;
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ApplicationInfo : INotifyPropertyChanged
    {
        private bool _isSelected;
        private string _status = "";
        private string _statusColor = "Gray";

       
        public static event Action SelectionChanged;

        public string Name { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string Hash { get; set; }
        public long Size { get; set; }
        public DateTime LastModified { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                    // Уведомляем окно об изменении выбора
                    SelectionChanged?.Invoke();
                }
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                }
            }
        }

        public string StatusColor
        {
            get => _statusColor;
            set
            {
                if (_statusColor != value)
                {
                    _statusColor = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SizeFormatted
        {
            get
            {
                if (Size < 1024) return $"{Size} B";
                if (Size < 1024 * 1024) return $"{Size / 1024.0:F1} KB";
                if (Size < 1024 * 1024 * 1024) return $"{Size / (1024.0 * 1024.0):F1} MB";
                return $"{Size / (1024.0 * 1024.0 * 1024.0):F2} GB";
            }
        }

        public string LastModifiedFormatted => LastModified.ToString("dd.MM.yyyy HH:mm");

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}