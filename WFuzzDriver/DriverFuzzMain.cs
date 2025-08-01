using System;
using System.IO;
using System.Reflection;
using WFuzz;

namespace WFuzzDriver
{
    /// <summary>
    /// 模糊测试驱动主程序
    /// </summary>
    public class DriverFuzzMain
    {
        /// <summary>
        /// 主入口点（简化版）
        /// </summary>
        /// <param name="args">命令行参数</param>
        public static void Main(string[] args)
        {
            // 基本参数检查
            if (args.Length < 2)
            {
                Console.WriteLine("用法: DriverFuzzMain <测试程序集路径> <ICaller完整类名>");
                Console.WriteLine("示例: DriverFuzzMain ./WFuzzGen.Generated.dll WFuzzGen.MyNamespace_MyClass_MyMethod_int_string");
                return;
            }

            string testAssemblyPath = args[0];
            string callerClassName = args[1]; // WFuzzGen 生成的类名，例如 "WFuzzGen.MyNamespace_MyClass_MyMethod_int_string"

            try
            {
                // 1. 加载测试程序集
                Assembly testAssembly = Assembly.LoadFrom(testAssemblyPath);
                Console.WriteLine($"[Driver] 已加载测试程序集: {testAssembly.FullName}");

                // 2. 定位并实例化 ICaller
                Type? callerType = testAssembly.GetType(callerClassName);
                if (callerType == null)
                {
                    // 尝试在 WFuzzGen 命名空间下查找
                    callerType = testAssembly.GetType($"WFuzzGen.{callerClassName}");
                }

                if (callerType == null || !typeof(ICaller).IsAssignableFrom(callerType))
                {
                    Console.Error.WriteLine($"[Driver] 错误: 无法在程序集中找到类型 '{callerClassName}' 或它未实现 ICaller 接口。");
                    return;
                }

                ICaller? callerInstance = Activator.CreateInstance(callerType) as ICaller;
                if (callerInstance == null)
                {
                    Console.Error.WriteLine($"[Driver] 错误: 无法创建 ICaller 实例 '{callerClassName}'。");
                    return;
                }
                Console.WriteLine($"[Driver] 已创建 ICaller 实例: {callerType.FullName}");

                // 3. 执行测试 (使用简单的固定输入进行演示)
                // 在实际的模糊测试引擎中，这里会循环并提供不同的 FuzzInput
                Console.WriteLine("[Driver] 开始执行测试...");
                byte[] testData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }; // 示例输入数据
                FuzzInput input = new FuzzInput(testData);

                object? result = callerInstance.Call(input);

                // 4. 处理结果
                if (result is ExceptionResult exceptionResult)
                {
                    Console.WriteLine($"[Driver] 测试执行捕获到异常: {exceptionResult.Exception.GetType().Name}: {exceptionResult.Exception.Message}");
                    Console.WriteLine($"[Driver] 异常分类: {exceptionResult.Category}");
                    if (exceptionResult.Category == ExceptionCategory.Fatal)
                    {
                        Console.WriteLine("[Driver] 致命异常，停止测试。");
                    }
                    else if (exceptionResult.Category == ExceptionCategory.Unexpected)
                    {
                        Console.WriteLine("[Driver] 意外异常，记录为潜在 Bug。");
                        // 这里应该记录崩溃信息、输入数据等
                    }
                    else
                    {
                        Console.WriteLine("[Driver] 预期业务异常。");
                    }
                }
                else
                {
                    Console.WriteLine($"[Driver] 测试执行成功完成。返回值类型: {(result?.GetType().ToString() ?? "null")}");
                    // 可以根据需要打印或处理非异常结果
                }

                Console.WriteLine("[Driver] 测试执行流程演示完成。");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Driver] 执行过程中发生未处理的错误: {ex}");
            }
        }
    }
}