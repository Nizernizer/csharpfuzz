using System;
using System.Linq;
using System.Text;
using WFuzzGen.Models;

namespace WFuzzGen.Templates
{
    /// <summary>
    /// 带覆盖率插桩的方法模板
    /// </summary>
    public static class InstrumentedMethodTemplate
    {
        public static string Generate(TestEntryInfo testEntry, bool enableCoverage = true)
        {
            var sb = new StringBuilder();
            var className = testEntry.Symbol.Replace("WFuzzGen.", "");
            var isStatic = testEntry.Metadata.MethodType == "static";
            var isDisposable = testEntry.Metadata.IsDisposable;
            var isAsync = testEntry.Metadata.IsAsync;
            
            // 文件头
            sb.AppendLine("using System;");
            if (isAsync)
            {
                sb.AppendLine("using System.Threading.Tasks;");
            }
            sb.AppendLine("using WFuzz;");
            if (enableCoverage)
            {
                sb.AppendLine("using WFuzzAgent;");
            }
            
            // 添加目标类型的命名空间
            if (!string.IsNullOrEmpty(testEntry.Metadata.Namespace))
            {
                sb.AppendLine($"using {testEntry.Metadata.Namespace};");
            }
            
            sb.AppendLine();
            sb.AppendLine("namespace WFuzzGen");
            sb.AppendLine("{");
            
            // 类定义
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// 测试入口: {testEntry.Function}");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    public class {className} : WFuzz.ICaller");
            sb.AppendLine("    {");
            
            // 生成唯一的块ID
            var blockIdBase = GetBlockIdBase(testEntry);
            
            // Call方法
            sb.AppendLine("        public object Call(WFuzz.FuzzInput input)");
            sb.AppendLine("        {");
            
            if (enableCoverage)
            {
                sb.AppendLine($"            // 记录方法入口");
                sb.AppendLine($"            CoverageCollector.RecordBlock({blockIdBase});");
            }
            
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            
            // 生成参数
            if (isStatic)
            {
                GenerateStaticMethodCall(sb, testEntry, enableCoverage, blockIdBase);
            }
            else
            {
                GenerateInstanceMethodCall(sb, testEntry, isDisposable, enableCoverage, blockIdBase);
            }
            
            sb.AppendLine("            }");
            sb.AppendLine("            catch (Exception ex)");
            sb.AppendLine("            {");
            
            if (enableCoverage)
            {
                sb.AppendLine($"                // 记录异常分支");
                sb.AppendLine($"                CoverageCollector.RecordBlock({blockIdBase + 100});");
            }
            
            sb.AppendLine("                return new WFuzz.ExceptionResult(ex);");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            
            // 生成块ID计算方法
            if (enableCoverage)
            {
                sb.AppendLine();
                sb.AppendLine($"        private static readonly ushort BlockIdBase = {blockIdBase};");
            }
            
            sb.AppendLine("    }");
            sb.AppendLine("}");
            
            return sb.ToString();
        }
        
        private static void GenerateStaticMethodCall(StringBuilder sb, TestEntryInfo testEntry, 
            bool enableCoverage, ushort blockIdBase)
        {
            // 生成参数
            for (int i = 0; i < testEntry.Arguments.Count; i++)
            {
                var arg = testEntry.Arguments[i];
                var typeName = !string.IsNullOrEmpty(arg.TypeFullName) ? arg.TypeFullName : arg.TypeId;
                sb.AppendLine($"                var arg{i} = input.GenerateArgument<{typeName}>({i});");
                
                if (enableCoverage)
                {
                    sb.AppendLine($"                CoverageCollector.RecordBlock({blockIdBase + i + 1});");
                }
            }
            
            // 调用方法
            sb.Append($"                ");
            if (testEntry.Metadata.ReturnType != "void")
            {
                sb.Append("var result = ");
            }
            
            sb.Append($"{testEntry.Metadata.DeclaringType}.{GetMethodName(testEntry)}(");
            sb.Append(string.Join(", ", Enumerable.Range(0, testEntry.Arguments.Count).Select(i => $"arg{i}")));
            sb.AppendLine(");");
            
            if (enableCoverage)
            {
                sb.AppendLine($"                CoverageCollector.RecordBlock({blockIdBase + 50});");
            }
            
            if (testEntry.Metadata.ReturnType != "void")
            {
                sb.AppendLine("                return result;");
            }
            else
            {
                sb.AppendLine("                return null;");
            }
        }
        
        private static void GenerateInstanceMethodCall(StringBuilder sb, TestEntryInfo testEntry, 
            bool isDisposable, bool enableCoverage, ushort blockIdBase)
        {
            // 生成实例
            var instanceArg = testEntry.Arguments[0];
            var instanceTypeName = !string.IsNullOrEmpty(instanceArg.TypeFullName) ? instanceArg.TypeFullName : instanceArg.TypeId;
            
            if (isDisposable)
            {
                sb.AppendLine($"                using var instance = input.GenerateArgument<{instanceTypeName}>(0);");
            }
            else
            {
                sb.AppendLine($"                var instance = input.GenerateArgument<{instanceTypeName}>(0);");
            }
            
            if (enableCoverage)
            {
                sb.AppendLine($"                CoverageCollector.RecordBlock({blockIdBase + 1});");
            }
            
            // 生成其他参数
            for (int i = 1; i < testEntry.Arguments.Count; i++)
            {
                var arg = testEntry.Arguments[i];
                var typeName = !string.IsNullOrEmpty(arg.TypeFullName) ? arg.TypeFullName : arg.TypeId;
                sb.AppendLine($"                var arg{i} = input.GenerateArgument<{typeName}>({i});");
                
                if (enableCoverage)
                {
                    sb.AppendLine($"                CoverageCollector.RecordBlock({blockIdBase + i + 1});");
                }
            }
            
            // 调用方法
            sb.Append($"                ");
            if (testEntry.Metadata.ReturnType != "void")
            {
                sb.Append("var result = ");
            }
            
            sb.AppendLine($"instance.{GetMethodName(testEntry)}({GetArgumentList(testEntry)});");
            
            if (enableCoverage)
            {
                sb.AppendLine($"                CoverageCollector.RecordBlock({blockIdBase + 50});");
            }
            
            if (testEntry.Metadata.ReturnType != "void")
            {
                sb.AppendLine("                return result;");
            }
            else
            {
                sb.AppendLine("                return null;");
            }
        }
        
        private static string GetMethodName(TestEntryInfo testEntry)
        {
            var function = testEntry.Function;
            var dotIndex = function.IndexOf('.');
            var parenIndex = function.IndexOf('(');
            
            if (dotIndex >= 0 && parenIndex > dotIndex)
            {
                return function.Substring(dotIndex + 1, parenIndex - dotIndex - 1);
            }
            
            return "Method";
        }
        
        private static string GetArgumentList(TestEntryInfo testEntry)
        {
            // 跳过第一个参数（实例）
            var args = Enumerable.Range(1, testEntry.Arguments.Count - 1).Select(i => $"arg{i}");
            return string.Join(", ", args);
        }
        
        private static ushort GetBlockIdBase(TestEntryInfo testEntry)
        {
            // 基于方法签名生成唯一的块ID基数
            var hash = testEntry.Symbol.GetHashCode();
            return (ushort)(Math.Abs(hash) % 50000);
        }
    }
}