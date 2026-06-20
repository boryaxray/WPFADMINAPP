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
        // ЕДИНОЕ ИМЯ СЛУЖБЫ
        private static readonly string ServiceName = "AppControlService";
        private static readonly string ServiceExeName = "ApplicationControlService.exe";

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
                // Проверяем наличие процесса службы
                bool processExists = Process.GetProcessesByName("ApplicationControlService").Length > 0;

                using (ServiceController sc = new ServiceController(ServiceName))
                {
                    string status = sc.Status.ToString();
                    return status;
                }
            }
            catch
            {
                // Проверяем, может быть процесс есть, но служба не зарегистрирована
                bool processExists = Process.GetProcessesByName("ApplicationControlService").Length > 0;
                if (processExists)
                {
                    return "Running (orphan)";
                }
                return "Not Installed";
            }
        }

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

        public static bool UninstallService()
        {
            try
            {
                Debug.WriteLine("=== ПРИНУДИТЕЛЬНОЕ УДАЛЕНИЕ СЛУЖБЫ ===");

                // 1. Сначала принудительно завершаем процесс
                foreach (var proc in Process.GetProcessesByName("ApplicationControlService"))
                {
                    try
                    {
                        proc.Kill();
                        proc.WaitForExit(5000);
                        Debug.WriteLine($"Процесс завершен, PID: {proc.Id}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Ошибка завершения: {ex.Message}");
                    }
                }

                Thread.Sleep(3000);

                // 2. Пробуем остановить службу
                try
                {
                    using (Process process = new Process())
                    {
                        process.StartInfo.FileName = "sc.exe";
                        process.StartInfo.Arguments = $"stop {ServiceName}";
                        process.StartInfo.UseShellExecute = true;
                        process.StartInfo.Verb = "runas";
                        process.StartInfo.CreateNoWindow = true;
                        process.Start();
                        process.WaitForExit(10000);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка sc stop: {ex.Message}");
                }

                Thread.Sleep(2000);

                // 3. Удаляем службу
                try
                {
                    using (Process process = new Process())
                    {
                        process.StartInfo.FileName = "sc.exe";
                        process.StartInfo.Arguments = $"delete {ServiceName}";
                        process.StartInfo.UseShellExecute = true;
                        process.StartInfo.Verb = "runas";
                        process.StartInfo.CreateNoWindow = true;
                        process.Start();
                        process.WaitForExit(10000);
                        Debug.WriteLine($"SC delete exit code: {process.ExitCode}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка sc delete: {ex.Message}");
                }

                // 4. Удаляем ключ реестра
                try
                {
                    Microsoft.Win32.Registry.LocalMachine.DeleteSubKeyTree(
                        $@"SYSTEM\CurrentControlSet\Services\{ServiceName}");
                    Debug.WriteLine("Ключ реестра удален");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка удаления реестра: {ex.Message}");
                }

                // 5. Еще раз проверяем процесс
                Thread.Sleep(1000);
                foreach (var proc in Process.GetProcessesByName("ApplicationControlService"))
                {
                    try
                    {
                        proc.Kill();
                        Debug.WriteLine($"Повторно завершен процесс");
                    }
                    catch { }
                }

                Debug.WriteLine("Служба удалена");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка удаления: {ex.Message}");
                return false;
            }
        }

        public static bool StartService()
        {
            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = "sc.exe";
                    process.StartInfo.Arguments = $"start {ServiceName}";
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
                    process.StartInfo.Arguments = $"stop {ServiceName}";
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
                    process.StartInfo.Arguments = $"qfailure {ServiceName}";
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

        public static bool ForceStopService()
        {
            try
            {
                Debug.WriteLine("Принудительная остановка службы...");

                bool processKilled = false;
                foreach (var proc in Process.GetProcessesByName("ApplicationControlService"))
                {
                    try
                    {
                        proc.Kill();
                        proc.WaitForExit(5000);
                        Debug.WriteLine($"Завершен процесс PID: {proc.Id}");
                        processKilled = true;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Ошибка завершения процесса: {ex.Message}");
                    }
                }

                try
                {
                    using (Process process = new Process())
                    {
                        process.StartInfo.FileName = "sc.exe";
                        process.StartInfo.Arguments = $"stop {ServiceName}";
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.CreateNoWindow = true;
                        process.StartInfo.RedirectStandardOutput = true;
                        process.Start();
                        process.WaitForExit(10000);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка остановки службы: {ex.Message}");
                }

                return processKilled;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ForceStopService error: {ex.Message}");
                return false;
            }
        }

        public static bool IsServiceProcessRunning()
        {
            try
            {
                return Process.GetProcessesByName("ApplicationControlService").Length > 0;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsServiceInstalled()
        {
            try
            {
                using (ServiceController sc = new ServiceController(ServiceName))
                {
                    return true;
                }
            }
            catch
            {
                return false;
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
                string path = Properties.Settings.Default.LogsPath;
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    return path;
                }

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
            string exePath = Path.Combine(appDir, "ApplicationControlService.exe");

            if (!File.Exists(exePath))
            {
                string parentDir = Path.GetDirectoryName(appDir);
                if (!string.IsNullOrEmpty(parentDir))
                {
                    exePath = Path.Combine(parentDir, "ApplicationControlService.exe");
                }
            }

            return exePath;
        }

        private static void RunServiceCommand(string arguments)
        {
            try
            {
                string exePath = GetServiceExePath();

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

        public static void AllowServiceStop()
        {
            try
            {
                using (var sc = new ServiceController(ServiceName))
                {
                    sc.ExecuteCommand(128);
                    Debug.WriteLine("Команда разрешения остановки отправлена службе");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка отправки команды: {ex.Message}");
            }
        }

        private static void DenyServiceStop()
        {
            try
            {
                using (var sc = new ServiceController(ServiceName))
                {
                    sc.ExecuteCommand(129);
                }
            }
            catch { }
        }

        public static bool StopServiceWithAuth(string password)
        {
            try
            {
                if (!ValidateAdminPassword(password))
                {
                    throw new Exception("Неверный пароль администратора");
                }

                AllowServiceStop();

                using (Process process = new Process())
                {
                    process.StartInfo.FileName = "sc.exe";
                    process.StartInfo.Arguments = $"stop {ServiceName}";
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
                DenyServiceStop();
            }
        }

        private static bool ValidateAdminPassword(string password)
        {
            string savedHash = Properties.Settings.Default.AdminPasswordHash;

            if (string.IsNullOrEmpty(savedHash))
            {
                string newHash = ComputeHash(password);
                Properties.Settings.Default.AdminPasswordHash = newHash;
                Properties.Settings.Default.Save();
                return true;
            }

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

        public static bool RestoreServiceFile()
        {
            try
            {
                string serviceDir = GetServiceDirectory();
                string backupPath = Path.Combine(serviceDir, "~$backup.exe");
                string servicePath = Path.Combine(serviceDir, "ApplicationControlService.exe");

                if (File.Exists(backupPath) && !File.Exists(servicePath))
                {
                    File.Copy(backupPath, servicePath);
                    File.SetAttributes(servicePath, FileAttributes.ReadOnly | FileAttributes.System);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public static string GetServiceDirectory()
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule.FileName;
                return Path.GetDirectoryName(exePath);
            }
            catch
            {
                return AppDomain.CurrentDomain.BaseDirectory;
            }
        }

    }
}