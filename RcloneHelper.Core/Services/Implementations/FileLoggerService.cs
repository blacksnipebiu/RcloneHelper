using System;
using System.IO;
using System.Text;
using RcloneHelper.Helpers;
using RcloneHelper.Services.Abstractions;

namespace RcloneHelper.Services.Implementations;

/// <summary>
/// 基于文件的滚动日志服务
/// </summary>
public class FileLoggerService : ILoggerService
{
    private readonly object _lock = new();
    private readonly string _logDir;
    private const int RetainDays = 7;

    public FileLoggerService()
    {
        _logDir = PathUtil.LogPath;
        Directory.CreateDirectory(_logDir);
        CleanupOldLogs();
    }

    private string LogPathForDate(DateTime date) => Path.Combine(_logDir, $"app_{date:yyyy-MM-dd}.log");

    private void CleanupOldLogs()
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-RetainDays);
            foreach (var file in Directory.GetFiles(_logDir, "app_*.log"))
                if (File.GetCreationTime(file) < cutoff)
                    File.Delete(file);
        }
        catch { }
    }

    public void Log(LogLevel level, string message)
    {
        var logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
        lock (_lock)
        {
            try { File.AppendAllText(LogPathForDate(DateTime.Now), logLine + Environment.NewLine, Encoding.UTF8); }
            catch { }
        }
    }

    public void Debug(string message) => Log(LogLevel.Debug, message);
    public void Info(string message) => Log(LogLevel.Info, message);
    public void Warning(string message) => Log(LogLevel.Warning, message);
    public void Error(string message) => Log(LogLevel.Error, message);
}
