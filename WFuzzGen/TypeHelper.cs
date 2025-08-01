using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace WFuzzGen
{
    /// <summary>
    /// 类型处理辅助类
    /// </summary>
    public static class TypeHelper
    {
        private static readonly HashSet<string> CSharpKeywords = new HashSet<string>
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
            "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this",
            "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
            "using", "virtual", "void", "volatile", "while"
        };

        private static readonly Dictionary<Type, string> BasicTypeNames = new Dictionary<Type, string>
        {
            { typeof(byte), "byte" },
            { typeof(sbyte), "sbyte" },
            { typeof(short), "short" },
            { typeof(ushort), "ushort" },
            { typeof(int), "int" },
            { typeof(uint), "uint" },
            { typeof(long), "long" },
            { typeof(ulong), "ulong" },
            { typeof(float), "float" },
            { typeof(double), "double" },
            { typeof(decimal), "decimal" },
            { typeof(bool), "bool" },
            { typeof(char), "char" },
            { typeof(string), "string" },
            { typeof(object), "object" },
            { typeof(void), "void" }
        };

        /// <summary>
        /// 获取类型的标识符名称
        /// </summary>
        public static string GetTypeIdentifier(Type? type)
        {
            if (type == null)
                return "void";

            // 基础类型
            if (BasicTypeNames.TryGetValue(type, out string basicName))
                return basicName;

            // 数组类型
            if (type.IsArray)
            {
                var elementType = GetTypeIdentifier(type.GetElementType());
                return $"{elementType}_array";
            }

            // 可空类型
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var underlyingType = type.GetGenericArguments()[0];
                return $"{GetTypeIdentifier(underlyingType)}?";
            }

            // 泛型类型
            if (type.IsGenericType)
            {
                var genericDef = type.GetGenericTypeDefinition();
                var baseName = GetSimpleTypeName(genericDef);
                var genericArgs = type.GetGenericArguments()
                    .Select(t => GetTypeIdentifier(t))
                    .ToArray();
                
                return $"{baseName}_{string.Join("_", genericArgs)}";
            }

            // 泛型参数（T, TKey, TValue 等）
            if (type.IsGenericParameter)
            {
                return type.Name;
            }

            // 普通类型
            return GetSimpleTypeName(type);
        }

        /// <summary>
        /// 获取类型的完整名称（用于代码生成）
        /// </summary>
        public static string GetTypeFullName(Type? type)
        {
            if (type == null)
                return "void";

            // 基础类型
            if (BasicTypeNames.TryGetValue(type, out string basicName))
                return basicName;

            // 数组类型
            if (type.IsArray)
            {
                var elementType = GetTypeFullName(type.GetElementType());
                return $"{elementType}[]";
            }

            // 可空类型
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var underlyingType = type.GetGenericArguments()[0];
                return $"{GetTypeFullName(underlyingType)}?";
            }

            // 泛型参数（T, TKey, TValue 等）
            if (type.IsGenericParameter)
            {
                // 对于泛型参数，返回 object 作为具体类型
                return "object";
            }

            // 泛型类型
            if (type.IsGenericType)
            {
                var genericDef = type.GetGenericTypeDefinition();
                // 获取不包含泛型参数数量的名称
                var typeName = genericDef.Name;
                var backtickIndex = typeName.IndexOf('`');
                if (backtickIndex > 0)
                {
                    typeName = typeName.Substring(0, backtickIndex);
                }
                
                var genericArgs = type.GetGenericArguments()
                    .Select(t => GetTypeFullName(t))
                    .ToArray();
                
                // 如果有命名空间，添加命名空间
                if (!string.IsNullOrEmpty(type.Namespace))
                {
                    return $"{type.Namespace}.{typeName}<{string.Join(", ", genericArgs)}>";
                }
                
                return $"{typeName}<{string.Join(", ", genericArgs)}>";
            }

            // 普通类型 - 返回命名空间.类型名（不包含程序集信息）
            if (!string.IsNullOrEmpty(type.Namespace))
            {
                return $"{type.Namespace}.{type.Name}";
            }
            
            return type.Name;
        }

        /// <summary>
        /// 获取方法的标识符名称
        /// </summary>
        public static string GetMethodIdentifier(MethodInfo method)
        {
            var parts = new List<string>();
            
            // 添加类型名
            parts.Add(GetTypeIdentifier(method.DeclaringType));
            
            // 添加方法名
            if (method.IsSpecialName)
            {
                // 处理属性、索引器、操作符等特殊方法
                parts.Add(GetSpecialMethodName(method));
            }
            else
            {
                parts.Add(method.Name);
            }
            
            // 处理泛型方法
            if (method.IsGenericMethodDefinition)
            {
                // 获取实际的类型参数或使用占位符
                var genericArgs = method.GetGenericArguments();
                if (genericArgs.Length > 0)
                {
                    // 对于开放泛型，使用具体类型
                    try
                    {
                        var concreteMethod = method.MakeGenericMethod(genericArgs.Select(t => typeof(object)).ToArray());
                        method = concreteMethod;
                    }
                    catch
                    {
                        // 如果无法创建具体方法，继续使用原方法
                    }
                }
            }
            
            // 添加参数类型
            var parameters = method.GetParameters();
            foreach (var param in parameters)
            {
                parts.Add(GetTypeIdentifier(param.ParameterType));
            }
            
            var identifier = string.Join("_", parts);
            return EnsureValidIdentifier(identifier);
        }

        /// <summary>
        /// 获取特殊方法名称
        /// </summary>
        private static string GetSpecialMethodName(MethodInfo method)
        {
            var name = method.Name;
            
            // 属性访问器
            if (name.StartsWith("get_") || name.StartsWith("set_"))
                return name;
            
            // 事件访问器
            if (name.StartsWith("add_") || name.StartsWith("remove_"))
                return name;
            
            // 操作符重载
            if (name.StartsWith("op_"))
                return name;
            
            return name;
        }

        /// <summary>
        /// 获取简单类型名称（将命名空间的点替换为下划线）
        /// </summary>
        private static string GetSimpleTypeName(Type type)
        {
            var fullName = type.FullName ?? type.Name;
            
            // 处理嵌套类的+号
            fullName = fullName.Replace('+', '_');
            
            // 处理泛型类型的`符号
            if (fullName.Contains('`'))
            {
                fullName = fullName.Substring(0, fullName.IndexOf('`'));
            }
            
            // 将点替换为下划线
            fullName = fullName.Replace('.', '_');
            
            return fullName;
        }

        /// <summary>
        /// 确保标识符有效
        /// </summary>
        private static string EnsureValidIdentifier(string identifier)
        {
            // 移除无效字符
            identifier = Regex.Replace(identifier, @"[^\w_]", "_");
            
            // 处理关键字冲突
            if (CSharpKeywords.Contains(identifier))
            {
                identifier = "__" + identifier;
            }
            
            // 处理长度限制
            if (identifier.Length > 200)
            {
                var hash = GetMD5Hash(identifier);
                identifier = identifier.Substring(0, 150) + "_" + hash.Substring(0, 8);
            }
            
            return identifier;
        }

        /// <summary>
        /// 计算MD5哈希
        /// </summary>
        private static string GetMD5Hash(string input)
        {
            using (var md5 = MD5.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(input);
                var hash = md5.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }

        /// <summary>
        /// 判断类型是否可以生成
        /// </summary>
        public static bool CanGenerateType(Type? type)
        {
            if (type == null)
                return false;

            // 基础类型都可以生成
            if (BasicTypeNames.ContainsKey(type))
                return true;

            // 已知的系统类型
            var knownTypes = new HashSet<Type>
            {
                typeof(DateTime), typeof(TimeSpan), typeof(Guid), typeof(Uri),
                typeof(DateTimeOffset), typeof(Version), typeof(object)
            };
            
            if (knownTypes.Contains(type))
                return true;

            // 数组类型
            if (type.IsArray)
                return CanGenerateType(type.GetElementType());

            // 可空类型
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                return CanGenerateType(type.GetGenericArguments()[0]);

            // 泛型参数（T, TKey, TValue 等）
            if (type.IsGenericParameter)
                return true; // 泛型参数总是可以生成（使用 object）

            // 泛型集合类型
            if (type.IsGenericType)
            {
                var genericDef = type.GetGenericTypeDefinition();
                var collectionTypes = new HashSet<Type>
                {
                    typeof(List<>), typeof(Dictionary<,>), typeof(HashSet<>),
                    typeof(Queue<>), typeof(Stack<>), typeof(LinkedList<>)
                };
                
                if (collectionTypes.Contains(genericDef))
                {
                    return type.GetGenericArguments().All(t => CanGenerateType(t));
                }
            }

            // 有公共构造函数的类型
            if (type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Any())
                return true;

            return false;
        }

        /// <summary>
        /// 检测是否为编译器生成的类型
        /// </summary>
        public static bool IsCompilerGenerated(Type type)
        {
            if (type.Name.Contains("<>") || type.Name.Contains("__"))
                return true;

            var compilerGeneratedAttr = typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute);
            return type.GetCustomAttribute(compilerGeneratedAttr) != null;
        }

        /// <summary>
        /// 检测是否为编译器生成的方法
        /// </summary>
        public static bool IsCompilerGenerated(MethodInfo method)
        {
            var compilerGeneratedAttr = typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute);
            return method.GetCustomAttribute(compilerGeneratedAttr) != null;
        }
    }
}