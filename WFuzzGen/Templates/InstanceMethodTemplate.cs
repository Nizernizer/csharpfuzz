using System;
using System.Linq;
using System.Text;
using WFuzzGen.Models;

namespace WFuzzGen.Templates
{
    /// <summary>
    /// 实例方法模板
    /// </summary>
    public static class InstanceMethodTemplate
    {
        public static string Generate(TestEntryInfo testEntry)
        {
            var sb = new StringBuilder();
            var className = testEntry.Symbol.Replace("WFuzzGen.", "");
            var isDisposable = testEntry.Metadata.IsDisposable;
            var isAsync = testEntry.Metadata.IsAsync;
            
            // 文件头
            sb.AppendLine("using System;");
            if (isAsync)
            {
                sb.AppendLine("using System.Threading.Tasks;");
            }
            sb.AppendLine("using WFuzz;");
            
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
            
            // Call方法
            sb.AppendLine("        public object Call(WFuzz.FuzzInput input)");
            sb.AppendLine("        {");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            
            // 生成实例 - 修复：使用 TypeFullName
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
            
            // 生成其他参数 - 修复：使用 TypeFullName
            for (int i = 1; i < testEntry.Arguments.Count; i++)
            {
                var arg = testEntry.Arguments[i];
                var typeName = !string.IsNullOrEmpty(arg.TypeFullName) ? arg.TypeFullName : arg.TypeId;
                sb.AppendLine($"                var arg{i} = input.GenerateArgument<{typeName}>({i});");
            }
            
            // 调用方法
            sb.Append($"                ");
            
            if (isAsync)
            {
                sb.AppendLine($"var task = instance.{GetMethodName(testEntry)}({GetArgumentList(testEntry)});");
                sb.AppendLine("                return task.ConfigureAwait(false).GetAwaiter().GetResult();");
            }
            else
            {
                if (testEntry.Metadata.ReturnType != "void")
                {
                    sb.Append("return ");
                }
                
                sb.AppendLine($"instance.{GetMethodName(testEntry)}({GetArgumentList(testEntry)});");
                
                if (testEntry.Metadata.ReturnType == "void")
                {
                    sb.AppendLine("                return null;");
                }
            }
            
            sb.AppendLine("            }");
            sb.AppendLine("            catch (Exception ex)");
            sb.AppendLine("            {");
            sb.AppendLine("                return new WFuzz.ExceptionResult(ex);");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            
            return sb.ToString();
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
    }
}