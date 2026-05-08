using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WPFAPP.Pages;
using WPFAPP.Utils;

namespace WPFAPP.Managers
{
    public class HashCheckResult
    {
        public WhiteListItem Item { get; set; }
        public string OldHash { get; set; }
        public string NewHash { get; set; }
        public bool HashChanged { get; set; }
        public bool FileExists { get; set; }
        public string FilePath { get; set; }
    }

    public static class HashChecker
    {
        public static async Task<List<HashCheckResult>> CheckAllHashesAsync(List<WhiteListItem> items)
        {
            return await Task.Run(() => CheckAllHashes(items));
        }

        private static List<HashCheckResult> CheckAllHashes(List<WhiteListItem> items)
        {
            var results = new List<HashCheckResult>();

            foreach (var item in items)
            {
                var result = new HashCheckResult
                {
                    Item = item,
                    OldHash = item.Hash
                };

                // Ищем файл по всей системе
                string foundPath = FindApplicationPath(item.Name);

                if (!string.IsNullOrEmpty(foundPath) && File.Exists(foundPath))
                {
                    result.FileExists = true;
                    result.FilePath = foundPath;

                    string newHash = HashUtils.CalculateSHA256(foundPath);
                    result.NewHash = newHash;

                    result.HashChanged = !string.Equals(item.Hash, newHash, StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    result.FileExists = false;
                    result.HashChanged = false;
                }

                results.Add(result);
            }

            return results;
        }

        private static string FindApplicationPath(string appName)
        {
            if (string.IsNullOrEmpty(appName))
                return null;

            // 1. Проверяем запущенные процессы
            try
            {
                var processes = Process.GetProcessesByName(appName);
                if (processes.Length > 0)
                {
                    try
                    {
                        string path = processes[0].MainModule?.FileName;
                        if (!string.IsNullOrEmpty(path) && File.Exists(path))
                            return path;
                    }
                    catch { }
                }
            }
            catch { }

            // 2. Ищем через Where.exe (быстрый поиск в PATH)
            try
            {
                using (Process whereProcess = new Process())
                {
                    whereProcess.StartInfo.FileName = "where.exe";
                    whereProcess.StartInfo.Arguments = appName + ".exe";
                    whereProcess.StartInfo.UseShellExecute = false;
                    whereProcess.StartInfo.CreateNoWindow = true;
                    whereProcess.StartInfo.RedirectStandardOutput = true;

                    whereProcess.Start();
                    string output = whereProcess.StandardOutput.ReadToEnd();
                    whereProcess.WaitForExit(3000);

                    if (!string.IsNullOrEmpty(output))
                    {
                        string firstPath = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                            .FirstOrDefault();
                        if (!string.IsNullOrEmpty(firstPath) && File.Exists(firstPath))
                            return firstPath;
                    }
                }
            }
            catch { }

            // 3. Поиск в Program Files
            string[] searchDirs = {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SysWOW64"),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
            };

            foreach (var dir in searchDirs)
            {
                if (!Directory.Exists(dir))
                    continue;

                try
                {
                    // Ищем точное совпадение
                    string exactPath = Path.Combine(dir, appName + ".exe");
                    if (File.Exists(exactPath))
                        return exactPath;

                    // Рекурсивный поиск (ограниченный)
                    var foundFiles = Directory.GetFiles(dir, appName + ".exe", SearchOption.AllDirectories)
                        .Take(5);

                    if (foundFiles.Any())
                        return foundFiles.First();
                }
                catch { }
            }

            // 4. Поиск через реестр (Uninstall)
            try
            {
                string path = FindPathFromRegistry(appName);
                if (!string.IsNullOrEmpty(path))
                    return path;
            }
            catch { }

            return null;
        }

        private static string FindPathFromRegistry(string appName)
        {
            string[] registryPaths = {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            foreach (var regPath in registryPaths)
            {
                try
                {
                    using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(regPath))
                    {
                        if (key == null) continue;

                        foreach (string subKeyName in key.GetSubKeyNames())
                        {
                            try
                            {
                                using (var subKey = key.OpenSubKey(subKeyName))
                                {
                                    if (subKey == null) continue;

                                    string displayName = subKey.GetValue("DisplayName") as string;
                                    if (string.IsNullOrEmpty(displayName)) continue;

                                    if (displayName.IndexOf(appName, StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        string iconPath = subKey.GetValue("DisplayIcon") as string;
                                        string installPath = subKey.GetValue("InstallLocation") as string;

                                        string exePath = CleanExePath(iconPath);
                                        if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                                            return exePath;

                                        if (!string.IsNullOrEmpty(installPath))
                                        {
                                            string possiblePath = Path.Combine(installPath, appName + ".exe");
                                            if (File.Exists(possiblePath))
                                                return possiblePath;
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }

            return null;
        }

        private static string CleanExePath(string path)
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

        public static async Task<bool> UpdateApplicationHash(WhiteListItem item, string newHash)
        {
            return await Task.Run(() =>
            {
                try
                {
                    return WhiteListManager.UpdateApplicationHash(item.Name, item.Hash, newHash);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[HashChecker] Ошибка обновления хеша: {ex.Message}");
                    return false;
                }
            });
        }
    }
}