# C# Fuzz 测试驱动生成器

这是一个用于生成 C# 模糊测试驱动的工具，可以自动分析 .NET 程序集并生成相应的测试入口代码。

## 项目结构

```
CSHARPFUZZ/
├── WFuzz/                    # 核心库
│   ├── ICaller.cs           # 测试调用接口
│   ├── IGenerator.cs        # 类型生成器接口  
│   ├── FuzzInput.cs         # 模糊测试输入
│   ├── ExceptionResult.cs   # 异常结果包装
│   └── Generators/          # 基础类型生成器
│       ├── AbstractGenerator.cs
│       ├── IntGenerator.cs
│       ├── StringGenerator.cs
│       ├── BoolGenerator.cs
│       ├── ByteGenerator.cs
│       ├── FloatGenerator.cs
│       ├── DoubleGenerator.cs
│       ├── DateTimeGenerator.cs
│       ├── GuidGenerator.cs
│       ├── TimeSpanGenerator.cs
│       └── NullableIntGenerator.cs
├── WFuzzGen/                # 代码生成工具
│   ├── Program.cs           # 命令行入口
│   ├── AssemblyAnalyzer.cs  # 程序集分析器
│   ├── CodeGenerator.cs     # 代码生成器
│   ├── TypeHelper.cs        # 类型辅助工具
│   ├── Models/              # 数据模型
│   │   ├── TestEntryInfo.cs
│   │   ├── GeneratorInfo.cs
│   │   └── TypeInfo.cs
│   └── Templates/           # 代码模板
│       ├── StaticMethodTemplate.cs
│       ├── InstanceMethodTemplate.cs
│       ├── PropertyTemplate.cs
│       └── IndexerTemplate.cs
├── TestLibrary/             # 测试示例库
│   └── SampleClasses.cs
├── WFuzz.sln               # 解决方案文件
├── build.ps1               # 构建脚本
├── run-example.ps1         # 运行示例脚本
└── README.md               # 项目文档

```

## 功能特性

- ✅ 自动分析 .NET 程序集
- ✅ 生成测试入口代码
- ✅ 支持静态和实例方法
- ✅ 支持属性的 getter/setter
- ✅ 支持索引器
- ✅ 支持异步方法
- ✅ 支持 IDisposable 资源管理
- ✅ 支持泛型类型
- ✅ 支持可空类型
- ✅ 并行分析支持
- ✅ 循环依赖检测

## 使用方法

### 1. 构建项目

```bash
dotnet build
```

### 2. 运行分析器

```bash
WFuzzGen <程序集路径> <输出目录> [选项]
```

选项:
- `--references <dll1,dll2,...>` - 引用程序集列表
- `--namespace <prefix>` - 命名空间前缀过滤
- `--exclude <pattern1,pattern2>` - 排除模式
- `--parallel <true/false>` - 启用并行分析
- `--max-parallel <n>` - 最大并行度

示例:
```bash
WFuzzGen ./TestLibrary.dll ./Output --namespace TestLibrary --parallel true
```

### 3. 查看生成结果

生成的代码将输出到指定目录，包括:
- 测试入口类文件 (.cs)
- 自定义生成器文件 (.cs)
- 分析结果 JSON 文件 (analysis_result.json)
- 项目文件 (.csproj)

## 支持的类型

### 基础类型
- `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`
- `float`, `double`, `decimal`
- `bool`, `char`, `string`

### 系统类型
- `DateTime`, `TimeSpan`, `Guid`, `Uri`
- `Nullable<T>` (可空值类型)
- 数组类型

### 集合类型
- `List<T>`, `Dictionary<TKey, TValue>`
- `HashSet<T>`, `Queue<T>`, `Stack<T>`

## 生成的代码示例

### 静态方法
```csharp
public class TestLibrary_Calculator_Multiply_int_int : WFuzz.ICaller
{
    public object Call(WFuzz.FuzzInput input)
    {
        try
        {
            var arg0 = input.GenerateArgument<int>(0);
            var arg1 = input.GenerateArgument<int>(1);
            return Calculator.Multiply(arg0, arg1);
        }
        catch (Exception ex)
        {
            return new WFuzz.ExceptionResult(ex);
        }
    }
}
```

### 实例方法
```csharp
public class TestLibrary_Calculator_Add_int_int : WFuzz.ICaller
{
    public object Call(WFuzz.FuzzInput input)
    {
        try
        {
            var instance = input.GenerateArgument<TestLibrary.Calculator>(0);
            var arg1 = input.GenerateArgument<int>(1);
            var arg2 = input.GenerateArgument<int>(2);
            return instance.Add(arg1, arg2);
        }
        catch (Exception ex)
        {
            return new WFuzz.ExceptionResult(ex);
        }
    }
}
```

### 属性访问器
```csharp
public class TestLibrary_StringProcessor_get_Prefix : WFuzz.ICaller
{
    public object Call(WFuzz.FuzzInput input)
    {
        try
        {
            var instance = input.GenerateArgument<TestLibrary.StringProcessor>(0);
            return instance.Prefix;
        }
        catch (Exception ex)
        {
            return new WFuzz.ExceptionResult(ex);
        }
    }
}
```

## 命名规则

生成的类名遵循以下规则：

1. **基础类型**：直接使用关键字（如 `int`, `string`）
2. **数组类型**：类型名_array（如 `int_array`）
3. **泛型类型**：用下划线分隔类型参数（如 `List_int`, `Dictionary_string_int`）
4. **命名空间**：点号替换为下划线（如 `System_DateTime`）
5. **方法标识**：类名_方法名_参数类型列表

## 异常处理

所有生成的测试入口都包含异常处理：

- 预期异常（如 `ArgumentException`）被归类为 `Expected`
- 意外异常被归类为 `Unexpected`
- 致命异常（如 `OutOfMemoryException`）被归类为 `Fatal`

## 高级特性

### 并行分析

启用并行分析可以加快大型程序集的处理速度：

```bash
WFuzzGen MyLargeAssembly.dll ./Output --parallel true --max-parallel 8
```

### 循环依赖检测

工具会自动检测类型间的循环依赖，并在生成代码时处理这些情况。

### 资源管理

对于实现 `IDisposable` 的类型，生成的代码会使用 `using` 语句确保资源正确释放。

### 异步方法支持

异步方法会被同步调用，使用 `ConfigureAwait(false).GetAwaiter().GetResult()` 避免死锁。

## 扩展生成器

可以通过继承 `AbstractGenerator<T>` 来创建自定义类型生成器：

```csharp
public class MyCustomTypeGenerator : AbstractGenerator<MyCustomType>
{
    protected override MyCustomType GenerateTyped(FuzzInput input)
    {
        // 实现生成逻辑
        return new MyCustomType();
    }
}
```

## 输出格式

### entrypoint.json 结构

```json
{
  "binaries": [
    {
      "binary": "指向目标程序可执行文件的绝对路径",
      "files": [
        {
          "file": ".dll",
          "language": "c#",
          "lines": 10,
          "functions": [
            {
              "function_brief":"add",
              "function": "Test.add",
              "symbol": "Test_add",
              "arguments": [
                {
                  "typeid": "i",
                  "display": "int",
                  "default_generator": "prev_length"
                }
              ],
            }
          ]
        }
      ],
      "type_generators": [
        {
          "typeid": "i",
          "display_name": "int",
          "generators": [
            {
              "display_name": "<prev_length>",
              "mangled_name": "prev_length",
              "is_default": true,
              "arguments": [
                {
                  "typeid": "i",
                  "display": "int",
                  "default_generator": "prev_length"
                }
              ]
            }
          ]
        }
      ]
    }
  ]
}
```

## 限制和注意事项

1. **编译器生成的类型**：自动跳过匿名类型和编译器生成的类型
2. **泛型约束**：目前对复杂泛型约束的支持有限
3. **特殊方法**：不支持扩展方法和部分特殊方法
4. **程序集加载**：需要所有依赖项都可以被加载
