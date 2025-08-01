using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using WFuzzGen.Models;

namespace WFuzzGen
{
    /// <summary>
    /// 程序集分析器
    /// </summary>
    public class AssemblyAnalyzer
    {
        private readonly List<Assembly> _loadedAssemblies = new List<Assembly>();
        private readonly System.Collections.Concurrent.ConcurrentDictionary<Type, bool> _analyzedTypes = new System.Collections.Concurrent.ConcurrentDictionary<Type, bool>();
        private readonly Dictionary<Type, bool> _generatableTypes = new Dictionary<Type, bool>();
        private readonly System.Collections.Concurrent.ConcurrentBag<TestEntryInfo> _testEntries = new System.Collections.Concurrent.ConcurrentBag<TestEntryInfo>();
        private readonly System.Collections.Concurrent.ConcurrentBag<GeneratorInfo> _generators = new System.Collections.Concurrent.ConcurrentBag<GeneratorInfo>();
        
        // 配置选项
        public string? NamespacePrefix { get; set; }
        public List<string> ExcludePatterns { get; set; } = new List<string>();
        public int MaxRecursionDepth { get; set; } = 10;
        public bool ParallelAnalysis { get; set; } = true;
        public int MaxParallelDegree { get; set; } = -1;

        /// <summary>
        /// 分析程序集
        /// </summary>
        public AnalysisResult Analyze(string root, List<string>? references = null)
        {
            var startTime = DateTime.Now;
            
            try
            {
                // 加载程序集
                LoadAssemblies(root, references);
                
                // 分析类型
                var types = GetTypesToAnalyze();
                
                // 生成测试入口
                if (ParallelAnalysis && MaxParallelDegree != 1)
                {
                    var parallelOptions = new System.Threading.Tasks.ParallelOptions
                    {
                        MaxDegreeOfParallelism = MaxParallelDegree > 0 ? MaxParallelDegree : Environment.ProcessorCount
                    };
                    
                    System.Threading.Tasks.Parallel.ForEach(types, parallelOptions, type =>
                    {
                        AnalyzeType(type);
                    });
                }
                else
                {
                    foreach (var type in types)
                    {
                        AnalyzeType(type);
                    }
                }
                
                // 生成基础类型生成器信息
                GenerateBasicGenerators();
                
                var endTime = DateTime.Now;
                
                return new AnalysisResult
                {
                    TestEntries = _testEntries.ToList(),
                    Generators = _generators.ToList(),
                    Statistics = new AnalysisStatistics
                    {
                        TotalTypesAnalyzed = _analyzedTypes.Count,
                        TotalMethodsFound = _testEntries.Count,
                        TotalTestEntriesGenerated = _testEntries.Count,
                        AnalysisTimeMs = (int)(endTime - startTime).TotalMilliseconds
                    }
                };
            }
            catch (Exception ex)
            {
                throw new AnalysisException($"分析失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 加载程序集
        /// </summary>
        private void LoadAssemblies(string root, List<string>? references)
        {
            // 加载主程序集
            if (File.Exists(root))
            {
                _loadedAssemblies.Add(Assembly.LoadFrom(root));
            }
            else if (Directory.Exists(root))
            {
                var files = Directory.GetFiles(root, "*.dll")
                    .Concat(Directory.GetFiles(root, "*.exe"));
                    
                foreach (var file in files)
                {
                    try
                    {
                        _loadedAssemblies.Add(Assembly.LoadFrom(file));
                    }
                    catch
                    {
                        // 忽略无法加载的程序集
                    }
                }
            }
            else
            {
                throw new ArgumentException($"路径不存在: {root}");
            }
            
            // 加载引用程序集
            if (references != null)
            {
                foreach (var reference in references)
                {
                    try
                    {
                        _loadedAssemblies.Add(Assembly.LoadFrom(reference));
                    }
                    catch
                    {
                        // 忽略无法加载的引用
                    }
                }
            }
        }

        /// <summary>
        /// 获取要分析的类型
        /// </summary>
        private List<Type> GetTypesToAnalyze()
        {
            var types = new List<Type>();
            
            foreach (var assembly in _loadedAssemblies)
            {
                try
                {
                    var assemblyTypes = assembly.GetTypes()
                        .Where(t => t.IsPublic && !t.IsNested)
                        .Where(t => !TypeHelper.IsCompilerGenerated(t))
                        .Where(t => string.IsNullOrEmpty(NamespacePrefix) || 
                                   (t.Namespace?.StartsWith(NamespacePrefix) ?? false))
                        .Where(t => !ExcludePatterns.Any(pattern => 
                                   t.FullName?.Contains(pattern) ?? false));
                                   
                    types.AddRange(assemblyTypes);
                }
                catch
                {
                    // 忽略无法获取类型的程序集
                }
            }
            
            return types;
        }

        /// <summary>
        /// 分析类型
        /// </summary>
        private void AnalyzeType(Type type)
        {
            if (!_analyzedTypes.TryAdd(type, true))
                return;
                
            // 分析静态方法
            var staticMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => !m.IsSpecialName && !TypeHelper.IsCompilerGenerated(m));
                
            foreach (var method in staticMethods)
            {
                AnalyzeMethod(method, type, true);
            }
            
            // 分析实例方法
            var instanceMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => !m.IsSpecialName && !TypeHelper.IsCompilerGenerated(m))
                .Where(m => m.DeclaringType == type); // 排除继承的方法
                
            foreach (var method in instanceMethods)
            {
                AnalyzeMethod(method, type, false);
            }
            
            // 不再分析属性（跳过 getter/setter）
            
            // 分析索引器
            var indexers = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetIndexParameters().Length > 0);
                
            foreach (var indexer in indexers)
            {
                AnalyzeIndexer(indexer, type);
            }
        }


    /// <summary>
    /// 分析方法
    /// </summary>
    private void AnalyzeMethod(MethodInfo method, Type declaringType, bool isStatic)
    {
        // 检查所有参数是否可生成
        var parameters = method.GetParameters();

        // 过滤掉没有参数的方法（对于实例方法，除了实例本身外没有其他参数）
        if (parameters.Length == 0)
            return;

        if (!isStatic && !TypeHelper.CanGenerateType(declaringType))
            return;

        foreach (var param in parameters)
        {
            if (!TypeHelper.CanGenerateType(param.ParameterType))
                return;
        }

        // 创建测试入口
        var testEntry = new TestEntryInfo
        {
            Function = GenerateFunctionSignature(method),
            Symbol = "WFuzzGen." + TypeHelper.GetMethodIdentifier(method),
            Metadata = new TestEntryMetadata
            {
                Assembly = method.DeclaringType.Assembly.GetName().Name,
                Namespace = method.DeclaringType.Namespace,
                DeclaringType = method.DeclaringType.Name,
                MemberType = "method",
                MethodType = isStatic ? "static" : "instance",
                ReturnType = TypeHelper.GetTypeIdentifier(method.ReturnType),
                IsAsync = IsAsyncMethod(method),
                IsGeneric = method.IsGenericMethodDefinition,
                AccessLevel = "public",
                IsDisposable = typeof(IDisposable).IsAssignableFrom(declaringType)
            }
        };

        // 添加参数信息
        if (!isStatic)
        {
            testEntry.Arguments.Add(new ArgumentInfo
            {
                TypeId = TypeHelper.GetTypeIdentifier(declaringType),
                TypeFullName = TypeHelper.GetTypeFullName(declaringType),
                DefaultGenerator = GetDefaultGenerator(declaringType),
                ParameterName = "instance",
                IsOptional = false,
                IsInstance = true,
                OriginalType = declaringType
            });
        }

        foreach (var param in parameters)
        {
            testEntry.Arguments.Add(new ArgumentInfo
            {
                TypeId = TypeHelper.GetTypeIdentifier(param.ParameterType),
                TypeFullName = TypeHelper.GetTypeFullName(param.ParameterType), // 添加完整类型名
                DefaultGenerator = GetDefaultGenerator(param.ParameterType),
                ParameterName = param.Name,
                IsOptional = param.IsOptional,
                DefaultValue = param.DefaultValue,
                OriginalType = param.ParameterType // 保存原始类型信息
            });
        }

        _testEntries.Add(testEntry);
    }

        /// <summary>
        /// 分析属性
        /// </summary>
        private void AnalyzeProperty(PropertyInfo property, Type declaringType)
        {
            var isStatic = property.GetMethod?.IsStatic ?? property.SetMethod?.IsStatic ?? false;
            
            // 估算行号信息
            var (lineBegin, lineEnd) = EstimatePropertyLines(property);
            
            // 不分析 getter（因为 getter 没有参数）
            // 只分析 setter（因为 setter 有一个 value 参数）
            if (property.CanWrite && property.SetMethod != null && property.SetMethod.IsPublic)
            {
                var getterEntry = new TestEntryInfo
                {
                    Function = $"{declaringType.Name}.{property.Name} {{ get; }}",
                    Symbol = $"WFuzzGen.{TypeHelper.GetTypeIdentifier(declaringType)}_get_{property.Name}",
                    Metadata = new TestEntryMetadata
                    {
                        Assembly = declaringType.Assembly.GetName().Name ?? "Unknown",
                        Namespace = declaringType.Namespace ?? "Unknown",
                        DeclaringType = declaringType.Name,
                        MemberType = "property",
                        MethodType = isStatic ? "static" : "instance",
                        ReturnType = TypeHelper.GetTypeIdentifier(property.PropertyType),
                        AccessLevel = "public",
                        IsDisposable = typeof(IDisposable).IsAssignableFrom(declaringType),
                        LineBegin = lineBegin,
                        LineEnd = lineEnd,
                        LineCount = lineEnd - lineBegin + 1,
                        SourceFile = $"{declaringType.Namespace ?? "Unknown"}.{declaringType.Name}.cs"
                    }
                };
                
                if (!isStatic)
                {
                    getterEntry.Arguments.Add(new ArgumentInfo
                    {
                        TypeId = TypeHelper.GetTypeIdentifier(declaringType),
                        DefaultGenerator = GetDefaultGenerator(declaringType),
                        ParameterName = "instance",
                        IsOptional = false,
                        IsInstance = true
                    });
                }
                
                _testEntries.Add(getterEntry);
            }
            
            // 分析setter
            if (property.CanWrite && property.SetMethod != null && property.SetMethod.IsPublic)
            {
                var setterEntry = new TestEntryInfo
                {
                    Function = $"{declaringType.Name}.{property.Name} {{ set; }}",
                    Symbol = $"WFuzzGen.{TypeHelper.GetTypeIdentifier(declaringType)}_set_{property.Name}",
                    Metadata = new TestEntryMetadata
                    {
                        Assembly = declaringType.Assembly.GetName().Name ?? "Unknown",
                        Namespace = declaringType.Namespace ?? "Unknown",
                        DeclaringType = declaringType.Name,
                        MemberType = "property",
                        MethodType = isStatic ? "static" : "instance",
                        ReturnType = "void",
                        AccessLevel = "public",
                        IsDisposable = typeof(IDisposable).IsAssignableFrom(declaringType),
                        LineBegin = lineBegin,
                        LineEnd = lineEnd,
                        LineCount = lineEnd - lineBegin + 1,
                        SourceFile = $"{declaringType.Namespace ?? "Unknown"}.{declaringType.Name}.cs"
                    }
                };
                
                if (!isStatic)
                {
                    setterEntry.Arguments.Add(new ArgumentInfo
                    {
                        TypeId = TypeHelper.GetTypeIdentifier(declaringType),
                        DefaultGenerator = GetDefaultGenerator(declaringType),
                        ParameterName = "instance",
                        IsOptional = false,
                        IsInstance = true
                    });
                }
                
                setterEntry.Arguments.Add(new ArgumentInfo
                {
                    TypeId = TypeHelper.GetTypeIdentifier(property.PropertyType),
                    DefaultGenerator = GetDefaultGenerator(property.PropertyType),
                    ParameterName = "value",
                    IsOptional = false
                });
                
                _testEntries.Add(setterEntry);
            }
        }

        /// <summary>
        /// 分析索引器
        /// </summary>
        private void AnalyzeIndexer(PropertyInfo indexer, Type declaringType)
        {
            var indexParams = indexer.GetIndexParameters();
            
            // 索引器总是有参数的，所以不需要过滤
            
            // 估算行号信息
            var (lineBegin, lineEnd) = EstimateIndexerLines(indexer);
            
            // getter
            if (indexer.CanRead && indexer.GetMethod != null && indexer.GetMethod.IsPublic)
            {
                var paramTypes = string.Join("_", indexParams.Select(p => TypeHelper.GetTypeIdentifier(p.ParameterType)));
                var getterEntry = new TestEntryInfo
                {
                    Function = $"{declaringType.Name}[{string.Join(", ", indexParams.Select(p => p.ParameterType.Name))}] {{ get; }}",
                    Symbol = $"WFuzzGen.{TypeHelper.GetTypeIdentifier(declaringType)}_get_Item_{paramTypes}",
                    Metadata = new TestEntryMetadata
                    {
                        Assembly = declaringType.Assembly.GetName().Name ?? "Unknown",
                        Namespace = declaringType.Namespace ?? "Unknown",
                        DeclaringType = declaringType.Name,
                        MemberType = "indexer",
                        MethodType = "instance",
                        ReturnType = TypeHelper.GetTypeIdentifier(indexer.PropertyType),
                        AccessLevel = "public",
                        IsDisposable = typeof(IDisposable).IsAssignableFrom(declaringType),
                        LineBegin = lineBegin,
                        LineEnd = lineEnd,
                        LineCount = lineEnd - lineBegin + 1,
                        SourceFile = $"{declaringType.Namespace ?? "Unknown"}.{declaringType.Name}.cs"
                    }
                };
                
                getterEntry.Arguments.Add(new ArgumentInfo
                {
                    TypeId = TypeHelper.GetTypeIdentifier(declaringType),
                    TypeFullName = TypeHelper.GetTypeFullName(declaringType),
                    DefaultGenerator = GetDefaultGenerator(declaringType),
                    ParameterName = "instance",
                    IsOptional = false,
                    IsInstance = true,
                    OriginalType = declaringType
                });
                
                foreach (var param in indexParams)
                {
                    getterEntry.Arguments.Add(new ArgumentInfo
                    {
                        TypeId = TypeHelper.GetTypeIdentifier(param.ParameterType),
                        TypeFullName = TypeHelper.GetTypeFullName(param.ParameterType),
                        DefaultGenerator = GetDefaultGenerator(param.ParameterType),
                        ParameterName = param.Name ?? $"index{Array.IndexOf(indexParams, param)}",
                        IsOptional = false,
                        OriginalType = param.ParameterType
                    });
                }
                
                _testEntries.Add(getterEntry);
            }
            
            // setter
            if (indexer.CanWrite && indexer.SetMethod != null && indexer.SetMethod.IsPublic)
            {
                var paramTypes = string.Join("_", indexParams.Select(p => TypeHelper.GetTypeIdentifier(p.ParameterType)));
                var setterEntry = new TestEntryInfo
                {
                    Function = $"{declaringType.Name}[{string.Join(", ", indexParams.Select(p => p.ParameterType.Name))}] {{ set; }}",
                    Symbol = $"WFuzzGen.{TypeHelper.GetTypeIdentifier(declaringType)}_set_Item_{paramTypes}",
                    Metadata = new TestEntryMetadata
                    {
                        Assembly = declaringType.Assembly.GetName().Name ?? "Unknown",
                        Namespace = declaringType.Namespace ?? "Unknown",
                        DeclaringType = declaringType.Name,
                        MemberType = "indexer",
                        MethodType = "instance",
                        ReturnType = "void",
                        AccessLevel = "public",
                        IsDisposable = typeof(IDisposable).IsAssignableFrom(declaringType),
                        LineBegin = lineBegin,
                        LineEnd = lineEnd,
                        LineCount = lineEnd - lineBegin + 1,
                        SourceFile = $"{declaringType.Namespace ?? "Unknown"}.{declaringType.Name}.cs"
                    }
                };
                
                setterEntry.Arguments.Add(new ArgumentInfo
                {
                    TypeId = TypeHelper.GetTypeIdentifier(declaringType),
                    TypeFullName = TypeHelper.GetTypeFullName(declaringType),
                    DefaultGenerator = GetDefaultGenerator(declaringType),
                    ParameterName = "instance",
                    IsOptional = false,
                    IsInstance = true,
                    OriginalType = declaringType
                });
                
                foreach (var param in indexParams)
                {
                    setterEntry.Arguments.Add(new ArgumentInfo
                    {
                        TypeId = TypeHelper.GetTypeIdentifier(param.ParameterType),
                        TypeFullName = TypeHelper.GetTypeFullName(param.ParameterType),
                        DefaultGenerator = GetDefaultGenerator(param.ParameterType),
                        ParameterName = param.Name ?? $"index{Array.IndexOf(indexParams, param)}",
                        IsOptional = false,
                        OriginalType = param.ParameterType
                    });
                }
                
                setterEntry.Arguments.Add(new ArgumentInfo
                {
                    TypeId = TypeHelper.GetTypeIdentifier(indexer.PropertyType),
                    TypeFullName = TypeHelper.GetTypeFullName(indexer.PropertyType),
                    DefaultGenerator = GetDefaultGenerator(indexer.PropertyType),
                    ParameterName = "value",
                    IsOptional = false,
                    OriginalType = indexer.PropertyType
                });
                
                _testEntries.Add(setterEntry);
            }
        }

        /// <summary>
        /// 生成函数签名
        /// </summary>
        private string GenerateFunctionSignature(MethodInfo method)
        {
            var parameters = method.GetParameters();
            var paramStrings = parameters.Select(p => 
                $"{TypeHelper.GetTypeIdentifier(p.ParameterType)} {p.Name ?? "param"}");
                
            return $"{method.DeclaringType?.Name ?? "Unknown"}.{method.Name}({string.Join(", ", paramStrings)})";
        }

        /// <summary>
        /// 判断是否为异步方法
        /// </summary>
        private bool IsAsyncMethod(MethodInfo method)
        {
            return method.GetCustomAttribute<AsyncStateMachineAttribute>() != null ||
                   method.ReturnType.IsGenericType && 
                   (method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>) ||
                    method.ReturnType == typeof(Task));
        }

        /// <summary>
        /// 获取默认生成器
        /// </summary>
        private string GetDefaultGenerator(Type type)
        {
            var typeId = TypeHelper.GetTypeIdentifier(type);
            
            // 基础类型生成器
            var basicGenerators = new Dictionary<string, string>
            {
                { "int", "WFuzz.Generators.IntGenerator" },
                { "string", "WFuzz.Generators.StringGenerator" },
                { "bool", "WFuzz.Generators.BoolGenerator" },
                { "byte", "WFuzz.Generators.ByteGenerator" },
                { "float", "WFuzz.Generators.FloatGenerator" },
                { "double", "WFuzz.Generators.DoubleGenerator" },
                { "System_DateTime", "WFuzz.Generators.DateTimeGenerator" },
                { "System_Guid", "WFuzz.Generators.GuidGenerator" },
                { "System_TimeSpan", "WFuzz.Generators.TimeSpanGenerator" }
            };
            
            if (basicGenerators.TryGetValue(typeId, out var generator))
                return generator;
                
            // 可空类型
            if (typeId.EndsWith("?"))
            {
                return $"WFuzz.Generators.Nullable{typeId.TrimEnd('?')}Generator";
            }
            
            // 数组类型
            if (typeId.EndsWith("_array"))
            {
                return $"WFuzzGen.{typeId}_generator";
            }
            
            // 其他类型使用泛型生成器
            return $"WFuzzGen.{typeId}_generator";
        }

        /// <summary>
        /// 生成基础类型生成器信息
        /// </summary>
        private void GenerateBasicGenerators()
        {
            var basicTypes = new[]
            {
                ("int", "int", "WFuzz.Generators.IntGenerator"),
                ("string", "string", "WFuzz.Generators.StringGenerator"),
                ("bool", "bool", "WFuzz.Generators.BoolGenerator"),
                ("byte", "byte", "WFuzz.Generators.ByteGenerator"),
                ("float", "float", "WFuzz.Generators.FloatGenerator"),
                ("double", "double", "WFuzz.Generators.DoubleGenerator"),
                ("System.DateTime", "DateTime", "WFuzz.Generators.DateTimeGenerator"),
                ("System.Guid", "Guid", "WFuzz.Generators.GuidGenerator"),
                ("System.TimeSpan", "TimeSpan", "WFuzz.Generators.TimeSpanGenerator"),
                ("int?", "int?", "WFuzz.Generators.NullableIntGenerator")
            };
            
            foreach (var (typeId, displayName, generatorName) in basicTypes)
            {
                _generators.Add(new GeneratorInfo
                {
                    TypeId = typeId,
                    DisplayName = displayName,
                    Generators = new List<GeneratorDefinition>
                    {
                        new GeneratorDefinition
                        {
                            DisplayName = "<basic>",
                            MangledName = generatorName,
                            IsDefault = true,
                            Arguments = new List<GeneratorArgument>()
                        }
                    }
                });
            }
        }

        /// <summary>
        /// 估算方法的行号（简化实现）
        /// </summary>
        private (int lineBegin, int lineEnd) EstimateMethodLines(MethodInfo method)
        {
            // 基于方法复杂度估算行数
            var baseLineNumber = GetEstimatedBaseLineNumber(method.DeclaringType);
            var methodIndex = GetMethodIndex(method);
            var estimatedLineCount = EstimateMethodLineCount(method);
            
            var lineBegin = baseLineNumber + (methodIndex * 10);
            var lineEnd = lineBegin + estimatedLineCount - 1;
            
            return (lineBegin, lineEnd);
        }

        /// <summary>
        /// 估算属性的行号
        /// </summary>
        private (int lineBegin, int lineEnd) EstimatePropertyLines(PropertyInfo property)
        {
            var baseLineNumber = GetEstimatedBaseLineNumber(property.DeclaringType);
            var propertyIndex = GetPropertyIndex(property);
            
            // 属性通常较短
            var lineBegin = baseLineNumber + (propertyIndex * 5);
            var lineEnd = lineBegin + 3;
            
            return (lineBegin, lineEnd);
        }

        /// <summary>
        /// 估算索引器的行号
        /// </summary>
        private (int lineBegin, int lineEnd) EstimateIndexerLines(PropertyInfo indexer)
        {
            var baseLineNumber = GetEstimatedBaseLineNumber(indexer.DeclaringType);
            var indexerIndex = GetPropertyIndex(indexer);
            
            // 索引器通常比属性稍长
            var lineBegin = baseLineNumber + (indexerIndex * 6);
            var lineEnd = lineBegin + 5;
            
            return (lineBegin, lineEnd);
        }

        /// <summary>
        /// 获取类型的估算基础行号
        /// </summary>
        private int GetEstimatedBaseLineNumber(Type? type)
        {
            if (type == null) return 10;
            
            // 基于命名空间和类型名的哈希值生成一个稳定的基础行号
            var hash = (type.Namespace ?? "").GetHashCode() ^ type.Name.GetHashCode();
            return 10 + Math.Abs(hash) % 100;
        }

        /// <summary>
        /// 获取方法在类型中的索引
        /// </summary>
        private int GetMethodIndex(MethodInfo method)
        {
            var methods = method.DeclaringType?.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static) ?? Array.Empty<MethodInfo>();
            return Array.IndexOf(methods, method);
        }

        /// <summary>
        /// 获取属性在类型中的索引
        /// </summary>
        private int GetPropertyIndex(PropertyInfo property)
        {
            var properties = property.DeclaringType?.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static) ?? Array.Empty<PropertyInfo>();
            return Array.IndexOf(properties, property);
        }

        /// <summary>
        /// 估算方法的行数
        /// </summary>
        private int EstimateMethodLineCount(MethodInfo method)
        {
            var baseCount = 3; // 方法签名 + 大括号
            
            // 根据参数数量增加行数
            baseCount += method.GetParameters().Length;
            
            // 异步方法通常更长
            if (IsAsyncMethod(method))
                baseCount += 5;
            
            // 根据返回类型调整
            if (method.ReturnType != typeof(void))
                baseCount += 2;
            
            return baseCount;
        }
    }

    /// <summary>
    /// 分析结果
    /// </summary>
    public class AnalysisResult
    {
        public List<TestEntryInfo> TestEntries { get; set; } = new List<TestEntryInfo>();
        public List<GeneratorInfo> Generators { get; set; } = new List<GeneratorInfo>();
        public AnalysisStatistics Statistics { get; set; } = new AnalysisStatistics();
    }

    /// <summary>
    /// 分析统计信息
    /// </summary>
    public class AnalysisStatistics
    {
        public int TotalTypesAnalyzed { get; set; }
        public int TotalMethodsFound { get; set; }
        public int TotalTestEntriesGenerated { get; set; }
        public int AnalysisTimeMs { get; set; }
    }

    /// <summary>
    /// 分析异常
    /// </summary>
    public class AnalysisException : Exception
    {
        public AnalysisException(string message) : base(message) { }
        public AnalysisException(string message, Exception innerException) : base(message, innerException) { }
    }
}