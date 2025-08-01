using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WFuzzGen;

namespace WFuzzGen
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("用法: WFuzzGen <程序集路径> <输出目录> [选项]");
                Console.WriteLine("选项:");
                Console.WriteLine("  --references <dll1,dll2,...>   引用程序集列表");
                Console.WriteLine("  --namespace <prefix>           命名空间前缀过滤");
                Console.WriteLine("  --exclude <pattern1,pattern2>  排除模式");
                Console.WriteLine("  --parallel <true/false>        启用并行分析");
                Console.WriteLine("  --max-parallel <n>             最大并行度");
                return;
            }

            var assemblyPath = args[0];
            var outputDirectory = args[1];
            
            var analyzer = new AssemblyAnalyzer();
            var references = new List<string>();
            
            // 解析命令行参数
            for (int i = 2; i < args.Length; i += 2)
            {
                if (i + 1 >= args.Length) break;
                
                switch (args[i])
                {
                    case "--references":
                        references.AddRange(args[i + 1].Split(','));
                        break;
                    case "--namespace":
                        analyzer.NamespacePrefix = args[i + 1];
                        break;
                    case "--exclude":
                        analyzer.ExcludePatterns.AddRange(args[i + 1].Split(','));
                        break;
                    case "--parallel":
                        analyzer.ParallelAnalysis = bool.Parse(args[i + 1]);
                        break;
                    case "--max-parallel":
                        analyzer.MaxParallelDegree = int.Parse(args[i + 1]);
                        break;
                }
            }
            
            try
            {
                Console.WriteLine($"开始分析程序集: {assemblyPath}");
                
                // 执行分析
                var result = analyzer.Analyze(assemblyPath, references);
                
                Console.WriteLine($"分析完成:");
                Console.WriteLine($"  - 分析类型数: {result.Statistics.TotalTypesAnalyzed}");
                Console.WriteLine($"  - 发现方法数: {result.Statistics.TotalMethodsFound}");
                Console.WriteLine($"  - 生成测试入口数: {result.Statistics.TotalTestEntriesGenerated}");
                Console.WriteLine($"  - 分析耗时: {result.Statistics.AnalysisTimeMs}ms");
                
                // 生成代码
                Console.WriteLine($"\n开始生成代码到: {outputDirectory}");
                var generator = new CodeGenerator(outputDirectory);
                generator.GenerateAll(result);
                
                Console.WriteLine("\n代码生成完成!");
                Console.WriteLine($"输出目录: {Path.GetFullPath(outputDirectory)}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"错误: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.Error.WriteLine($"内部错误: {ex.InnerException.Message}");
                }
                Environment.Exit(1);
            }
        }
    }
}