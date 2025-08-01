using System;
using System.IO;
// using SharpFuzz; // 不再需要 OutOfProcess
using WFuzz;     // 必需

public class Program
{
    public static void Main(string[] args)
    {
        // 1. 创建 WFuzzGen 生成的 ICaller 实例
        ICaller? caller = null;
        try
        {
            var generatedAssembly = System.Reflection.Assembly.LoadFrom(
                Path.Combine(AppContext.BaseDirectory, "WFuzzGen.Generated.dll")
            );
            // 确保类名正确
            var callerType = generatedAssembly.GetType("WFuzzGen.TestLibrary_Calculator_Add_int_int");
            if (callerType != null)
            {
                caller = System.Activator.CreateInstance(callerType) as ICaller;
            }
        }
        catch (Exception ex)
        {
            // 在初始化阶段发生严重错误，可以考虑写日志或直接退出
            Console.Error.WriteLine($"[FuzzTarget] 初始化 ICaller 失败: {ex.Message}");
            Environment.Exit(1); // 或 return; 让 afl-fuzz 检测到非正常退出
        }

        if (caller == null)
        {
            Console.Error.WriteLine("[FuzzTarget] 无法创建 ICaller 实例。");
            Environment.Exit(1);
        }

        Console.WriteLine("[FuzzTarget] ICaller 实例创建成功。");
        // 注意：在实际 fuzzing 中，这行日志可能只在第一次运行时看到

        try
        {
            // 2. 从 STDIN 读取输入数据
            //    注意：AFL 通常通过 STDIN 传递输入
            byte[] inputData = Console.OpenStandardInput().ReadAllBytes(); // 简化读取，注意大文件可能有问题

            // 或者更稳健的方式 (推荐)：
            // byte[] inputData;
            // using (var memoryStream = new MemoryStream())
            // {
            //     Console.OpenStandardInput().CopyTo(memoryStream);
            //     inputData = memoryStream.ToArray();
            // }


            // 3. 将输入数据包装成 FuzzInput
            var fuzzInput = new FuzzInput(inputData);

            // 4. 调用 ICaller
            //    注意：ICaller.Call 内部已经处理了 Expected/Unexpected/Fatal 异常
            //    我们在这里让它自然抛出 Unexpected/Fatal 异常
            var result = caller.Call(fuzzInput);

            // 5. (可选) 处理非异常结果
            //    对于 afl-fuzz，通常不需要特别处理成功的结果
            //    如果需要，可以在这里添加逻辑
            // if (!(result is ExceptionResult))
            // {
            //     // Console.WriteLine($"[FuzzTarget] 成功执行，结果类型: {result?.GetType()}"); // 日志太多
            // }

            // 6. 如果 ICaller.Call 没有抛出异常，程序正常退出 (Exit Code 0)
            //    这告诉 afl-fuzz 这个输入是"有趣的"（如果增加了覆盖率）或至少是"无害的"
        }
        catch (Exception ex) // 捕获 ICaller.Call 抛出的 Unexpected/Fatal 异常，或其他未处理异常
        {
            // 7. 打印异常信息到 STDERR
            //    afl-fuzz 会捕获 STDERR 输出，并在发现崩溃时保存相关信息
            Console.Error.WriteLine($"[FuzzTarget] 捕获到未处理异常: {ex}");
            // 8. 以非零退出码退出，通知 afl-fuzz 发生了崩溃
            Environment.Exit(1);
        }

        // 9. 正常退出 (Exit Code 0)
        // Environment.Exit(0); // 隐式
    }
}

// 为 Stream 添加 ReadAllBytes 扩展方法 (如果使用第一种读取方式)
public static class StreamExtensions
{
    public static byte[] ReadAllBytes(this Stream stream)
    {
        if (stream is MemoryStream memoryStream)
        {
            return memoryStream.ToArray();
        }

        using (var ms = new MemoryStream())
        {
            stream.CopyTo(ms);
            return ms.ToArray();
        }
    }
}