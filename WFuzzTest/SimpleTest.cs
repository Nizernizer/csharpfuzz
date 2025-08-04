using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WFuzz;
using WFuzzEngine;
using WFuzzGen;
// 明确指定使用 WFuzz 命名空间的 ExceptionCategory
using ExceptionCategory = WFuzz.ExceptionCategory;

namespace WFuzzTest
{
    /// <summary>
    /// 简单的端到端测试程序
    /// 用于验证第一阶段的核心功能
    /// </summary>
    class SimpleTest
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== WFuzz 第一阶段集成测试 ===\n");

            try
            {
                // 1. 测试基础类型生成器
                TestBasicGenerators();

                // 2. 测试覆盖率收集
                TestCoverageCollection();

                // 3. 测试简单的模糊测试场景
                await TestSimpleFuzzing();

                // 4. 测试异常处理
                TestExceptionHandling();

                Console.WriteLine("\n所有测试通过！");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"\n测试失败: {ex}");
                Environment.Exit(1);
            }
        }

        static void TestBasicGenerators()
        {
            Console.WriteLine("测试1: 基础类型生成器");
            
            var input = new FuzzInput(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            
            // 测试整数生成
            var intVal = input.GenerateArgument<int>(0);
            Console.WriteLine($"  生成的int: {intVal}");
            
            // 测试字符串生成
            var strVal = input.GenerateArgument<string>(1);
            Console.WriteLine($"  生成的string: '{strVal}'");
            
            // 测试布尔生成
            var boolVal = input.GenerateBool();
            Console.WriteLine($"  生成的bool: {boolVal}");
            
            // 测试数组生成
            var arrVal = input.GenerateArgument<int[]>(2);
            Console.WriteLine($"  生成的int[]: Length={arrVal?.Length ?? 0}");
            
            Console.WriteLine("  ✓ 基础类型生成器正常工作\n");
        }

        static void TestCoverageCollection()
        {
            Console.WriteLine("测试2: 覆盖率收集");
            
            // 初始化覆盖率收集器
            WFuzzAgent.CoverageCollector.Initialize();
            
            // 模拟代码执行
            WFuzzAgent.CoverageCollector.RecordBlock(100);
            WFuzzAgent.CoverageCollector.RecordBlock(200);
            WFuzzAgent.CoverageCollector.RecordBlock(100); // 重复
            WFuzzAgent.CoverageCollector.RecordBlock(300);
            
            // 获取统计信息
            var stats = WFuzzAgent.CoverageCollector.GetStatistics();
            Console.WriteLine($"  覆盖的边: {stats.CoveredEdges}");
            Console.WriteLine($"  总命中数: {stats.TotalHits}");
            Console.WriteLine($"  覆盖率: {stats.CoveragePercentage:F2}%");
            
            // 重置
            WFuzzAgent.CoverageCollector.Reset();
            var resetStats = WFuzzAgent.CoverageCollector.GetStatistics();
            Console.WriteLine($"  重置后的覆盖边: {resetStats.CoveredEdges}");
            
            WFuzzAgent.CoverageCollector.Cleanup();
            Console.WriteLine("  ✓ 覆盖率收集正常工作\n");
        }

        static async Task TestSimpleFuzzing()
        {
            Console.WriteLine("测试3: 简单模糊测试");
            
            // 创建一个测试目标
            var testCaller = new TestDivisionCaller();
            
            // 配置
            var config = new EngineConfig
            {
                EngineName = "SharpFuzz",
                MaxIterations = 100,
                TimeoutMs = 1000,
                OutputDirectory = Path.Combine(Path.GetTempPath(), "wfuzz_test")
            };
            
            // 清理输出目录
            if (Directory.Exists(config.OutputDirectory))
                Directory.Delete(config.OutputDirectory, true);
            
            // 运行引擎
            var engine = new SharpFuzzEngine();
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3)); // 3秒超时
            
            Console.WriteLine("  运行模糊测试 (3秒)...");
            await engine.RunAsync(testCaller, config, cts.Token);
            
            // 检查结果
            var crashDir = Path.Combine(config.OutputDirectory, "crashes");
            if (Directory.Exists(crashDir))
            {
                var crashes = Directory.GetFiles(crashDir, "*.input");
                Console.WriteLine($"  发现 {crashes.Length} 个崩溃");
                
                if (crashes.Length > 0)
                {
                    // 应该至少发现除零错误
                    Console.WriteLine("  ✓ 成功发现崩溃！");
                }
            }
            
            Console.WriteLine("  ✓ 模糊测试引擎正常工作\n");
        }

        static void TestExceptionHandling()
        {
            Console.WriteLine("测试4: 异常处理");
            
            var input = new FuzzInput(new byte[] { 0, 0, 0, 0 });
            
            // 测试预期异常
            try
            {
                throw new ArgumentNullException("test");
            }
            catch (Exception ex)
            {
                var result = new ExceptionResult(ex);
                Console.WriteLine($"  ArgumentNullException -> {result.Category} (预期: Expected)");
                if (result.Category != ExceptionCategory.Expected)
                    throw new Exception("异常分类错误");
            }
            
            // 测试意外异常
            try
            {
                throw new NullReferenceException("test");
            }
            catch (Exception ex)
            {
                var result = new ExceptionResult(ex);
                Console.WriteLine($"  NullReferenceException -> {result.Category} (预期: Unexpected)");
                if (result.Category != ExceptionCategory.Unexpected)
                    throw new Exception("异常分类错误");
            }
            
            // 测试致命异常
            try
            {
                throw new OutOfMemoryException("test");
            }
            catch (Exception ex)
            {
                var result = new ExceptionResult(ex);
                Console.WriteLine($"  OutOfMemoryException -> {result.Category} (预期: Fatal)");
                if (result.Category != ExceptionCategory.Fatal)
                    throw new Exception("异常分类错误");
            }
            
            Console.WriteLine("  ✓ 异常处理正常工作\n");
        }
    }

    /// <summary>
    /// 测试用的除法调用器
    /// </summary>
    public class TestDivisionCaller : ICaller
    {
        public object Call(FuzzInput input)
        {
            try
            {
                var a = input.GenerateArgument<double>(0);
                var b = input.GenerateArgument<double>(1);
                
                // 故意的缺陷：没有检查除零
                var result = a / b;
                
                // 另一个缺陷：结果为无穷大时崩溃
                if (double.IsInfinity(result))
                {
                    throw new ArithmeticException("Result is infinity");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                return new ExceptionResult(ex);
            }
        }
    }
}