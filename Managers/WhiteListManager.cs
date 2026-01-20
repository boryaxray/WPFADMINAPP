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
                // ВСЕГДА берем путь из настроек админ-утилиты
                string path = Properties.Settings.Default.WhiteListPath;

                // Если путь не указан в настройках, используем путь по умолчанию
                if (string.IsNullOrEmpty(path))
                {
                    path = @"C:\ProgramData\AppControl\WhiteList";
                    Properties.Settings.Default.WhiteListPath = path;
                    Properties.Settings.Default.Save();
                }

                // Создаем директорию если не существует
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

                    // Сбрасываем кэшированный путь
                    _configPath = null;
                }
            }
            catch { }
        }

        public static string GetActiveConfigPath()
        {
            // Просто комбинируем директорию с именем файла
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

                // Читаем файл
                string json = File.ReadAllText(configPath, Encoding.UTF8);

                // Убираем BOM если есть
                json = json.Trim(new char[] { '\uFEFF', '\u200B' }).Trim();

                if (string.IsNullOrEmpty(json) || json == "[]" || json == "{}")
                {
                    WriteDebug("Конфиг пустой");
                    return new List<WhiteListItem>();
                }

                // Парсим с DataContractJsonSerializer
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

                    // Проверяем валидность
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

                // Создаем директорию если не существует
                string configDir = Path.GetDirectoryName(configPath);
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                    WriteDebug($"Создана директория: {configDir}");
                }

                // Сериализуем с DataContractJsonSerializer
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

                    // Преобразуем в читаемый JSON
                    string json = Encoding.UTF8.GetString(ms.ToArray());

                    // Простое форматирование
                    json = FormatJson(json);

                    // Сохраняем без BOM
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
            // Для отладки можно записывать в файл или использовать Debug.WriteLine
            System.Diagnostics.Debug.WriteLine($"[WhiteListManager] {DateTime.Now:HH:mm:ss} {message}");

            // Или записывать в лог-файл админ-утилиты
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
                // Простейшее форматирование
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
                // Устанавливаем путь если указан
                if (!string.IsNullOrEmpty(configDir))
                {
                    SetConfigPath(configDir);
                }

                // 1. Проверяем существование файла
                if (!File.Exists(filePath))
                {
                    result.Error = "Файл не существует";
                    result.Success = false;
                    return result;
                }

                // 2. Проверяем, не системный ли файл
                if (HashUtils.IsSystemFile(filePath))
                {
                    result.Error = "Нельзя добавлять системные файлы в белый список";
                    result.Success = false;
                    return result;
                }

                // 3. Вычисляем хэш
                result.Hash = HashUtils.CalculateSHA256(filePath);

                if (string.IsNullOrEmpty(result.Hash))
                {
                    result.Error = "Не удалось вычислить хэш файла";
                    result.Success = false;
                    return result;
                }

                // 4. Проверяем формат хэша
                if (!HashUtils.ValidateHash(result.Hash))
                {
                    result.Error = $"Некорректный формат хэша (длина: {result.Hash.Length})";
                    result.Success = false;
                    return result;
                }

                // 5. Загружаем текущий конфиг
                var currentItems = LoadFromConfig();

                // 6. Проверяем, нет ли уже этого хэша
                if (currentItems.Any(item => item.Hash.Equals(result.Hash, StringComparison.OrdinalIgnoreCase)))
                {
                    result.Error = "Приложение уже находится в белом списке";
                    result.Success = false;
                    return result;
                }

                // 7. Добавляем новое приложение
                string appName = Path.GetFileNameWithoutExtension(filePath);
                if (string.IsNullOrEmpty(appName))
                {
                    appName = "Unknown Application";
                }

                var newItem = new WhiteListItem(appName, result.Hash);
                currentItems.Add(newItem);

                // 8. Сохраняем обновленный конфиг
                if (SaveConfig(currentItems))
                {
                    // 9. Если служба работает - перезагружаем её конфиг
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
                // Устанавливаем путь если указан
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
                // Игнорируем ошибки перезагрузки службы
            }
        }
    }
}