using System;
using System.Collections.Generic;
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
        private static readonly string[] SearchPaths = {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32"),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        };

        public static async Task<List<HashCheckResult>> CheckAllHashesAsync(List<WhiteListItem> items)
        {
            return await Task.Run(() => CheckAllHashes(items));
        }

        private static List<HashCheckResult> CheckAllHashes(List<WhiteListItem> items)
        {
            var results = new List<HashCheckResult>();
            var foundFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Сканируем систему
            ScanForExecutableFiles(foundFiles);

            // Проверяем каждый элемент
            foreach (var item in items)
            {
                var result = new HashCheckResult
                {
                    Item = item,
                    OldHash = item.Hash
                };

                string foundPath = FindFileByName(foundFiles, item.Name);

                if (foundPath != null && File.Exists(foundPath))
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

        private static void ScanForExecutableFiles(Dictionary<string, string> foundFiles)
        {
            foreach (var searchPath in SearchPaths)
            {
                if (!Directory.Exists(searchPath))
                    continue;

                try
                {
                    foreach (var exeFile in Directory.GetFiles(searchPath, "*.exe", SearchOption.AllDirectories)
                        .Take(10000))
                    {
                        try
                        {
                            string fileName = Path.GetFileNameWithoutExtension(exeFile);
                            if (!foundFiles.ContainsKey(fileName))
                            {
                                foundFiles[fileName] = exeFile;
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }

        private static string FindFileByName(Dictionary<string, string> foundFiles, string appName)
        {
            if (string.IsNullOrEmpty(appName))
                return null;

            if (foundFiles.TryGetValue(appName, out string path))
                return path;

            var match = foundFiles.FirstOrDefault(f =>
                string.Equals(f.Key, appName, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(match.Key))
                return match.Value;

            return null;
        }

        public static async Task<bool> UpdateApplicationHash(WhiteListItem item, string newHash)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Загружаем текущий конфиг
                    var allItems = WhiteListManager.LoadFromConfig();

                    // Ищем элемент по имени (или по старому хешу)
                    var targetItem = allItems.FirstOrDefault(i =>
                        i.Name.Equals(item.Name, StringComparison.OrdinalIgnoreCase) ||
                        i.Hash.Equals(item.Hash, StringComparison.OrdinalIgnoreCase));

                    if (targetItem != null)
                    {
                        // Полностью заменяем старый хеш на новый
                        targetItem.Hash = newHash;

                        // Сохраняем обновленный список
                        bool saved = WhiteListManager.SaveConfig(allItems);

                        if (saved)
                        {
                            WriteDebug($"Хеш обновлен для {item.Name}: {item.Hash.Substring(0, 16)}... -> {newHash.Substring(0, 16)}...");
                            return true;
                        }
                    }
                    else
                    {
                        WriteDebug($"Приложение {item.Name} не найдено в белом списке");
                    }

                    return false;
                }
                catch (Exception ex)
                {
                    WriteDebug($"Ошибка обновления хеша: {ex.Message}");
                    return false;
                }
            });
        }

        private static void WriteDebug(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[HashChecker] {DateTime.Now:HH:mm:ss} {message}");
        }
    }
}