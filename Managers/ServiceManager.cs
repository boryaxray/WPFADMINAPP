using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Text;
using System.Windows;

namespace WPFAPP.Managers
{
    public static class ServiceManager
    {
        private static readonly string ServiceExePath = "ApplicationControlService.exe";
        private static readonly string ServiceName = "AppControlService";
        private static string _cachedStatus = "Unknown";
        private static DateTime _lastStatusCheck = DateTime.MinValue;
        private static readonly TimeSpan _cacheDuration = TimeSpan.FromSeconds(2);
        public static void SetConfigPath(string logsPath, string whiteListPath)
        {
            try
            {
                if (!string.IsNullOrEmpty(logsPath))
                {
                    Properties.Settings.Default.LogsPath = logsPath;
                }

                if (!string.IsNullOrEmpty(whiteListPath))
                {
                    Properties.Settings.Default.WhiteListPath = whiteListPath;
                }

                Properties.Settings.Default.Save();
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка сохранения путей: {ex.Message}", ex);
            }
        }
        public static string GetServiceStatus()
        {
            try
            {
                // Проверяем через SC QUERY (самый надежный способ)
                string scOutput = RunScQuery();

                if (string.IsNullOrEmpty(scOutput))
                    return "Not Installed";

                if (scOutput.Contains("STATE") && scOutput.Contains("RUNNING"))
                    return "Running";

                if (scOutput.Contains("STATE") && scOutput.Contains("STOPPED"))
                    return "Stopped";

                if (scOutput.Contains("OpenService FAILED") || scOutput.Contains("1060"))
                    return "Not Installed";

                // Альтернатива - проверка через ServiceController
                try
                {
                    using (var sc = new ServiceController(ServiceName))
                    {
                        sc.Refresh();
                        return sc.Status.ToString();
                    }
                }
                catch (InvalidOperationException)
                {
                    return "Not Installed";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetServiceStatus error: {ex.Message}");
                return "Error";
            }
        }
        private static string RunScQuery()
        {
            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = "sc.exe";
                    process.StartInfo.Arguments = $"query {ServiceName}";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.StandardOutputEncoding = Encoding.GetEncoding(866);

                    process.Start();
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(3000);

                    return output;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RunScQuery error: {ex.Message}");
                return string.Empty;
            }
        }

        /*        public static void InstallService(string logsPath = null, string whiteListPath = null)
                {
                    try
                    {
                        // Если пути не указаны - берем из настроек
                        if (string.IsNullOrEmpty(logsPath))
                        {
                            logsPath = Properties.Settings.Default.LogsPath;
                            if (string.IsNullOrEmpty(logsPath))
                                logsPath = @"C:\ProgramData\AppControl\Logs";
                        }

                        if (string.IsNullOrEmpty(whiteListPath))
                        {
                            whiteListPath = Properties.Settings.Default.WhiteListPath;
                            if (string.IsNullOrEmpty(whiteListPath))
                                whiteListPath = @"C:\ProgramData\AppControl\WhiteList";
                        }

                        // ФИКС: Убираем кавычки и пробелы
                        logsPath = logsPath?.Trim().Trim('"');
                        whiteListPath = whiteListPath?.Trim().Trim('"');

                        // Сохраняем пути в настройках
                        Properties.Settings.Default.LogsPath = logsPath;
                        Properties.Settings.Default.WhiteListPath = whiteListPath;
                        Properties.Settings.Default.Save();

                        // передаем пути службе
                        string args = $"--install \"{logsPath}\" \"{whiteListPath}\"";

                        RunServiceCommand(args);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Ошибка установки службы: {ex.Message}", ex);
                    }
                }*/

        // В файле ServiceManager.cs добавьте/измените метод InstallService:

        /*public static void InstallService(string logsPath = null, string whiteListPath = null)
        {
            try
            {
                // Если пути не указаны - берем из настроек
                if (string.IsNullOrEmpty(logsPath))
                {
                    logsPath = Properties.Settings.Default.LogsPath;
                    if (string.IsNullOrEmpty(logsPath))
                        logsPath = @"C:\ProgramData\AppControl\Logs";
                }

                if (string.IsNullOrEmpty(whiteListPath))
                {
                    whiteListPath = Properties.Settings.Default.WhiteListPath;
                    if (string.IsNullOrEmpty(whiteListPath))
                        whiteListPath = @"C:\ProgramData\AppControl\WhiteList";
                }

                // ФИКС: Убираем кавычки и пробелы
                logsPath = logsPath?.Trim().Trim('"');
                whiteListPath = whiteListPath?.Trim().Trim('"');

                // Сохраняем пути в настройках
                Properties.Settings.Default.LogsPath = logsPath;
                Properties.Settings.Default.WhiteListPath = whiteListPath;
                Properties.Settings.Default.Save();

                // передаем пути службе
                string args = $"--install \"{logsPath}\" \"{whiteListPath}\"";

                RunServiceCommand(args);

                // ДОБАВЛЯЕМ: После установки службы настраиваем самовосстановление
                ConfigureServiceRecovery();
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка установки службы: {ex.Message}", ex);
            }
        }*/

        public static void InstallService(string logsPath = null, string whiteListPath = null)
        {
            try
            {
                if (string.IsNullOrEmpty(logsPath))
                {
                    logsPath = Properties.Settings.Default.LogsPath;
                    if (string.IsNullOrEmpty(logsPath))
                        logsPath = @"C:\ProgramData\AppControl\Logs";
                }

                if (string.IsNullOrEmpty(whiteListPath))
                {
                    whiteListPath = Properties.Settings.Default.WhiteListPath;
                    if (string.IsNullOrEmpty(whiteListPath))
                        whiteListPath = @"C:\ProgramData\AppControl\WhiteList";
                }

                logsPath = logsPath?.Trim().Trim('"');
                whiteListPath = whiteListPath?.Trim().Trim('"');

                Properties.Settings.Default.LogsPath = logsPath;
                Properties.Settings.Default.WhiteListPath = whiteListPath;
                Properties.Settings.Default.Save();

                string args = $"--install \"{logsPath}\" \"{whiteListPath}\"";
                RunServiceCommand(args);
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка установки службы: {ex.Message}", ex);
            }
        }


        private static void ConfigureServiceRecovery()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Настройка самовосстановления службы...");

                // Команда для настройки восстановления при сбоях
                // reset= 0 - счетчик сбоев никогда не сбрасывается
                // actions= restart/5000/restart/5000/restart/5000 - 3 попытки перезапуска через 5 секунд
                string recoveryCommand = $"failure AppControlService reset= 86400 actions= restart/5000/restart/5000/restart/5000";

                RunSCCommand(recoveryCommand, "настройка восстановления");

                // Включаем флаг восстановления при аварийном завершении
                string failureFlagCommand = $"failureflag AppControlService 1";
                RunSCCommand(failureFlagCommand, "включение восстановления");

                System.Diagnostics.Debug.WriteLine("Самовосстановление настроено успешно");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка настройки самовосстановления: {ex.Message}");
                // Не бросаем исключение, чтобы не сломать установку
            }
        }

        private static bool RunSCCommand(string arguments, string operationName)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss}] sc.exe {arguments}");

                using (Process process = new Process())
                {
                    process.StartInfo.FileName = "sc.exe";
                    process.StartInfo.Arguments = arguments;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;

                    // Важно использовать кодировку 866 для русской консоли
                    process.StartInfo.StandardOutputEncoding = System.Text.Encoding.GetEncoding(866);
                    process.StartInfo.StandardErrorEncoding = System.Text.Encoding.GetEncoding(866);

                    StringBuilder output = new StringBuilder();
                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            output.AppendLine(e.Data);
                            System.Diagnostics.Debug.WriteLine($"  {e.Data}");
                        }
                    };

                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            System.Diagnostics.Debug.WriteLine($"  [ОШИБКА] {e.Data}");
                        }
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    process.WaitForExit(15000); // 15 секунд

                    System.Diagnostics.Debug.WriteLine($"  Код выхода: {process.ExitCode}");

                    // Коды успеха для sc.exe
                    if (process.ExitCode == 0 || process.ExitCode == 1073 || output.ToString().Contains("SUCCESS"))
                    {
                        System.Diagnostics.Debug.WriteLine($"  ✓ {operationName} - УСПЕШНО");
                        return true;
                    }

                    System.Diagnostics.Debug.WriteLine($"  ✗ {operationName} - НЕУДАЧА (код: {process.ExitCode})");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"  ✗ Ошибка при {operationName}: {ex.Message}");
                return false;
            }
        }

        /*private static void RunServiceCommand(string arguments)
        {
            try
            {
                Process process = new Process();
                process.StartInfo.FileName = GetServiceExePath();
                process.StartInfo.Arguments = arguments;
                process.StartInfo.UseShellExecute = true;
                process.StartInfo.Verb = "runas"; // Запуск от имени администратора
                process.StartInfo.WindowStyle = ProcessWindowStyle.Normal;

                process.Start();

                if (!process.WaitForExit(60000)) // 60 секунд
                {
                    process.Kill();
                    throw new Exception("Процесс не завершился вовремя");
                }

                if (process.ExitCode != 0)
                {
                    // Код 1 обычно означает проблемы с аргументами
                    throw new Exception($"Ошибка установки службы (код: {process.ExitCode})");
                }
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                // Пользователь отменил UAC
                throw new Exception("Операция отменена пользователем");
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка выполнения команды: {ex.Message}");
            }
        }*/

        private static void RunServiceCommand(string arguments)
        {
            try
            {
                string exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ApplicationControlService.exe");

                if (!File.Exists(exePath))
                {
                    throw new FileNotFoundException($"Файл службы не найден: {exePath}");
                }

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = arguments,
                    UseShellExecute = true,
                    Verb = "runas"
                };

                using (Process process = Process.Start(startInfo))
                {
                    if (process != null)
                    {
                        process.WaitForExit(60000);
                        if (process.ExitCode != 0)
                        {
                            throw new Exception($"Ошибка (код: {process.ExitCode})");
                        }
                    }
                }
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                throw new Exception("Операция отменена пользователем");
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка: {ex.Message}");
            }
        }
        public static bool UninstallService()
        {
            try
            {
                // 1. Сначала останавливаем службу
                try
                {
                    using (ServiceController sc = new ServiceController(ServiceName))
                    {
                        if (sc.Status == ServiceControllerStatus.Running)
                        {
                            Console.WriteLine("Остановка службы...");
                            sc.Stop();
                            sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка остановки: {ex.Message}");
                    // Пробуем через taskkill
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "taskkill.exe",
                        Arguments = "/F /IM ApplicationControlService.exe",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    })?.WaitForExit(5000);
                }

                // 2. Удаляем службу
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = "sc.exe";
                    process.StartInfo.Arguments = $"delete {ServiceName}";
                    process.StartInfo.UseShellExecute = true;
                    process.StartInfo.Verb = "runas";
                    process.StartInfo.CreateNoWindow = true;

                    process.Start();
                    process.WaitForExit(10000);

                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка удаления службы: {ex.Message}");
            }
        }

        public static bool StartService()
        {
            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = "sc.exe";
                    process.StartInfo.Arguments = "start AppControlService";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardOutput = true;

                    process.Start();
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(10000);

                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка запуска службы: {ex.Message}");
            }
        }

        public static bool StopService()
        {
            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = "sc.exe";
                    process.StartInfo.Arguments = "stop AppControlService";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardOutput = true;

                    process.Start();
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(10000);

                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка остановки службы: {ex.Message}");
            }
        }

        public static string GetServiceRecoveryInfo()
        {
            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = "sc.exe";
                    process.StartInfo.Arguments = "qfailure AppControlService";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.StandardOutputEncoding = System.Text.Encoding.GetEncoding(866);

                    process.Start();
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(5000);

                    return output;
                }
            }
            catch (Exception ex)
            {
                return $"Ошибка получения информации: {ex.Message}";
            }
        }


        public static void ClearAllLogs()
        {
            try
            {
                string logDir = GetLogDirectory();

                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                string[] logFiles = {
                    "detailed.log",
                    "service.log",
                    "terminations.log",
                    "compact.log",
                    "crash.log"
                };

                int cleared = 0;
                foreach (var logFile in logFiles)
                {
                    string fullPath = Path.Combine(logDir, logFile);
                    if (File.Exists(fullPath))
                    {
                        File.WriteAllText(fullPath, string.Empty);
                        cleared++;
                    }
                }

                Debug.WriteLine($"Cleared {cleared} log files in {logDir}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Clear logs error: {ex.Message}");
                throw;
            }
        }

        public static string GetLogDirectory()
        {
            try
            {
                // Сначала пробуем из настроек
                string path = Properties.Settings.Default.LogsPath;
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
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

        public static bool OpenLogsInNotepad()
        {
            try
            {
                string logDir = GetLogDirectory();
                string logPath = Path.Combine(logDir, "detailed.log");

                if (!File.Exists(logPath))
                {
                    // Создаем пустой файл если нет
                    Directory.CreateDirectory(logDir);
                    File.WriteAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Лог-файл создан\n");
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = logPath,
                    UseShellExecute = true
                });
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Open logs error: {ex.Message}");
                return false;
            }
        }

        public static void StartDebugMode(string logsPath = null, string whiteListPath = null)
        {
            try
            {
                string serviceExePath = GetServiceExePath();
                if (!File.Exists(serviceExePath))
                {
                    throw new FileNotFoundException($"Файл службы не найден: {serviceExePath}");
                }

                // Если пути не переданы, берем из настроек
                if (string.IsNullOrEmpty(logsPath))
                {
                    logsPath = Properties.Settings.Default.LogsPath;
                }

                if (string.IsNullOrEmpty(whiteListPath))
                {
                    whiteListPath = Properties.Settings.Default.WhiteListPath;
                }

                string args = $"--debug \"{logsPath}\" \"{whiteListPath}\"";
                Process.Start(serviceExePath, args);
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка запуска режима отладки: {ex.Message}", ex);
            }
        }
        private static string GetServiceExePath()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(appDir, "ApplicationControlService.exe");
        }





        public static bool StopServiceWithAuth(string password)
        {
            try
            {
                // Проверяем пароль (должен быть настроен администратором)
                if (!ValidateAdminPassword(password))
                {
                    throw new Exception("Неверный пароль администратора");
                }

                // Разрешаем остановку службы
                AllowServiceStop();

                // Останавливаем службу
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = "sc.exe";
                    process.StartInfo.Arguments = "stop AppControlService";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.Verb = "runas";

                    process.Start();
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(10000);

                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка остановки службы: {ex.Message}");
            }
            finally
            {
                // Запрещаем остановку
                DenyServiceStop();
            }
        }

        private static bool ValidateAdminPassword(string password)
        {
            // Получаем сохраненный хеш пароля
            string savedHash = Properties.Settings.Default.AdminPasswordHash;

            if (string.IsNullOrEmpty(savedHash))
            {
                // Первый запуск - сохраняем пароль
                string newHash = ComputeHash(password);
                Properties.Settings.Default.AdminPasswordHash = newHash;
                Properties.Settings.Default.Save();
                return true;
            }

            // Проверяем пароль
            string inputHash = ComputeHash(password);
            return inputHash == savedHash;
        }

        private static string ComputeHash(string input)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(input + "AppControlSalt123");
                byte[] hash = sha256.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }

        // Установка пароля администратора
        public static bool SetAdminPassword(string newPassword)
        {
            try
            {
                string hash = ComputeHash(newPassword);
                Properties.Settings.Default.AdminPasswordHash = hash;
                Properties.Settings.Default.Save();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void AllowServiceStop()
        {
            try
            {
                // Отправляем сигнал службе разрешить остановку
                using (var sc = new ServiceController("AppControlService"))
                {
                    sc.ExecuteCommand(128); // Пользовательская команда 128
                }
            }
            catch { }
        }

        private static void DenyServiceStop()
        {
            try
            {
                using (var sc = new ServiceController("AppControlService"))
                {
                    sc.ExecuteCommand(129); // Пользовательская команда 129
                }
            }
            catch { }
        }

    }
}