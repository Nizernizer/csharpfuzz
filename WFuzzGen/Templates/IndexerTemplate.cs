using System;
using System.Linq;
using System.Text;
using WFuzzGen.Models;

namespace WFuzzGen.Templates
{
    /// <summary>
    /// 索引器模板
    /// </summary>
    public static class IndexerTemplate
    {
        public static string Generate(TestEntryInfo testEntry)
        {
            var sb = new StringBuilder();
            var className = testEntry.Symbol.Replace("WFuzzGen.", "");
            var isGetter = testEntry.Function.Contains("{ get; }");
            var isDisposable = testEntry.Metadata.IsDisposable;
            
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
            
            // 生成实例
            if (isDisposable)
            {
                sb.AppendLine($"                using var instance = input.GenerateArgument<{testEntry.Arguments[0].TypeFullName}>(0);");
            }
            else
            {
                sb.AppendLine($"                var instance = input.GenerateArgument<{testEntry.Arguments[0].TypeFullName}>(0);");
            }
            
            if (isGetter)
            {
                // Getter - 生成索引参数
                for (int i = 1; i < testEntry.Arguments.Count; i++)
                {
                    var arg = testEntry.Arguments[i];
                    sb.AppendLine($"                var index{i-1} = input.GenerateArgument<{arg.TypeFullName}>({i});");
                }
                
                // 访问索引器
                sb.Append("                return instance[");
                sb.Append(string.Join(", ", Enumerable.Range(0, testEntry.Arguments.Count - 1).Select(i => $"index{i}")));
                sb.AppendLine("];");
            }
            else
            {
                // Setter - 生成索引参数
                int valueIndex = testEntry.Arguments.Count - 1;
                for (int i = 1; i < valueIndex; i++)
                {
                    var arg = testEntry.Arguments[i];
                    sb.AppendLine($"                var index{i-1} = input.GenerateArgument<{arg.TypeFullName}>({i});");
                }
                
                // 生成值参数
                sb.AppendLine($"                var value = input.GenerateArgument<{testEntry.Arguments[valueIndex].TypeFullName}>({valueIndex});");
                
                // 设置索引器
                sb.Append("                instance[");
                sb.Append(string.Join(", ", Enumerable.Range(0, valueIndex - 1).Select(i => $"index{i}")));
                sb.AppendLine("] = value;");
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
    }
}