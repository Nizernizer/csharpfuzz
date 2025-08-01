using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using WFuzzGen.Models;
using WFuzzGen.Templates;

namespace WFuzzGen
{
    /// <summary>
    /// 代码生成器
    /// </summary>
    public class CodeGenerator
    {
        private readonly string _outputDirectory;
        private readonly List<string> _generatedFiles = new List<string>();
        private readonly List<TestEntryInfo> _generatedTestEntries = new List<TestEntryInfo>();

        public CodeGenerator(string outputDirectory)
        {
            _outputDirectory = outputDirectory;
            if (!Directory.Exists(_outputDirectory))
            {
                Directory.CreateDirectory(_outputDirectory);
            }
        }

        /// <summary>
        /// 生成所有代码
        /// </summary>
        public void GenerateAll(AnalysisResult analysisResult)
        {
            // 保存测试入口供生成器使用
            _generatedTestEntries.Clear();
            _generatedTestEntries.AddRange(analysisResult.TestEntries);
            
            // 生成测试入口代码
            foreach (var testEntry in analysisResult.TestEntries)
            {
                GenerateTestEntry(testEntry);
            }
            
            // 生成自定义生成器代码
            var customGenerators = GetCustomGenerators(analysisResult);
            foreach (var generator in customGenerators)
            {
                GenerateCustomGenerator(generator);
            }
            
            // 生成项目文件
            GenerateProjectFile();
            
            // 生成输出JSON
            GenerateOutputJson(analysisResult);
        }

        /// <summary>
        /// 生成测试入口代码
        /// </summary>
        private void GenerateTestEntry(TestEntryInfo testEntry)
        {
            string code;
            
            switch (testEntry.Metadata.MemberType)
            {
                case "method":
                    code = testEntry.Metadata.MethodType == "static" 
                        ? StaticMethodTemplate.Generate(testEntry)
                        : InstanceMethodTemplate.Generate(testEntry);
                    break;
                    
                case "property":
                    code = PropertyTemplate.Generate(testEntry);
                    break;
                    
                case "indexer":
                    code = IndexerTemplate.Generate(testEntry);
                    break;
                    
                default:
                    return;
            }
            
            var fileName = $"{testEntry.Symbol.Replace("WFuzzGen.", "")}.cs";
            var filePath = Path.Combine(_outputDirectory, fileName);
            
            File.WriteAllText(filePath, code, Encoding.UTF8);
            _generatedFiles.Add(fileName);
        }

        /// <summary>
        /// 获取需要生成的自定义生成器
        /// </summary>
        private List<GeneratorInfo> GetCustomGenerators(AnalysisResult analysisResult)
        {
            var customGenerators = new List<GeneratorInfo>();
            var processedTypes = new HashSet<string>();
            
            foreach (var testEntry in analysisResult.TestEntries)
            {
                foreach (var arg in testEntry.Arguments)
                {
                    if (processedTypes.Contains(arg.TypeId))
                        continue;
                        
                    processedTypes.Add(arg.TypeId);
                    
                    // 检查是否需要自定义生成器
                    if (arg.DefaultGenerator.StartsWith("WFuzzGen."))
                    {
                        customGenerators.Add(new GeneratorInfo
                        {
                            TypeId = arg.TypeId,
                            DisplayName = arg.TypeId,
                            Generators = new List<GeneratorDefinition>
                            {
                                new GeneratorDefinition
                                {
                                    DisplayName = "<custom>",
                                    MangledName = arg.DefaultGenerator,
                                    IsDefault = true
                                }
                            }
                        });
                    }
                }
            }
            
            return customGenerators;
        }

    /// <summary>
    /// 生成自定义生成器代码
    /// </summary>
    private void GenerateCustomGenerator(GeneratorInfo generator)
    {
        var generatorName = generator.Generators[0].MangledName;
        var className = generatorName.Replace("WFuzzGen.", "");
        
        // 从 TestEntries 中找到使用此生成器的参数信息
        ArgumentInfo? argInfo = null;
        foreach (var entry in _generatedTestEntries)
        {
            argInfo = entry.Arguments.FirstOrDefault(a => a.DefaultGenerator == generatorName);
            if (argInfo != null) break;
        }
        
        var code = new StringBuilder();
        code.AppendLine("using System;");
        code.AppendLine("using System.Collections.Generic;");
        code.AppendLine("using System.Linq;");
        code.AppendLine("using WFuzz;");
        
        // 添加目标类型的命名空间
        if (argInfo?.OriginalType != null && !string.IsNullOrEmpty(argInfo.OriginalType.Namespace))
        {
            code.AppendLine($"using {argInfo.OriginalType.Namespace};");
        }
        
        code.AppendLine();
        code.AppendLine("namespace WFuzzGen");
        code.AppendLine("{");
        
        // 特殊处理一些类型
        if (generator.TypeId == "object")
        {
            // object 类型的特殊生成器
            code.AppendLine($"    public class {className} : WFuzz.ICaller");
            code.AppendLine("    {");
            code.AppendLine("        public object Call(WFuzz.FuzzInput input)");
            code.AppendLine("        {");
            code.AppendLine("            try");
            code.AppendLine("            {");
            code.AppendLine("                // 为 object 类型返回一个简单的字符串");
            code.AppendLine("                return input.GenerateArgument<string>(0);");
            code.AppendLine("            }");
            code.AppendLine("            catch (Exception ex)");
            code.AppendLine("            {");
            code.AppendLine("                return new WFuzz.ExceptionResult(ex);");
            code.AppendLine("            }");
            code.AppendLine("        }");
            code.AppendLine("    }");
        }
        else if (generator.TypeId == "T")
        {
            // 泛型参数 T 的特殊处理
            code.AppendLine($"    public class {className} : WFuzz.ICaller");
            code.AppendLine("    {");
            code.AppendLine("        public object Call(WFuzz.FuzzInput input)");
            code.AppendLine("        {");
            code.AppendLine("            try");
            code.AppendLine("            {");
            code.AppendLine("                // 为泛型参数 T 返回一个 object");
            code.AppendLine("                return new object();");
            code.AppendLine("            }");
            code.AppendLine("            catch (Exception ex)");
            code.AppendLine("            {");
            code.AppendLine("                return new WFuzz.ExceptionResult(ex);");
            code.AppendLine("            }");
            code.AppendLine("        }");
            code.AppendLine("    }");
        }
        else if (generator.TypeId.EndsWith("_array"))
        {
            // 数组生成器
            var elementType = generator.TypeId.Replace("_array", "");
            var elementTypeFullName = argInfo?.TypeFullName?.Replace("[]", "") ?? elementType;
            
            code.AppendLine($"    public class {className} : WFuzz.ICaller");
            code.AppendLine("    {");
            code.AppendLine("        public object Call(WFuzz.FuzzInput input)");
            code.AppendLine("        {");
            code.AppendLine("            try");
            code.AppendLine("            {");
            code.AppendLine("                int length = Math.Abs(input.GenerateArgument<int>(0)) % 10;");
            code.AppendLine($"                var array = new {elementTypeFullName}[length];");
            code.AppendLine("                for (int i = 0; i < length; i++)");
            code.AppendLine("                {");
            code.AppendLine($"                    array[i] = input.GenerateArgument<{elementTypeFullName}>(i + 1);");
            code.AppendLine("                }");
            code.AppendLine("                return array;");
            code.AppendLine("            }");
            code.AppendLine("            catch (Exception ex)");
            code.AppendLine("            {");
            code.AppendLine("                return new WFuzz.ExceptionResult(ex);");
            code.AppendLine("            }");
            code.AppendLine("        }");
            code.AppendLine("    }");
        }
        else
        {
            // 使用实际的类型信息
            var typeFullName = argInfo?.TypeFullName ?? GetFullTypeName(generator.TypeId);
            var isGeneric = argInfo?.OriginalType?.IsGenericType ?? false;
            
            code.AppendLine($"    public class {className} : WFuzz.ICaller");
            code.AppendLine("    {");
            code.AppendLine("        public object Call(WFuzz.FuzzInput input)");
            code.AppendLine("        {");
            code.AppendLine("            try");
            code.AppendLine("            {");
            
            if (isGeneric && argInfo?.OriginalType != null)
            {
                // 对于泛型类型，使用具体的类型参数
                if (typeFullName.Contains("Container"))
                {
                    // Container<T> 特殊处理
                    code.AppendLine($"                return new TestLibrary.Container<object>();");
                }
                else
                {
                    // 其他泛型类型
                    code.AppendLine($"                return new {typeFullName}();");
                }
            }
            else
            {
                // 非泛型类型使用反射
                code.AppendLine($"                var type = typeof({typeFullName});");
                code.AppendLine("                ");
                code.AppendLine("                var ctors = type.GetConstructors();");
                code.AppendLine("                var defaultCtor = ctors.FirstOrDefault(c => c.GetParameters().Length == 0);");
                code.AppendLine("                ");
                code.AppendLine("                if (defaultCtor != null)");
                code.AppendLine("                {");
                code.AppendLine("                    return defaultCtor.Invoke(null);");
                code.AppendLine("                }");
                code.AppendLine("                ");
                code.AppendLine("                var ctor = ctors.OrderBy(c => c.GetParameters().Length).FirstOrDefault();");
                code.AppendLine("                if (ctor != null)");
                code.AppendLine("                {");
                code.AppendLine("                    var parameters = ctor.GetParameters();");
                code.AppendLine("                    var args = new object[parameters.Length];");
                code.AppendLine("                    ");
                code.AppendLine("                    for (int i = 0; i < parameters.Length; i++)");
                code.AppendLine("                    {");
                code.AppendLine("                        var paramType = parameters[i].ParameterType;");
                code.AppendLine("                        ");
                code.AppendLine("                        if (paramType == typeof(string))");
                code.AppendLine("                            args[i] = input.GenerateArgument<string>(i);");
                code.AppendLine("                        else if (paramType == typeof(int))");
                code.AppendLine("                            args[i] = input.GenerateArgument<int>(i);");
                code.AppendLine("                        else if (paramType == typeof(double))");
                code.AppendLine("                            args[i] = input.GenerateArgument<double>(i);");
                code.AppendLine("                        else if (paramType == typeof(bool))");
                code.AppendLine("                            args[i] = input.GenerateArgument<bool>(i);");
                code.AppendLine("                        else if (paramType.IsValueType)");
                code.AppendLine("                            args[i] = Activator.CreateInstance(paramType);");
                code.AppendLine("                        else");
                code.AppendLine("                            args[i] = null;");
                code.AppendLine("                    }");
                code.AppendLine("                    ");
                code.AppendLine("                    return ctor.Invoke(args);");
                code.AppendLine("                }");
                code.AppendLine("                ");
                code.AppendLine($"                throw new InvalidOperationException(\"无法创建类型 {typeFullName} 的实例\");");
            }
            
            code.AppendLine("            }");
            code.AppendLine("            catch (Exception ex)");
            code.AppendLine("            {");
            code.AppendLine("                return new WFuzz.ExceptionResult(ex);");
            code.AppendLine("            }");
            code.AppendLine("        }");
            code.AppendLine("    }");
        }
        
        code.AppendLine("}");
        
        var fileName = $"{className}.cs";
        var filePath = Path.Combine(_outputDirectory, fileName);
        
        File.WriteAllText(filePath, code.ToString(), Encoding.UTF8);
        _generatedFiles.Add(fileName);
    }

        /// <summary>
        /// 生成项目文件
        /// </summary>
        private void GenerateProjectFile()
        {
            var projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <RootNamespace>WFuzzGen</RootNamespace>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include=""..\WFuzz\WFuzz.csproj"" />
    <ProjectReference Include=""..\TestLibrary\TestLibrary.csproj"" />
  </ItemGroup>
</Project>";

            var projectPath = Path.Combine(_outputDirectory, "WFuzzGen.Generated.csproj");
            File.WriteAllText(projectPath, projectContent, Encoding.UTF8);
        }

        /// <summary>
        /// 生成输出JSON
        /// </summary>
        private void GenerateOutputJson(AnalysisResult analysisResult)
        {
            // 计算项目总行数
            var totalLines = analysisResult.TestEntries.Sum(e => e.Metadata.LineCount);
            if (totalLines == 0) totalLines = 1; // 避免除零
            
            // 按程序集分组测试入口
            var entriesByAssembly = analysisResult.TestEntries
                .GroupBy(e => e.Metadata.Assembly)
                .ToList();

            var binaries = new List<object>();

            foreach (var assemblyGroup in entriesByAssembly)
            {
                var assemblyName = assemblyGroup.Key;
                
                // 按源文件分组
                var fileGroups = assemblyGroup
                    .GroupBy(e => e.Metadata.SourceFile)
                    .Select(g => new
                    {
                        file = g.Key,
                        language = "csharp",
                        lines = g.Sum(e => e.Metadata.LineCount), // 文件的有效行数
                        functions = g.Select(entry => new
                        {
                            function_brief = GetFunctionBrief(entry.Function),
                            function = entry.Function,
                            symbol = entry.Symbol.Replace("WFuzzGen.", ""),
                            arguments = entry.Arguments
                                .Where(arg => !arg.IsInstance) // 过滤掉实例参数
                                .Select(arg => new
                                {
                                    typeid = GetTypeId(arg.TypeId),
                                    display = arg.TypeId,
                                    default_generator = GetGeneratorId(arg.DefaultGenerator)
                                }).ToArray(),
                            lineBegin = entry.Metadata.LineBegin,
                            lineEnd = entry.Metadata.LineEnd,
                            lineCount = entry.Metadata.LineCount,
                            lineCoverage = (double)entry.Metadata.LineCount / totalLines
                        }).ToArray()
                    }).ToArray();

                // 收集所有用到的类型
                var usedTypes = assemblyGroup
                    .SelectMany(e => e.Arguments.Select(a => a.TypeId))
                    .Distinct()
                    .ToList();

                // 生成类型生成器信息
                var typeGenerators = usedTypes.Select(typeId => new
                {
                    typeid = GetTypeId(typeId),
                    display_name = typeId,
                    generators = GetGeneratorsForType(typeId, analysisResult.Generators)
                }).ToArray();

                var binary = new
                {
                    binary = Path.GetFullPath(Path.Combine("TestLibrary", "bin", "Debug", "net9.0", $"{assemblyName}.dll")),
                    files = fileGroups,
                    type_generators = typeGenerators
                };

                binaries.Add(binary);
            }

            var output = new
            {
                binaries = binaries
            };
            
            var json = System.Text.Json.JsonSerializer.Serialize(output, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });
            
            var jsonPath = Path.Combine(_outputDirectory, "analysis_result.json");
            File.WriteAllText(jsonPath, json, Encoding.UTF8);
        }

        /// <summary>
        /// 获取函数简名
        /// </summary>
        private string GetFunctionBrief(string function)
        {
            // 从 "ClassName.MethodName(params)" 提取 "MethodName"
            var dotIndex = function.LastIndexOf('.');
            var parenIndex = function.IndexOf('(');
            
            if (dotIndex >= 0 && parenIndex > dotIndex)
            {
                return function.Substring(dotIndex + 1, parenIndex - dotIndex - 1);
            }
            
            return function;
        }

        /// <summary>
        /// 获取类型ID（简化版本）
        /// </summary>
        private string GetTypeId(string typeFullName)
        {
            // 将常见类型映射到简短ID
            var typeIdMap = new Dictionary<string, string>
            {
                { "int", "i" },
                { "string", "s" },
                { "bool", "b" },
                { "byte", "by" },
                { "float", "f" },
                { "double", "d" },
                { "System_DateTime", "dt" },
                { "System_Guid", "g" },
                { "System_TimeSpan", "ts" },
                { "int?", "i?" },
                { "void", "v" }
            };

            if (typeIdMap.TryGetValue(typeFullName, out var id))
                return id;

            // 对于复杂类型，生成基于哈希的短ID
            var hash = typeFullName.GetHashCode();
            return $"t{Math.Abs(hash) % 10000}";
        }

        /// <summary>
        /// 获取生成器ID
        /// </summary>
        private string GetGeneratorId(string generatorFullName)
        {
            // 从完整类名提取简短ID
            var lastDot = generatorFullName.LastIndexOf('.');
            if (lastDot >= 0)
            {
                var name = generatorFullName.Substring(lastDot + 1);
                // 移除 "Generator" 后缀
                if (name.EndsWith("Generator"))
                    name = name.Substring(0, name.Length - 9);
                return name.ToLower();
            }
            
            return generatorFullName;
        }

        /// <summary>
        /// 获取类型的生成器列表
        /// </summary>
        private object[] GetGeneratorsForType(string typeId, List<GeneratorInfo> allGenerators)
        {
            var generator = allGenerators.FirstOrDefault(g => g.TypeId == typeId);
            
            if (generator != null)
            {
                return generator.Generators.Select(g => new
                {
                    display_name = g.DisplayName,
                    mangled_name = GetGeneratorId(g.MangledName),
                    is_default = g.IsDefault,
                    arguments = g.Arguments.Select(arg => new
                    {
                        typeid = GetTypeId(arg.TypeId),
                        display = arg.TypeId,
                        default_generator = GetGeneratorId(arg.TypeId)
                    }).ToArray()
                }).ToArray();
            }

            // 默认生成器
            return new[]
            {
                new
                {
                    display_name = "<default>",
                    mangled_name = GetGeneratorId($"WFuzzGen.{typeId}_generator"),
                    is_default = true,
                    arguments = new object[0]
                }
            };
        }

        /// <summary>
        /// 获取类型的完整名称
        /// </summary>
        private string GetFullTypeName(string typeId)
        {
            // 将下划线转换回点号（命名空间分隔符）
            var fullName = typeId.Replace('_', '.');
            
            // 处理一些特殊情况
            if (fullName.StartsWith("TestLibrary."))
                return fullName;
            
            // 如果没有命名空间，假设在 TestLibrary 命名空间下
            if (!fullName.Contains('.'))
                return $"TestLibrary.{fullName}";
                
            return fullName;
        }

        /// <summary>
        /// 检查是否为特殊类型
        /// </summary>
        private bool IsSpecialType(string typeId)
        {
            var specialTypes = new HashSet<string>
            {
                "TestLibrary_Container_T",
                "TestLibrary_EventPublisher"
            };
            
            return specialTypes.Contains(typeId) || typeId.Contains("_T");
        }

        /// <summary>
        /// 生成特殊类型的实例化代码
        /// </summary>
        private string GenerateSpecialTypeInstantiation(string typeId)
        {
            // 处理泛型类型
            if (typeId.Contains("Container_T"))
            {
                return "new TestLibrary.Container<object>()";
            }
            
            if (typeId == "TestLibrary_EventPublisher")
            {
                return "new TestLibrary.EventPublisher()";
            }
            
            // 默认
            return $"new {GetFullTypeName(typeId)}()";
        }
    }
}