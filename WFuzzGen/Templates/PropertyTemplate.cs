using System;
using System.Text;
using WFuzzGen.Models;

namespace WFuzzGen.Templates
{
    /// <summary>
    /// 属性模板
    /// </summary>
    public static class PropertyTemplate
    {
        public static string Generate(TestEntryInfo testEntry)
        {
            var sb = new StringBuilder();
            var className = testEntry.Symbol.Replace("WFuzzGen.", "");
            var isStatic = testEntry.Metadata.MethodType == "static";
            var isGetter = testEntry.Function.Contains("{ get; }");
            var propertyName = ExtractPropertyName(testEntry.Function);
            
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
            
            if (isGetter)
            {
                // Getter
                if (isStatic)
                {
                    sb.AppendLine($"                return {testEntry.Metadata.DeclaringType}.{propertyName};");
                }
                else
                {
                    var isDisposable = testEntry.Metadata.IsDisposable;
                    var instanceTypeName = !string.IsNullOrEmpty(testEntry.Arguments[0].TypeFullName) 
                        ? testEntry.Arguments[0].TypeFullName 
                        : testEntry.Arguments[0].TypeId;
                    
                    if (isDisposable)
                    {
                        sb.AppendLine($"                using var instance = input.GenerateArgument<{instanceTypeName}>(0);");
                    }
                    else
                    {
                        sb.AppendLine($"                var instance = input.GenerateArgument<{instanceTypeName}>(0);");
                    }
                    sb.AppendLine($"                return instance.{propertyName};");
                }
            }
            else
            {
                // Setter
                if (isStatic)
                {
                    var valueTypeName = !string.IsNullOrEmpty(testEntry.Arguments[0].TypeFullName) 
                        ? testEntry.Arguments[0].TypeFullName 
                        : testEntry.Arguments[0].TypeId;
                    sb.AppendLine($"                var value = input.GenerateArgument<{valueTypeName}>(0);");
                    sb.AppendLine($"                {testEntry.Metadata.DeclaringType}.{propertyName} = value;");
                }
                else
                {
                    var isDisposable = testEntry.Metadata.IsDisposable;
                    var instanceTypeName = !string.IsNullOrEmpty(testEntry.Arguments[0].TypeFullName) 
                        ? testEntry.Arguments[0].TypeFullName 
                        : testEntry.Arguments[0].TypeId;
                    
                    if (isDisposable)
                    {
                        sb.AppendLine($"                using var instance = input.GenerateArgument<{instanceTypeName}>(0);");
                    }
                    else
                    {
                        sb.AppendLine($"                var instance = input.GenerateArgument<{instanceTypeName}>(0);");
                    }
                    
                    var valueTypeName = !string.IsNullOrEmpty(testEntry.Arguments[1].TypeFullName) 
                        ? testEntry.Arguments[1].TypeFullName 
                        : testEntry.Arguments[1].TypeId;
                    sb.AppendLine($"                var value = input.GenerateArgument<{valueTypeName}>(1);");
                    sb.AppendLine($"                instance.{propertyName} = value;");
                }
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
        
        private static string ExtractPropertyName(string function)
        {
            var dotIndex = function.IndexOf('.');
            var braceIndex = function.IndexOf(' ');
            
            if (dotIndex >= 0 && braceIndex > dotIndex)
            {
                return function.Substring(dotIndex + 1, braceIndex - dotIndex - 1);
            }
            
            return "Property";
        }
    }
}