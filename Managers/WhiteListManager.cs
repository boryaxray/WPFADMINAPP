using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.ServiceProcess;
using WPFAPP.Pages;
using WPFAPP.Utils;

namespace WPFAPP.Managers
{
    public class AddApplicationResult
    {
        public bool Success { get; set; }
        public string Hash { get; set; }
        public string Error { get; set; }

        public AddApplicationResult()
        {
            Hash = string.Empty;
            Error = string.Empty;
        }
    }

    public static class WhiteListManager
    {
        private static readonly string DefaultConfigDir = @"C:\ProgramData\AppControl\WhiteList";
        private static string _configPath = null;

        public static string GetConfigDirectory()
        {
            try
            {
                string path = Properties.Settings.Default.WhiteListPath;

                if (string.IsNullOrEmpty(path))
                {
                    path = @"C:\ProgramData\AppControl\WhiteList";
                    Properties.Settings.Default.WhiteListPath = path;
                    Properties.Settings.Default.Save();
                }

                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                return path;
            }
            catch
            {
                return @"C:\ProgramData\AppControl\WhiteList";
            }
        }

        public static void SetConfigPath(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path))
                {
                    Properties.Settings.Default.WhiteListPath = path;
                    Properties.Settings.Default.Save();
                    _configPath = null;
                }
            }
            catch { }
        }

        public static string GetActiveConfigPath()
        {
            string configDir = GetConfigDirectory();
            return Path.Combine(configDir, "config.json");
        }

        public static List<WhiteListItem> LoadFromConfig()
        {
            try
            {
                string configPath = GetActiveConfigPath();

                WriteDebug($"Загружаем конфиг из: {configPath}");

                if (!File.Exists(configPath))
                {
                    WriteDebug($"Файл конфига не найден, создаем пустой: {configPath}");
                    SaveConfig(new List<WhiteListItem>());
                    return new List<WhiteListItem>();
                }

                string json = File.ReadAllText(configPath, Encoding.UTF8);
                json = json.Trim(new char[] { '\uFEFF', '\u200B' }).Trim();

                if (string.IsNullOrEmpty(json) || json == "[]" || json == "{}")
                {
                    WriteDebug("Конфиг пустой");
                    return new List<WhiteListItem>();
                }

                using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(
                        typeof(List<WhiteListItem>),
                        new DataContractJsonSerializerSettings
                        {
                            UseSimpleDictionaryFormat = true,
                            EmitTypeInformation = System.Runtime.Serialization.EmitTypeInformation.Never
                        }
                    );

                    var items = (List<WhiteListItem>)serializer.ReadObject(ms) ?? new List<WhiteListItem>();
                    var validItems = items.Where(item => item.IsValid()).ToList();

                    WriteDebug($"Загружено {validItems.Count} валидных записей из {items.Count} всего");

                    return validItems;
                }
            }
            catch (Exception ex)
            {
                WriteDebug($"Ошибка загрузки конфига: {ex.Message}");
                return new List<WhiteListItem>();
            }
        }

        public static bool SaveConfig(List<WhiteListItem> items)
        {
            try
            {
                string configPath = GetActiveConfigPath();

                WriteDebug($"Сохраняем конфиг в: {configPath}");

                string configDir = Path.GetDirectoryName(configPath);
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                    WriteDebug($"Создана директория: {configDir}");
                }

                using (MemoryStream ms = new MemoryStream())
                {
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(
                        typeof(List<WhiteListItem>),
                        new DataContractJsonSerializerSettings
                        {
                            UseSimpleDictionaryFormat = true,
                            EmitTypeInformation = System.Runtime.Serialization.EmitTypeInformation.Never
                        }
                    );

                    serializer.WriteObject(ms, items);
                    ms.Position = 0;

                    string json = Encoding.UTF8.GetString(ms.ToArray());
                    json = FormatJson(json);

                    File.WriteAllText(configPath, json, new UTF8Encoding(false));

                    WriteDebug($"Сохранено {items.Count} записей");
                    return true;
                }
            }
            catch (Exception ex)
            {
                WriteDebug($"Ошибка сохранения конфига: {ex.Message}");
                return false;
            }
        }

        private static void WriteDebug(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[WhiteListManager] {DateTime.Now:HH:mm:ss} {message}");

            try
            {
                string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "AppControl", "admin_util.log");
                string logDir = Path.GetDirectoryName(logPath);

                if (!Directory.Exists(logDir))
                    Directory.CreateDirectory(logDir);

                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
            }
            catch { }
        }

        private static string FormatJson(string json)
        {
            try
            {
                json = json.Replace("{\"", "{\n  \"")
                          .Replace(",\"", ",\n  \"")
                          .Replace("}]", "}\n]")
                          .Replace("},{", "},\n  {");
                return json;
            }
            catch
            {
                return json;
            }
        }

        public static AddApplicationResult AddApplication(string filePath, string configDir = null)
        {
            var result = new AddApplicationResult();

            try
            {
                if (!string.IsNullOrEmpty(configDir))
                {
                    SetConfigPath(configDir);
                }

                if (!File.Exists(filePath))
                {
                    result.Error = "Файл не существует";
                    result.Success = false;
                    return result;
                }

                if (HashUtils.IsSystemFile(filePath))
                {
                    result.Error = "Нельзя добавлять системные файлы в белый список";
                    result.Success = false;
                    return result;
                }

                result.Hash = HashUtils.CalculateSHA256(filePath);

                if (string.IsNullOrEmpty(result.Hash))
                {
                    result.Error = "Не удалось вычислить хэш файла";
                    result.Success = false;
                    return result;
                }

                if (!HashUtils.ValidateHash(result.Hash))
                {
                    result.Error = $"Некорректный формат хэша (длина: {result.Hash.Length})";
                    result.Success = false;
                    return result;
                }

                var currentItems = LoadFromConfig();

                if (currentItems.Any(item => item.Hash.Equals(result.Hash, StringComparison.OrdinalIgnoreCase)))
                {
                    result.Error = "Приложение уже находится в белом списке";
                    result.Success = false;
                    return result;
                }

                string appName = Path.GetFileNameWithoutExtension(filePath);
                if (string.IsNullOrEmpty(appName))
                {
                    appName = "Unknown Application";
                }

                var newItem = new WhiteListItem(appName, result.Hash);
                currentItems.Add(newItem);

                if (SaveConfig(currentItems))
                {
                    if (IsServiceRunning())
                    {
                        ReloadServiceConfig();
                    }

                    result.Success = true;
                    return result;
                }
                else
                {
                    result.Error = "Не удалось сохранить конфигурацию";
                    result.Success = false;
                    return result;
                }
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                result.Success = false;
                return result;
            }
        }

        public static bool RemoveApplications(List<WhiteListItem> itemsToRemove, string configDir = null)
        {
            try
            {
                if (!string.IsNullOrEmpty(configDir))
                {
                    SetConfigPath(configDir);
                }

                var allItems = LoadFromConfig();
                var itemsToKeep = allItems.Where(item =>
                    !itemsToRemove.Any(toRemove =>
                        toRemove.Hash.Equals(item.Hash, StringComparison.OrdinalIgnoreCase))).ToList();

                bool saved = SaveConfig(itemsToKeep);

                if (saved && IsServiceRunning())
                {
                    ReloadServiceConfig();
                }

                return saved;
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка удаления приложений: {ex.Message}");
            }
        }

        public static string SelectFile()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Исполняемые файлы (*.exe)|*.exe|Все файлы (*.*)|*.*";
            openFileDialog.Title = "Выберите приложение для добавления в белый список";
            openFileDialog.CheckFileExists = true;

            if (openFileDialog.ShowDialog() == true)
            {
                return openFileDialog.FileName;
            }

            return null;
        }

        private static bool IsServiceRunning()
        {
            try
            {
                using (var sc = new ServiceController("AppControlService"))
                {
                    return sc.Status == ServiceControllerStatus.Running;
                }
            }
            catch
            {
                return false;
            }
        }

        private static void ReloadServiceConfig()
        {
            try
            {
                using (var sc = new ServiceController("AppControlService"))
                {
                    if (sc.Status == ServiceControllerStatus.Running)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                    }
                }
            }
            catch
            {
               
            }
        }
    }
}