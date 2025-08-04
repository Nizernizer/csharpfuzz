using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using WFuzz;
using SharpFuzz;

namespace WFuzzEngine
{
    /// <summary>
    /// 基于 SharpFuzz 的引擎适配器
    /// </summary>
    public class SharpFuzzEngine : IFuzzEngine
    {
        public string Name => "SharpFuzz";

        public async Task RunAsync(ICaller caller, EngineConfig config, CancellationToken cancellationToken)
        {
            Console.WriteLine($"[SharpFuzzEngine] Starting SharpFuzz engine with config: {config}");

            try
            {
                // 检查是否在libFuzzer环境下运行
                var args = Environment.GetCommandLineArgs();
                bool isLibFuzzerMode = Environment.GetEnvironmentVariable("LIBFUZZER_MODE") == "1" ||
                                      (args.Length > 0 && args[0].Contains("-max_len"));
                
                if (isLibFuzzerMode)
                {
                    // libFuzzer模式
                    await RunLibFuzzerMode(caller, config, cancellationToken);
                }
                else
                {
                    // 独立模式
                    await RunStandaloneMode(caller, config, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[SharpFuzzEngine] 模糊测试被取消");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SharpFuzzEngine] 引擎运行时发生错误: {ex}");
                throw;
            }
        }

        /// <summary>
        /// libFuzzer模式 - 与Fuzzer.LibFuzzer集成
        /// </summary>
        private async Task RunLibFuzzerMode(ICaller caller, EngineConfig config, CancellationToken cancellationToken)
        {
            Console.WriteLine("[SharpFuzzEngine] 运行在 libFuzzer 模式");

            // 创建用于取消的信号
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(() => tcs.TrySetResult(true));

            // 将 ICaller 包装成 SharpFuzz 可以驱动的形式
            // 使用 SharpFuzz 定义的 ReadOnlySpanAction 委托类型
            SharpFuzz.ReadOnlySpanAction fuzzTarget = (ReadOnlySpan<byte> data) =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException();
                }

                var fuzzInput = new FuzzInput(data.ToArray());
                object? result = null;
                
                try
                {
                    result = caller.Call(fuzzInput);
                }
                catch (Exception ex)
                {
                    // 记录异常但不中断fuzzing
                    HandleException(ex, data.ToArray(), config);
                    throw; // 重新抛出以让libFuzzer记录
                }

                // 处理结果
                if (result is ExceptionResult exceptionResult)
                {
                    if (exceptionResult.Category == ExceptionCategory.Fatal ||
                        exceptionResult.Category == ExceptionCategory.Unexpected)
                    {
                        Console.WriteLine($"[SharpFuzzEngine] 捕获到 {exceptionResult.Category} 异常: {exceptionResult.Exception.Message}");
                        
                        // 保存崩溃信息
                        SaveCrash(data.ToArray(), exceptionResult.Exception, config.OutputDirectory);
                        
                        // 对于意外异常，抛出以让libFuzzer记录
                        if (exceptionResult.Category == ExceptionCategory.Unexpected)
                        {
                            throw exceptionResult.Exception;
                        }
                    }
                }
            };

            // 在新线程中运行 libFuzzer
            var fuzzerTask = Task.Run(() =>
            {
                try
                {
                    Console.WriteLine("[SharpFuzzEngine] 调用 Fuzzer.LibFuzzer.Run...");
                    Fuzzer.LibFuzzer.Run(fuzzTarget);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[SharpFuzzEngine] LibFuzzer 运行错误: {ex}");
                    throw;
                }
            });

            // 等待取消或完成
            await Task.WhenAny(fuzzerTask, tcs.Task);
            
            if (cancellationToken.IsCancellationRequested)
            {
                Console.WriteLine("[SharpFuzzEngine] 正在停止 libFuzzer...");
                // libFuzzer 通常通过信号停止，这里我们依赖于进程退出
            }
        }

        /// <summary>
        /// 独立模式 - 用于开发和测试
        /// </summary>
        private async Task RunStandaloneMode(ICaller caller, EngineConfig config, CancellationToken cancellationToken)
        {
            Console.WriteLine($"[SharpFuzzEngine] Running in standalone mode");

            // Create output directories
            if (!string.IsNullOrEmpty(config.OutputDirectory))
            {
                Directory.CreateDirectory(config.OutputDirectory);
                Directory.CreateDirectory(Path.Combine(config.OutputDirectory, "crashes"));
                Directory.CreateDirectory(Path.Combine(config.OutputDirectory, "queue"));
            }

            // Load seed inputs
            var seeds = LoadSeedInputs(config.InputDirectory);
            if (seeds.Count == 0)
            {
                Console.WriteLine("[SharpFuzzEngine] No seed inputs, using default seeds");
                seeds.Add(new byte[] { 0x00 });
                seeds.Add(new byte[] { 0xFF });
                seeds.Add(System.Text.Encoding.UTF8.GetBytes("test"));
            }

            Console.WriteLine($"[SharpFuzzEngine] Loaded {seeds.Count} seeds");

            // Simple fuzzing loop
            int iterations = 0;
            int crashes = 0;
            var mutator = new SimpleMutator();
            var startTime = DateTime.Now;

            while (!cancellationToken.IsCancellationRequested && 
                   (config.MaxIterations < 0 || iterations < config.MaxIterations))
            {
                // 选择种子
                var seed = seeds[iterations % seeds.Count];
                
                // 变异
                var mutated = mutator.Mutate(seed);
                
                // 创建输入
                var fuzzInput = new FuzzInput(mutated);
                
                try
                {
                    // 执行测试
                    var result = caller.Call(fuzzInput);
                    
                    // 处理结果
                    if (result is ExceptionResult exResult)
                    {
                        if (exResult.Category == ExceptionCategory.Unexpected ||
                            exResult.Category == ExceptionCategory.Fatal)
                        {
                            crashes++;
                            SaveCrash(mutated, exResult.Exception, config.OutputDirectory);
                            Console.WriteLine($"[SharpFuzzEngine] Found crash #{crashes}: {exResult.Exception.GetType().Name}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 未预期的异常
                    crashes++;
                    SaveCrash(mutated, ex, config.OutputDirectory);
                    Console.WriteLine($"[SharpFuzzEngine] Found crash #{crashes}: {ex.GetType().Name}");
                }

                iterations++;
                
                // 定期报告进度
                if (iterations % 1000 == 0)
                {
                    var elapsed = DateTime.Now - startTime;
                    var execPerSec = iterations / elapsed.TotalSeconds;
                    Console.WriteLine($"[SharpFuzzEngine] Progress: {iterations} executions, " +
                                    $"{crashes} crashes, {execPerSec:F0} exec/s");
                }

                // 避免占用过多CPU
                if (iterations % 100 == 0)
                {
                    await Task.Delay(1, cancellationToken);
                }
            }

            var totalElapsed = DateTime.Now - startTime;
            Console.WriteLine($"[SharpFuzzEngine] Complete: {iterations} executions, {crashes} crashes, " +
                            $"elapsed: {totalElapsed.TotalSeconds:F1}s");
        }

        /// <summary>
        /// 处理异常
        /// </summary>
        private void HandleException(Exception ex, byte[] input, EngineConfig config)
        {
            var category = ClassifyException(ex);
            
            if (category != ExceptionCategory.Expected)
            {
                Console.WriteLine($"[SharpFuzzEngine] {category} 异常: {ex.GetType().Name}: {ex.Message}");
                SaveCrash(input, ex, config.OutputDirectory);
            }
        }

        /// <summary>
        /// 异常分类
        /// </summary>
        private ExceptionCategory ClassifyException(Exception ex)
        {
            return ex switch
            {
                ArgumentNullException => ExceptionCategory.Expected,
                ArgumentException => ExceptionCategory.Expected,
                InvalidOperationException => ExceptionCategory.Expected,
                NotImplementedException => ExceptionCategory.Expected,
                NotSupportedException => ExceptionCategory.Expected,
                OutOfMemoryException => ExceptionCategory.Fatal,
                StackOverflowException => ExceptionCategory.Fatal,
                AccessViolationException => ExceptionCategory.Fatal,
                ExecutionEngineException => ExceptionCategory.Fatal,
                _ => ExceptionCategory.Unexpected
            };
        }

        /// <summary>
        /// 保存崩溃信息
        /// </summary>
        private void SaveCrash(byte[] input, Exception ex, string? outputDir)
        {
            if (string.IsNullOrEmpty(outputDir))
                return;

            try
            {
                var crashDir = Path.Combine(outputDir, "crashes");
                Directory.CreateDirectory(crashDir);

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                var exType = ex.GetType().Name;
                var hash = GetExceptionHash(ex);
                
                var baseName = $"id_{timestamp}_{exType}_{hash:x8}";
                
                // 保存输入文件
                File.WriteAllBytes(Path.Combine(crashDir, baseName + ".input"), input);
                
                // 保存异常信息
                var crashInfo = $"Exception Type: {ex.GetType().FullName}\n" +
                               $"Message: {ex.Message}\n" +
                               $"Input Size: {input.Length} bytes\n" +
                               $"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                               $"\nStack Trace:\n{ex.StackTrace}\n";
                
                if (ex.InnerException != null)
                {
                    crashInfo += $"\nInner Exception: {ex.InnerException.GetType().FullName}\n" +
                                $"Inner Message: {ex.InnerException.Message}\n" +
                                $"Inner Stack:\n{ex.InnerException.StackTrace}\n";
                }
                
                File.WriteAllText(Path.Combine(crashDir, baseName + ".txt"), crashInfo);
            }
            catch (Exception saveEx)
            {
                Console.Error.WriteLine($"[SharpFuzzEngine] 保存崩溃信息失败: {saveEx.Message}");
            }
        }

        /// <summary>
        /// 获取异常哈希值（用于去重）
        /// </summary>
        private uint GetExceptionHash(Exception ex)
        {
            var str = ex.GetType().FullName + (ex.StackTrace?.Split('\n')[0] ?? "");
            uint hash = 2166136261u;
            foreach (char c in str)
            {
                hash = (hash ^ c) * 16777619u;
            }
            return hash;
        }

        /// <summary>
        /// 加载种子输入
        /// </summary>
        private List<byte[]> LoadSeedInputs(string? directory)
        {
            var seeds = new List<byte[]>();
            
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return seeds;

            foreach (var file in Directory.GetFiles(directory))
            {
                try
                {
                    var data = File.ReadAllBytes(file);
                    if (data.Length > 0 && data.Length < 1_000_000) // 限制最大1MB
                    {
                        seeds.Add(data);
                    }
                }
                catch
                {
                    // 忽略无法读取的文件
                }
            }

            return seeds;
        }

        /// <summary>
        /// 简单的变异器
        /// </summary>
        private class SimpleMutator
        {
            private readonly Random _random = new Random();
            private readonly byte[] _interestingBytes = { 0, 1, 16, 32, 64, 100, 127, 128, 255 };
            private readonly int[] _interestingInts = { -1, 0, 1, 16, 32, 64, 100, 127, 128, 255, 256, 512, 1000, 1024, 4096, 32767, 65535 };

            public byte[] Mutate(byte[] input)
            {
                if (input == null || input.Length == 0)
                    return new byte[] { (byte)_random.Next(256) };

                // 复制输入
                var mutated = (byte[])input.Clone();
                
                // 选择变异策略
                var strategy = _random.Next(10);
                
                switch (strategy)
                {
                    case 0: // 位翻转
                        BitFlip(mutated);
                        break;
                    case 1: // 字节替换
                        ByteSubstitution(mutated);
                        break;
                    case 2: // 有趣值替换
                        InterestingValueSubstitution(mutated);
                        break;
                    case 3: // 算术操作
                        ArithmeticOperation(mutated);
                        break;
                    case 4: // 块删除
                        mutated = BlockDeletion(mutated);
                        break;
                    case 5: // 块插入
                        mutated = BlockInsertion(mutated);
                        break;
                    case 6: // 块复制
                        mutated = BlockDuplication(mutated);
                        break;
                    case 7: // 块交换
                        BlockSwap(mutated);
                        break;
                    case 8: // 随机字节
                        RandomBytes(mutated);
                        break;
                    case 9: // Havoc (多次变异)
                        mutated = Havoc(mutated);
                        break;
                }

                return mutated;
            }

            private void BitFlip(byte[] data)
            {
                if (data.Length == 0) return;
                var pos = _random.Next(data.Length);
                var bit = _random.Next(8);
                data[pos] ^= (byte)(1 << bit);
            }

            private void ByteSubstitution(byte[] data)
            {
                if (data.Length == 0) return;
                var pos = _random.Next(data.Length);
                data[pos] = (byte)_random.Next(256);
            }

            private void InterestingValueSubstitution(byte[] data)
            {
                if (data.Length == 0) return;
                var pos = _random.Next(data.Length);
                data[pos] = _interestingBytes[_random.Next(_interestingBytes.Length)];
            }

            private void ArithmeticOperation(byte[] data)
            {
                if (data.Length == 0) return;
                var pos = _random.Next(data.Length);
                var op = _random.Next(4);
                switch (op)
                {
                    case 0: data[pos]++; break;
                    case 1: data[pos]--; break;
                    case 2: data[pos] = (byte)(data[pos] + _random.Next(1, 32)); break;
                    case 3: data[pos] = (byte)(data[pos] - _random.Next(1, 32)); break;
                }
            }

            private byte[] BlockDeletion(byte[] data)
            {
                if (data.Length <= 1) return data;
                var start = _random.Next(data.Length);
                var length = _random.Next(1, Math.Min(data.Length - start, data.Length / 4 + 1));
                var result = new byte[data.Length - length];
                Array.Copy(data, 0, result, 0, start);
                Array.Copy(data, start + length, result, start, data.Length - start - length);
                return result;
            }

            private byte[] BlockInsertion(byte[] data)
            {
                var pos = _random.Next(data.Length + 1);
                var length = _random.Next(1, Math.Min(16, data.Length / 4 + 1));
                var result = new byte[data.Length + length];
                Array.Copy(data, 0, result, 0, pos);
                for (int i = 0; i < length; i++)
                {
                    result[pos + i] = (byte)_random.Next(256);
                }
                Array.Copy(data, pos, result, pos + length, data.Length - pos);
                return result;
            }

            private byte[] BlockDuplication(byte[] data)
            {
                if (data.Length == 0) return new byte[] { 0 };
                var srcPos = _random.Next(data.Length);
                var length = _random.Next(1, Math.Min(data.Length - srcPos, Math.Min(16, data.Length / 4 + 1)));
                var dstPos = _random.Next(data.Length + 1);
                var result = new byte[data.Length + length];
                Array.Copy(data, 0, result, 0, dstPos);
                Array.Copy(data, srcPos, result, dstPos, length);
                Array.Copy(data, dstPos, result, dstPos + length, data.Length - dstPos);
                return result;
            }

            private void BlockSwap(byte[] data)
            {
                if (data.Length < 4) return;
                var pos1 = _random.Next(data.Length / 2);
                var pos2 = _random.Next(data.Length / 2, data.Length);
                var length = _random.Next(1, Math.Min(Math.Min(pos2 - pos1, data.Length - pos2), 16));
                for (int i = 0; i < length; i++)
                {
                    var tmp = data[pos1 + i];
                    data[pos1 + i] = data[pos2 + i];
                    data[pos2 + i] = tmp;
                }
            }

            private void RandomBytes(byte[] data)
            {
                if (data.Length == 0) return;
                var start = _random.Next(data.Length);
                var length = _random.Next(1, Math.Min(data.Length - start, 16));
                for (int i = 0; i < length; i++)
                {
                    data[start + i] = (byte)_random.Next(256);
                }
            }

            private byte[] Havoc(byte[] data)
            {
                var result = (byte[])data.Clone();
                var iterations = _random.Next(1, 16);
                for (int i = 0; i < iterations; i++)
                {
                    result = Mutate(result);
                    if (result.Length > 100000) // 防止过度增长
                    {
                        result = data;
                        break;
                    }
                }
                return result;
            }
        }
    }
}