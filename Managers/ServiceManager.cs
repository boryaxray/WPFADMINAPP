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
                using (ServiceController sc = new ServiceController(ServiceName))
                {
                    return sc.Status.ToString();
                }
            }
            catch (InvalidOperationException)
            {
                return "Not Installed";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
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

        public static void InstallService(string logsPath = null, string whiteListPath = null)
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
        }

        // ДОБАВЛЯЕМ новый метод для настройки самовосстановления:
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

        // ДОБАВЛЯЕМ метод для выполнения SC команды:
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

        private static void RunServiceCommand(string arguments)
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
        }
        public static bool UninstallService()
        {
            try
            {
                if (!File.Exists(ServiceExePath))
                {
                    throw new FileNotFoundException($"Файл службы не найден: {ServiceExePath}");
                }

                // Используем Process.Start без ожидания завершения
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = ServiceExePath,
                    Arguments = "--uninstall",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    Verb = "runas" // Запуск от имени администратора
                };

                Process.Start(startInfo);

                // Не ждем завершения, чтобы не блокировать UI
                return true;
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                // Пользователь отменил UAC
                throw new Exception("Операция отменена пользователем");
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

      
    }
}