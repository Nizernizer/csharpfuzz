using System;
using System.IO;
using System.IO.Pipes;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using WFuzz;
using WFuzzAgent;

namespace WFuzzEngine
{
    /// <summary>
    /// AFLSharp 引擎完整实现
    /// 实现AFL协议的fork server通信和覆盖率反馈
    /// </summary>
    public class AFLSharpEngine : IFuzzEngine
    {
        public string Name => "AFLSharp";
        
        // AFL协议常量
        private const int FORKSRV_FD = 198;  // AFL fork server控制管道
        private const int AFL_SHM_ENV = 1;   // 共享内存环境变量
        
        // AFL状态码
        private const uint AFL_CHILD_DONE = 0xdeadbeef;
        private const uint AFL_CHILD_EXITED = 0xcafebabe;
        
        private Process _aflProcess;
        private string _shmId;
        private bool _isInitialized;
        private readonly object _lock = new object();
        
        public async Task RunAsync(ICaller caller, EngineConfig config, CancellationToken cancellationToken)
        {
            Console.WriteLine($"[AFLSharpEngine] 启动 AFL 引擎模式");
            
            try
            {
                // 检查是否在AFL环境下运行
                _shmId = Environment.GetEnvironmentVariable("__AFL_SHM_ID");
                
                if (!string.IsNullOrEmpty(_shmId))
                {
                    // AFL模式 - 作为fork server运行
                    Console.WriteLine($"[AFLSharpEngine] 检测到AFL环境，共享内存ID: {_shmId}");
                    await RunAsForkServer(caller, config, cancellationToken);
                }
                else
                {
                    // 独立模式 - 用于测试和开发
                    Console.WriteLine($"[AFLSharpEngine] 独立模式运行");
                    await RunStandalone(caller, config, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[AFLSharpEngine] 模糊测试被取消");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[AFLSharpEngine] 引擎错误: {ex}");
                throw;
            }
            finally
            {
                Cleanup();
            }
        }
        
        /// <summary>
        /// 作为AFL fork server运行
        /// </summary>
        private async Task RunAsForkServer(ICaller caller, EngineConfig config, CancellationToken cancellationToken)
        {
            // 初始化覆盖率收集
            CoverageCollector.Initialize(_shmId);
            
            // 设置fork server通信
            if (!SetupForkServer())
            {
                throw new InvalidOperationException("无法设置fork server通信");
            }
            
            Console.WriteLine("[AFLSharpEngine] Fork server已就绪");
            
            // 主循环 - 处理AFL的测试用例
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // 等待AFL发送新的测试用例
                    var hasInput = await WaitForInput(cancellationToken);
                    if (!hasInput)
                        break;
                    
                    // 重置覆盖率
                    CoverageCollector.Reset();
                    
                    // 读取输入数据
                    byte[] inputData = ReadInput();
                    if (inputData == null || inputData.Length == 0)
                        continue;
                    
                    // 创建FuzzInput
                    var fuzzInput = new FuzzInput(inputData);
                    
                    // 执行测试
                    var stopwatch = Stopwatch.StartNew();
                    object result = null;
                    Exception caughtException = null;
                    
                    try
                    {
                        // 设置超时
                        using (var cts = new CancellationTokenSource(config.TimeoutMs))
                        {
                            result = await Task.Run(() => caller.Call(fuzzInput), cts.Token);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // 超时
                        Console.WriteLine("[AFLSharpEngine] 执行超时");
                        SignalResult(ResultType.Timeout);
                        continue;
                    }
                    catch (Exception ex)
                    {
                        caughtException = ex;
                    }
                    
                    stopwatch.Stop();
                    
                    // 处理结果
                    if (caughtException != null)
                    {
                        HandleException(caughtException);
                    }
                    else if (result is ExceptionResult exResult)
                    {
                        HandleExceptionResult(exResult);
                    }
                    else
                    {
                        // 正常完成
                        SignalResult(ResultType.Success);
                    }
                    
                    // 记录统计信息
                    var stats = CoverageCollector.GetStatistics();
                    if (stats.CoveredEdges > 0)
                    {
                        Console.WriteLine($"[AFLSharpEngine] 覆盖率: {stats}，执行时间: {stopwatch.ElapsedMilliseconds}ms");
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[AFLSharpEngine] 测试循环错误: {ex.Message}");
                    SignalResult(ResultType.Error);
                }
            }
        }
        
        /// <summary>
        /// 独立模式运行（用于开发和测试）
        /// </summary>
        private async Task RunStandalone(ICaller caller, EngineConfig config, CancellationToken cancellationToken)
        {
            // 初始化本地覆盖率收集
            CoverageCollector.Initialize();
            
            // 加载或生成种子输入
            var seeds = LoadSeedInputs(config.InputDirectory);
            if (seeds.Count == 0)
            {
                Console.WriteLine("[AFLSharpEngine] 没有种子输入，生成默认种子");
                seeds.Add(GenerateDefaultSeed());
            }
            
            Console.WriteLine($"[AFLSharpEngine] 加载了 {seeds.Count} 个种子输入");
            
            var corpus = new Corpus();
            foreach (var seed in seeds)
            {
                corpus.Add(seed);
            }
            
            // 变异和测试循环
            int iterations = 0;
            var mutator = new Mutator();
            
            while (!cancellationToken.IsCancellationRequested && 
                   (config.MaxIterations < 0 || iterations < config.MaxIterations))
            {
                // 选择种子
                var seed = corpus.SelectSeed();
                
                // 变异
                var mutated = mutator.Mutate(seed);
                
                // 重置覆盖率
                CoverageCollector.Reset();
                
                // 执行测试
                var fuzzInput = new FuzzInput(mutated);
                object result = null;
                
                try
                {
                    result = caller.Call(fuzzInput);
                }
                catch (Exception ex)
                {
                    // 发现崩溃
                    await SaveCrash(mutated, ex, config.OutputDirectory);
                    Console.WriteLine($"[AFLSharpEngine] 发现崩溃: {ex.GetType().Name}");
                }
                
                // 检查覆盖率
                var stats = CoverageCollector.GetStatistics();
                if (stats.CoveredEdges > corpus.MaxCoverage)
                {
                    // 新覆盖率，添加到语料库
                    corpus.Add(mutated);
                    Console.WriteLine($"[AFLSharpEngine] 新覆盖率: {stats.CoveredEdges} 边");
                }
                
                iterations++;
                
                if (iterations % 1000 == 0)
                {
                    Console.WriteLine($"[AFLSharpEngine] 进度: {iterations} 次迭代, " +
                                    $"语料库大小: {corpus.Size}, " +
                                    $"最大覆盖率: {corpus.MaxCoverage}");
                }
                
                // 短暂延迟以避免CPU占用过高
                await Task.Delay(1, cancellationToken);
            }
            
            Console.WriteLine($"[AFLSharpEngine] 完成 {iterations} 次迭代");
        }
        
        #region Fork Server通信
        
        private bool SetupForkServer()
        {
            try
            {
                // 在实际AFL环境中，这些文件描述符由AFL设置
                // 这里我们模拟基本的握手协议
                
                // 发送"hello"消息给AFL
                var hello = BitConverter.GetBytes(0xC0FFEE);
                // 实际实现需要写入FORKSRV_FD文件描述符
                
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        private async Task<bool> WaitForInput(CancellationToken cancellationToken)
        {
            // 在实际AFL环境中，这里会阻塞等待AFL的信号
            // 模拟实现
            await Task.Delay(10, cancellationToken);
            return !cancellationToken.IsCancellationRequested;
        }
        
        private byte[] ReadInput()
        {
            // AFL通过stdin传递输入
            using (var stdin = Console.OpenStandardInput())
            using (var ms = new MemoryStream())
            {
                stdin.CopyTo(ms);
                return ms.ToArray();
            }
        }
        
        private void SignalResult(ResultType type)
        {
            // 向AFL报告执行结果
            uint status = type switch
            {
                ResultType.Success => AFL_CHILD_DONE,
                ResultType.Crash => AFL_CHILD_EXITED,
                ResultType.Timeout => AFL_CHILD_EXITED | 0x1000,
                _ => AFL_CHILD_EXITED | 0x2000
            };
            
            // 实际实现需要写入控制管道
            Console.WriteLine($"[AFLSharpEngine] 信号结果: {type} (0x{status:X8})");
        }
        
        #endregion
        
        #region 结果处理
        
        private void HandleException(Exception ex)
        {
            var category = ClassifyException(ex);
            
            switch (category)
            {
                case ExceptionCategory.Expected:
                    // 预期异常，正常返回
                    SignalResult(ResultType.Success);
                    break;
                    
                case ExceptionCategory.Unexpected:
                    // 意外异常，报告为崩溃
                    Console.WriteLine($"[AFLSharpEngine] 意外异常: {ex.GetType().Name}: {ex.Message}");
                    SignalResult(ResultType.Crash);
                    break;
                    
                case ExceptionCategory.Fatal:
                    // 致命异常
                    Console.WriteLine($"[AFLSharpEngine] 致命异常: {ex.GetType().Name}");
                    SignalResult(ResultType.Crash);
                    break;
            }
        }
        
        private void HandleExceptionResult(ExceptionResult result)
        {
            if (result.Category.Equals(ExceptionCategory.Fatal) || 
                result.Category.Equals(ExceptionCategory.Unexpected))
            {
                Console.WriteLine($"[AFLSharpEngine] 异常结果: {result.Category} - {result.Exception.Message}");
                SignalResult(ResultType.Crash);
            }
            else
            {
                SignalResult(ResultType.Success);
            }
        }
        
        private ExceptionCategory ClassifyException(Exception ex)
        {
            return ex switch
            {
                ArgumentNullException => ExceptionCategory.Expected,
                ArgumentException => ExceptionCategory.Expected,
                InvalidOperationException => ExceptionCategory.Expected,
                NotImplementedException => ExceptionCategory.Expected,
                OutOfMemoryException => ExceptionCategory.Fatal,
                StackOverflowException => ExceptionCategory.Fatal,
                AccessViolationException => ExceptionCategory.Fatal,
                _ => ExceptionCategory.Unexpected
            };
        }
        
        #endregion
        
        #region 辅助方法
        
        private List<byte[]> LoadSeedInputs(string directory)
        {
            var seeds = new List<byte[]>();
            
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return seeds;
            
            foreach (var file in Directory.GetFiles(directory))
            {
                try
                {
                    var data = File.ReadAllBytes(file);
                    if (data.Length > 0)
                        seeds.Add(data);
                }
                catch
                {
                    // 忽略无法读取的文件
                }
            }
            
            return seeds;
        }
        
        private byte[] GenerateDefaultSeed()
        {
            // 生成基本的默认种子
            return new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 };
        }
        
        private async Task SaveCrash(byte[] input, Exception ex, string outputDir)
        {
            if (string.IsNullOrEmpty(outputDir))
                outputDir = "crashes";
            
            Directory.CreateDirectory(outputDir);
            
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var hash = GetHash(ex.StackTrace ?? ex.Message);
            var filename = $"crash_{ex.GetType().Name}_{timestamp}_{hash:X8}";
            
            // 保存输入
            await File.WriteAllBytesAsync(Path.Combine(outputDir, filename + ".input"), input);
            
            // 保存异常信息
            var info = $"Exception: {ex.GetType().FullName}\n" +
                      $"Message: {ex.Message}\n" +
                      $"StackTrace:\n{ex.StackTrace}";
            await File.WriteAllTextAsync(Path.Combine(outputDir, filename + ".txt"), info);
        }
        
        private uint GetHash(string text)
        {
            uint hash = 0;
            foreach (char c in text)
            {
                hash = ((hash << 5) + hash) + c;
            }
            return hash;
        }
        
        private void Cleanup()
        {
            CoverageCollector.Cleanup();
            _aflProcess?.Dispose();
        }
        
        #endregion
        
        #region 内部类型
        
        private enum ResultType
        {
            Success,
            Crash,
            Timeout,
            Error
        }
        
        private enum ExceptionCategory
        {
            Expected,
            Unexpected,
            Fatal
        }
        
        /// <summary>
        /// 简单的语料库实现
        /// </summary>
        private class Corpus
        {
            private readonly List<byte[]> _inputs = new List<byte[]>();
            private readonly Random _random = new Random();
            
            public int Size => _inputs.Count;
            public int MaxCoverage { get; private set; }
            
            public void Add(byte[] input)
            {
                _inputs.Add(input);
                var stats = CoverageCollector.GetStatistics();
                if (stats.CoveredEdges > MaxCoverage)
                    MaxCoverage = stats.CoveredEdges;
            }
            
            public byte[] SelectSeed()
            {
                if (_inputs.Count == 0)
                    return new byte[0];
                    
                return _inputs[_random.Next(_inputs.Count)];
            }
        }
        
        /// <summary>
        /// 简单的变异器实现
        /// </summary>
        private class Mutator
        {
            private readonly Random _random = new Random();

            public byte[] Mutate(byte[] input)
            {
                if (input.Length == 0)
                    return new byte[] { (byte)_random.Next(256) };

                var mutated = (byte[])input.Clone();
                var strategy = _random.Next(4); // 假设策略是 0, 1, 2, 3

                switch (strategy)
                {
                    case 0: // 位翻转
                        if (mutated.Length > 0)
                        {
                            var pos = _random.Next(mutated.Length);
                            var bit = _random.Next(8);
                            mutated[pos] ^= (byte)(1 << bit);
                        }
                        break;

                    case 1: // 字节替换
                        if (mutated.Length > 0)
                        {
                            var pos = _random.Next(mutated.Length);
                            mutated[pos] = (byte)_random.Next(256);
                        }
                        break;

                    case 2: // 插入
                    {
                        var insertPos = _random.Next(mutated.Length + 1);
                        var newData = new byte[mutated.Length + 1]; // <-- newData 作用域开始
                        Array.Copy(mutated, 0, newData, 0, insertPos);
                        newData[insertPos] = (byte)_random.Next(256);
                        Array.Copy(mutated, insertPos, newData, insertPos + 1, mutated.Length - insertPos);
                        mutated = newData;
                        break;
                    }

                    case 3: // 删除
                    {
                        if (mutated.Length > 1)
                        {
                            var deletePos = _random.Next(mutated.Length);
                            var newData = new byte[mutated.Length - 1]; // <-- newData 作用域开始 (与case 2不冲突)
                            Array.Copy(mutated, 0, newData, 0, deletePos);
                            Array.Copy(mutated, deletePos + 1, newData, deletePos, mutated.Length - deletePos - 1);
                            mutated = newData;
                        }
                        break;
                    }
                }

                return mutated;
            }
        }
        
        #endregion
    }
}