using System;
using System.Linq;
using System.Text;
using WFuzzGen.Models;

namespace WFuzzGen.Templates
{
    /// <summary>
    /// 静态方法模板
    /// </summary>
    public static class StaticMethodTemplate
    {
        public static string Generate(TestEntryInfo testEntry)
        {
            var sb = new StringBuilder();
            var className = testEntry.Symbol.Replace("WFuzzGen.", "");
            
            // 文件头
            sb.AppendLine("using System;");
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
            sb.AppendLine($"    public class {className} : WFuzz.ICaller");
            sb.AppendLine("    {");
            
            // Call方法
            sb.AppendLine("        public object Call(WFuzz.FuzzInput input)");
            sb.AppendLine("        {");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            
            // 生成参数 - 修复：使用 TypeFullName 而不是 TypeId
            for (int i = 0; i < testEntry.Arguments.Count; i++)
            {
                var arg = testEntry.Arguments[i];
                // 优先使用 TypeFullName，如果为空则使用 TypeId
                var typeName = !string.IsNullOrEmpty(arg.TypeFullName) ? arg.TypeFullName : arg.TypeId;
                sb.AppendLine($"                var arg{i} = input.GenerateArgument<{typeName}>({i});");
            }
            
            // 调用方法
            sb.Append($"                ");
            if (testEntry.Metadata.ReturnType != "void")
            {
                sb.Append("return ");
            }
            
            sb.Append($"{testEntry.Metadata.DeclaringType}.{GetMethodName(testEntry)}(");
            sb.Append(string.Join(", ", Enumerable.Range(0, testEntry.Arguments.Count).Select(i => $"arg{i}")));
            sb.AppendLine(");");
            
            if (testEntry.Metadata.ReturnType == "void")
            {
                sb.AppendLine("                return null;");
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
            // 从函数签名中提取方法名
            var function = testEntry.Function;
            var dotIndex = function.IndexOf('.');
            var parenIndex = function.IndexOf('(');
            
            if (dotIndex >= 0 && parenIndex > dotIndex)
            {
                return function.Substring(dotIndex + 1, parenIndex - dotIndex - 1);
            }
            
            return "Method";
        }
    }
}