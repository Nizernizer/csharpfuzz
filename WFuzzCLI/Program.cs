using System;
using System.CommandLine; // 需要添加 System.CommandLine NuGet 包
using System.CommandLine.Invocation;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WFuzz; // 引用核心库
using WFuzzDriver; // 引用驱动程序
using WFuzzEngine; // 引用引擎

namespace WFuzzCLI
{
    class Program
    {
        // 用于优雅地停止模糊测试
        private static readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        static async Task<int> Main(string[] args)
        {
            // 注册 Ctrl+C 事件处理器
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true; // 阻止进程立即退出
                _cancellationTokenSource.Cancel();
                Console.WriteLine("\n[CLI] 接收到 Ctrl+C，正在停止模糊测试...");
            };

            // 定义根命令
            var rootCommand = new RootCommand("C# Fuzz 测试框架 CLI");

            // --- 定义 'run' 命令 ---
            var runCommand = new Command("run", "运行单个模糊测试 (手动指定入口)");
            var assemblyOption = new Option<FileInfo>(
                name: "--assembly",
                description: "生成的测试程序集路径 (例如: ./WFuzzGen.Generated.dll)")
            { IsRequired = true };
            runCommand.AddOption(assemblyOption);

            var callerOption = new Option<string>(
                name: "--caller",
                description: "ICaller 实现的完整类名 (例如: WFuzzGen.MyNamespace_MyClass_MyMethod_int_string)")
            { IsRequired = true };
            runCommand.AddOption(callerOption);
            // --- run 命令的新选项 ---
            var engineOption = new Option<string>(
                name: "--engine",
                description: "使用的模糊测试引擎 (例如: AFLSharp, SharpFuzz, Native)",
                getDefaultValue: () => "AFLSharp");
            runCommand.AddOption(engineOption);

            var inputDirOption = new Option<DirectoryInfo?>(
                name: "--input-dir",
                description: "种子输入目录路径");
            runCommand.AddOption(inputDirOption);

            var outputDirOption = new Option<DirectoryInfo?>(
                name: "--output-dir",
                description: "输出目录路径 (用于存放崩溃、队列等)");
            runCommand.AddOption(outputDirOption);

            var timeoutOption = new Option<int>(
                name: "--timeout",
                description: "单次测试超时时间 (毫秒)",
                getDefaultValue: () => 1000);
            runCommand.AddOption(timeoutOption);

            var iterationsOption = new Option<int>(
                name: "--iterations",
                description: "最大测试迭代次数 (-1 表示无限制)",
                getDefaultValue: () => -1);
            runCommand.AddOption(iterationsOption);

            // --- 定义 'fuzz' 命令 ---
            var fuzzCommand = new Command("fuzz", "运行模糊测试 (通过配置文件)");
            var configOption = new Option<FileInfo?>(
                name: "--config",
                description: "配置文件路径 (wfuzz.json)");
            fuzzCommand.AddOption(configOption);
            // fuzz 命令也可以接受部分 run 命令的选项作为覆盖


            // 设置 'run' 命令的处理程序
            runCommand.SetHandler(async (FileInfo assembly, string caller, string engine,
                                         DirectoryInfo? inputDir, DirectoryInfo? outputDir,
                                         int timeout, int iterations) =>
            {
                var config = new EngineConfig
                {
                    EngineName = engine,
                    InputDirectory = inputDir?.FullName ?? "./seeds",
                    OutputDirectory = outputDir?.FullName ?? "./output",
                    TimeoutMs = timeout,
                    MaxIterations = iterations
                    // 其他配置可以在这里设置默认值或从其他选项传入
                };
                await RunFuzzTestAsync(assembly.FullName, caller, config, _cancellationTokenSource.Token);
            }, assemblyOption, callerOption, engineOption, inputDirOption, outputDirOption, timeoutOption, iterationsOption);

            // 设置 'fuzz' 命令的处理程序 (占位符)
            fuzzCommand.SetHandler(async (FileInfo? config) =>
            {
                Console.WriteLine($"[CLI] 'fuzz' 命令功能尚未完全实现。请使用 'run' 命令或提供配置文件支持。配置文件路径: {config?.FullName ?? "未提供"}");
                // TODO: 实现从 wfuzz.json 读取配置，确定测试目标和引擎配置
                await Task.CompletedTask;
            }, configOption);

            // 将命令添加到根命令
            rootCommand.AddCommand(runCommand);
            rootCommand.AddCommand(fuzzCommand);

            // 解析并调用命令
            return await rootCommand.InvokeAsync(args);
        }

        /// <summary>
        /// 执行模糊测试的核心逻辑
        /// </summary>
        /// <param name="testAssemblyPath">测试程序集路径</param>
        /// <param name="callerClassName">ICaller 类名</param>
        /// <param name="engineConfig">引擎配置</param>
        /// <param name="cancellationToken">取消令牌</param>
        static async Task RunFuzzTestAsync(string testAssemblyPath, string callerClassName, EngineConfig engineConfig, CancellationToken cancellationToken)
        {
            Console.WriteLine($"[CLI] 开始运行模糊测试...");
            Console.WriteLine($"[CLI] 测试程序集: {testAssemblyPath}");
            Console.WriteLine($"[CLI] 测试入口: {callerClassName}");
            Console.WriteLine($"[CLI] 引擎配置: {engineConfig}");

            ICaller? callerInstance = null;
            try
            {
                // 1. 加载 ICaller (复用 DriverFuzzMain 的逻辑)
                callerInstance = LoadICaller(testAssemblyPath, callerClassName);
                if (callerInstance == null)
                {
                    Console.Error.WriteLine("[CLI] 无法加载 ICaller 实例，测试终止。");
                    return;
                }
                Console.WriteLine($"[CLI] 已加载 ICaller 实例: {callerClassName}");

                // 2. 创建并启动模糊测试引擎
                IFuzzEngine? engine = CreateEngine(engineConfig.EngineName);
                if (engine == null)
                {
                    Console.Error.WriteLine($"[CLI] 未知或不支持的引擎: {engineConfig.EngineName}");
                    return;
                }
                Console.WriteLine($"[CLI] 使用引擎: {engine.Name}");

                Console.WriteLine("[CLI] 启动模糊测试引擎... (按 Ctrl+C 停止)");
                await engine.RunAsync(callerInstance, engineConfig, cancellationToken);

            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[CLI] 模糊测试已由用户取消。");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[CLI] 运行模糊测试时发生错误: {ex}");
            }
            finally
            {
                Console.WriteLine("[CLI] 模糊测试运行结束。");
            }
        }

        /// <summary>
        /// 加载 ICaller 实例 (从 DriverFuzzMain 提取的逻辑)
        /// </summary>
        private static ICaller? LoadICaller(string testAssemblyPath, string callerClassName)
        {
            try
            {
                var testAssembly = System.Reflection.Assembly.LoadFrom(testAssemblyPath);
                Console.WriteLine($"[CLI] 已加载测试程序集: {testAssembly.FullName}");

                Type? callerType = testAssembly.GetType(callerClassName);
                if (callerType == null)
                {
                    callerType = testAssembly.GetType($"WFuzzGen.{callerClassName}");
                }

                if (callerType == null || !typeof(ICaller).IsAssignableFrom(callerType))
                {
                    Console.Error.WriteLine($"[CLI] 错误: 无法在程序集中找到类型 '{callerClassName}' 或它未实现 ICaller 接口。");
                    return null;
                }

                ICaller? callerInstance = System.Activator.CreateInstance(callerType) as ICaller;
                if (callerInstance == null)
                {
                    Console.Error.WriteLine($"[CLI] 错误: 无法创建 ICaller 实例 '{callerClassName}'。");
                    return null;
                }
                return callerInstance;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[CLI] 加载 ICaller 时出错: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 根据名称创建引擎实例
        /// </summary>
        private static IFuzzEngine? CreateEngine(string engineName)
        {
            return engineName.ToLowerInvariant() switch
            {
                // "aflsharp" => new AFLSharpEngine(), // 如果不再使用，可以注释掉或删除
                "sharpfuzz" => new SharpFuzzEngine(), // 添加对 SharpFuzzEngine 的支持
                // "native" => new NativeFuzzEngine(),   // 如果有，可以保留
                _ => null
            };
        }
    }
}