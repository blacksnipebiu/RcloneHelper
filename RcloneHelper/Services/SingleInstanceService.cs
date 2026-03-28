using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RcloneHelper.Services;

/// <summary>
/// 单实例服务，确保应用只有一个实例运行
/// 使用 Mutex 检测实例，Named Pipe 进行进程间通信
/// </summary>
public class SingleInstanceService : IDisposable
{
    // 应用唯一标识符 GUID
    private const string AppGuid = "A1B2C3D4-E5F6-7890-ABCD-EF1234567890";
    
    // Mutex 名称（使用 Global\ 前缀确保跨用户会话）
    private readonly string _mutexName = $"Global\\{{{AppGuid}}}";
    
    // Named Pipe 名称
    private readonly string _pipeName = $"RcloneHelper_{AppGuid}";
    
    private Mutex? _mutex;
    private bool _isFirstInstance;
    private bool _disposed;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _pipeServerTask;

    /// <summary>
    /// 是否为第一个实例
    /// </summary>
    public bool IsFirstInstance => _isFirstInstance;

    /// <summary>
    /// 接收到参数时触发
    /// </summary>
    public event Action<string[]>? ArgumentsReceived;

    /// <summary>
    /// 尝试启动单实例服务
    /// </summary>
    /// <returns>如果是第一个实例返回 true，否则返回 false</returns>
    public bool TryStart()
    {
        try
        {
            _mutex = new Mutex(true, _mutexName, out _isFirstInstance);
            
            if (_isFirstInstance)
            {
                // 第一个实例：启动 Named Pipe 服务器监听后续实例的参数
                StartPipeServer();
            }
            
            return _isFirstInstance;
        }
        catch (Exception ex)
        {
            // 如果创建 Mutex 失败，假设不是第一个实例
            System.Diagnostics.Debug.WriteLine($"创建 Mutex 失败: {ex.Message}");
            _isFirstInstance = false;
            return false;
        }
    }

    /// <summary>
    /// 发送参数给第一个实例
    /// </summary>
    /// <param name="args">要发送的参数</param>
    /// <returns>发送成功返回 true</returns>
    public bool SendArgumentsToFirstInstance(string[] args)
    {
        if (_isFirstInstance)
        {
            return false; // 第一个实例不需要发送给自己
        }

        try
        {
            using var pipe = new NamedPipeClientStream(
                ".",
                _pipeName,
                PipeDirection.Out,
                PipeOptions.CurrentUserOnly);

            // 等待连接，超时 500ms
            pipe.Connect(500);

            using var writer = new StreamWriter(pipe, Encoding.UTF8) { AutoFlush = true };
            
            // 写入参数数量
            writer.WriteLine(args.Length);
            
            // 写入每个参数
            foreach (var arg in args)
            {
                // 使用 Base64 编码避免特殊字符问题
                var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(arg));
                writer.WriteLine(encoded);
            }

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"发送参数失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 启动 Named Pipe 服务器
    /// </summary>
    private void StartPipeServer()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        _pipeServerTask = Task.Run(async () =>
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    using var pipe = new NamedPipeServerStream(
                        _pipeName,
                        PipeDirection.In,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                    // 等待客户端连接
                    await pipe.WaitForConnectionAsync(_cancellationTokenSource.Token);

                    if (_cancellationTokenSource.Token.IsCancellationRequested)
                        break;

                    // 读取参数
                    var args = await ReadArgumentsAsync(pipe);
                    
                    // 触发事件
                    if (args != null && args.Length > 0)
                    {
                        ArgumentsReceived?.Invoke(args);
                    }
                }
                catch (OperationCanceledException)
                {
                    // 正常退出
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Named Pipe 服务器错误: {ex.Message}");
                    
                    // 短暂等待后重试
                    await Task.Delay(100, _cancellationTokenSource.Token);
                }
            }
        }, _cancellationTokenSource.Token);
    }

    /// <summary>
    /// 从 Named Pipe 读取参数
    /// </summary>
    private async Task<string[]?> ReadArgumentsAsync(NamedPipeServerStream pipe)
    {
        try
        {
            using var reader = new StreamReader(pipe, Encoding.UTF8);
            
            // 读取参数数量
            var countLine = await reader.ReadLineAsync();
            if (countLine == null || !int.TryParse(countLine, out var count))
            {
                return null;
            }

            // 读取每个参数
            var args = new string[count];
            for (var i = 0; i < count; i++)
            {
                var encoded = await reader.ReadLineAsync();
                if (encoded == null)
                {
                    return null;
                }
                
                // Base64 解码
                args[i] = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            }

            return args;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"读取参数失败: {ex.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // 停止 Pipe 服务器
        _cancellationTokenSource?.Cancel();
        
        try
        {
            _pipeServerTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // 忽略等待异常
        }
        
        _cancellationTokenSource?.Dispose();

        // 释放 Mutex
        try
        {
            if (_mutex != null)
            {
                if (_isFirstInstance)
                {
                    _mutex.ReleaseMutex();
                }
                _mutex.Dispose();
            }
        }
        catch
        {
            // 忽略 Mutex 释放异常
        }
    }
}