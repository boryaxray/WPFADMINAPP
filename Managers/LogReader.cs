using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;

namespace WPFAPP.Managers
{
    public static class LogReader
    {
        private static readonly string DefaultLogDir = @"C:\ProgramData\AppControl";
        private static string _logDirectory = DefaultLogDir;

        public static string GetLogDirectory()
        {
            try
            {
                string path = Properties.Settings.Default.LogsPath;
                if (!string.IsNullOrEmpty(path) && Directory.Exists(Path.GetDirectoryName(path)))
                {
                    return path;
                }

                // Путь по умолчанию
                string defaultPath = @"C:\ProgramData\AppControl\Logs";
                if (!Directory.Exists(defaultPath))
                {
                    Directory.CreateDirectory(defaultPath);
                }
                return defaultPath;
            }
            catch
            {
                return @"C:\ProgramData\AppControl\Logs";
            }
        }

        public static void SetLogDirectory(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path))
                {
                    Properties.Settings.Default.LogsPath = path;
                    Properties.Settings.Default.Save();
                }
            }
            catch { }
        }
        public static void OpenLogsInNotepad(string logType = "detailed")
        {
            try
            {
                string logPath = GetLogPath(logType);
                if (!File.Exists(logPath))
                {
                    throw new FileNotFoundException($"Лог-файл не найден: {logPath}");
                }

                Process.Start("notepad.exe", logPath);
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка открытия логов: {ex.Message}");
            }
        }

        public static void ClearLogs(string logType = "all")
        {
            try
            {
                if (logType == "all")
                {
                    string[] logFiles = {
                        Path.Combine(_logDirectory, "detailed.log"),
                        Path.Combine(_logDirectory, "service.log"),
                        Path.Combine(_logDirectory, "terminations.log")
                    };

                    foreach (var logFile in logFiles)
                    {
                        if (File.Exists(logFile))
                        {
                            File.WriteAllText(logFile, string.Empty);
                        }
                    }
                }
                else
                {
                    string logPath = GetLogPath(logType);
                    if (File.Exists(logPath))
                    {
                        File.WriteAllText(logPath, string.Empty);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка очистки логов: {ex.Message}");
            }
        }

        public static bool LogsExist(string logType = "detailed")
        {
            string logPath = GetLogPath(logType);
            return File.Exists(logPath);
        }

        public static string GetLogPath(string logType)
        {
            return logType switch
            {
                "service" => Path.Combine(_logDirectory, "service.log"),
                "terminations" => Path.Combine(_logDirectory, "terminations.log"),
                _ => Path.Combine(_logDirectory, "detailed.log") // detailed по умолчанию
            };
        }

        public static string[] GetAvailableLogs()
        {
            string[] logTypes = { "detailed.log", "service.log", "terminations.log" };
            return logTypes.Where(t => File.Exists(Path.Combine(_logDirectory, t)))
                          .Select(t => t.Replace(".log", ""))
                          .ToArray();
        }
    }
}