using System;
using System.Threading;
using System.Threading.Tasks;
using WFuzz; // 引用核心库
// 确保正确引入了 SharpFuzz 命名空间
using SharpFuzz;
// 如果 SharpFuzz 使用了特定的委托类型，可能需要引入其命名空间
// using SharpFuzz.Delegates; // 举例，具体看 SharpFuzz 源码

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
            Console.WriteLine($"[SharpFuzzEngine] 启动 SharpFuzz 引擎，配置: {config}");

            // 1. 将 ICaller 包装成 SharpFuzz 可以驱动的形式
            // 关键修改：使用 SharpFuzz 库定义的委托类型
            // 假设类型是 SharpFuzz.ReadOnlySpanAction，如果不是，请根据实际库的定义调整
            SharpFuzz.ReadOnlySpanAction fuzzTarget = (ReadOnlySpan<byte> data) =>
            {
                // ... (fuzzTarget 内部逻辑保持不变) ...
                 if (cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("[SharpFuzzEngine] 取消请求已收到，在测试目标内部。");
                    // 注意：libFuzzer 通常不直接响应 OperationCanceledException 来停止
                }

                var fuzzInput = new FuzzInput(data.ToArray());
                object? result = caller.Call(fuzzInput);

                if (result is ExceptionResult exceptionResult)
                {
                    if (exceptionResult.Category == ExceptionCategory.Fatal ||
                        exceptionResult.Category == ExceptionCategory.Unexpected)
                    {
                        Console.WriteLine($"[SharpFuzzEngine] 捕获到 {exceptionResult.Category} 异常: {exceptionResult.Exception.Message}");
                        throw exceptionResult.Exception;
                    }
                }
            };

            try
            {
                Console.WriteLine("[SharpFuzzEngine] 调用 SharpFuzz.LibFuzzer.Run...");

                // 2. 使用 SharpFuzz 运行测试
                // 确保传递的委托类型正确
                await Task.Run(() =>
                {
                    // 如果 Fuzzer.LibFuzzer.Run 有重载，确保调用的是接受 ReadOnlySpanAction 的那个
                    Fuzzer.LibFuzzer.Run(fuzzTarget); 
                }, cancellationToken);

                Console.WriteLine("[SharpFuzzEngine] SharpFuzz.LibFuzzer.Run 已返回。");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[SharpFuzzEngine] 模糊测试任务被取消。");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[SharpFuzzEngine] 引擎运行时发生错误: {ex}");
            }
            finally
            {
                Console.WriteLine("[SharpFuzzEngine] 引擎已停止。");
            }
        }
    }
}