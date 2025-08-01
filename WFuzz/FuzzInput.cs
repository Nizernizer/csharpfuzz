using System;
using System.Collections.Generic;
using System.Linq;

namespace WFuzz
{
    /// <summary>
    /// 模糊测试输入数据类
    /// </summary>
    public class FuzzInput
    {
        private readonly byte[] _data;
        private int _position;
        private readonly Dictionary<Type, IGenerator> _generators;

        public FuzzInput(byte[] data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _position = 0;
            _generators = InitializeGenerators();
        }

        /// <summary>
        /// 生成指定类型的参数
        /// </summary>
        /// <typeparam name="T">参数类型</typeparam>
        /// <param name="index">参数索引</param>
        /// <returns>生成的参数值</returns>
        public T GenerateArgument<T>(int index)
        {
            var type = typeof(T);
            
            // 处理泛型参数类型（T, TKey, TValue 等）
            if (type.IsGenericParameter)
            {
                // 对于泛型参数，返回默认值或 null
                return default(T)!;
            }
            
            // 尝试使用注册的生成器
            if (_generators.TryGetValue(type, out var generator))
            {
                var result = generator.Generate<T>(this);
                if (result != null)
                    return (T)result;
            }
            
            // 基础类型的内联生成
            if (type == typeof(int))
            {
                return (T)(object)GenerateInt();
            }
            if (type == typeof(string))
            {
                return (T)(object)GenerateString();
            }
            if (type == typeof(bool))
            {
                return (T)(object)GenerateBool();
            }
            if (type == typeof(double))
            {
                return (T)(object)GenerateDouble();
            }
            if (type == typeof(float))
            {
                return (T)(object)GenerateFloat();
            }
            if (type == typeof(byte))
            {
                return (T)(object)GetNextByte();
            }
            if (type == typeof(object))
            {
                // 为 object 类型返回一个简单的字符串
                return (T)(object)GenerateString();
            }
            
            // 处理数组类型
            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                var length = Math.Abs(GenerateInt()) % 10; // 限制数组长度
                var array = Array.CreateInstance(elementType!, length);
                
                for (int i = 0; i < length; i++)
                {
                    // 使用反射调用 GenerateArgument 方法
                    var method = GetType().GetMethod(nameof(GenerateArgument))!.MakeGenericMethod(elementType!);
                    var element = method.Invoke(this, new object[] { index + i + 1 });
                    array.SetValue(element, i);
                }
                
                return (T)(object)array;
            }
            
            // 处理泛型类型
            if (type.IsGenericType)
            {
                var genericDef = type.GetGenericTypeDefinition();
                
                // List<T>
                if (genericDef == typeof(List<>))
                {
                    var elementType = type.GetGenericArguments()[0];
                    var listType = typeof(List<>).MakeGenericType(elementType);
                    var list = Activator.CreateInstance(listType);
                    
                    var addMethod = listType.GetMethod("Add");
                    var count = Math.Abs(GenerateInt()) % 5; // 限制列表大小
                    
                    for (int i = 0; i < count; i++)
                    {
                        var method = GetType().GetMethod(nameof(GenerateArgument))!.MakeGenericMethod(elementType);
                        var element = method.Invoke(this, new object[] { index + i + 1 });
                        addMethod!.Invoke(list, new[] { element });
                    }
                    
                    return (T)list!;
                }
                
                // Dictionary<TKey, TValue>
                if (genericDef == typeof(Dictionary<,>))
                {
                    var keyType = type.GetGenericArguments()[0];
                    var valueType = type.GetGenericArguments()[1];
                    var dictType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
                    var dict = Activator.CreateInstance(dictType);
                    
                    var addMethod = dictType.GetMethod("Add");
                    var count = Math.Abs(GenerateInt()) % 3; // 限制字典大小
                    
                    for (int i = 0; i < count; i++)
                    {
                        var keyMethod = GetType().GetMethod(nameof(GenerateArgument))!.MakeGenericMethod(keyType);
                        var valueMethod = GetType().GetMethod(nameof(GenerateArgument))!.MakeGenericMethod(valueType);
                        
                        var key = keyMethod.Invoke(this, new object[] { index + i * 2 + 1 });
                        var value = valueMethod.Invoke(this, new object[] { index + i * 2 + 2 });
                        
                        try
                        {
                            addMethod!.Invoke(dict, new[] { key, value });
                        }
                        catch
                        {
                            // 忽略重复键等错误
                        }
                    }
                    
                    return (T)dict!;
                }
            }
            
            // 尝试创建默认实例
            try
            {
                if (type.IsValueType)
                {
                    return default(T)!;
                }
                else
                {
                    // 尝试使用无参构造函数
                    var ctor = type.GetConstructor(Type.EmptyTypes);
                    if (ctor != null)
                    {
                        var instance = ctor.Invoke(null);
                        if (instance != null)
                            return (T)instance;
                    }
                    
                    // 尝试使用参数最少的构造函数
                    var ctors = type.GetConstructors();
                    if (ctors.Length > 0)
                    {
                        var simplestCtor = ctors.OrderBy(c => c.GetParameters().Length).First();
                        var parameters = simplestCtor.GetParameters();
                        var args = new object[parameters.Length];
                        
                        for (int i = 0; i < parameters.Length; i++)
                        {
                            var paramType = parameters[i].ParameterType;
                            var method = GetType().GetMethod(nameof(GenerateArgument))!.MakeGenericMethod(paramType);
                            args[i] = method.Invoke(this, new object[] { index + i + 1 }) ?? 
                                     (paramType.IsValueType ? Activator.CreateInstance(paramType) : null);
                        }
                        
                        var instance = simplestCtor.Invoke(args);
                        if (instance != null)
                            return (T)instance;
                    }
                }
            }
            catch
            {
                // 忽略创建失败
            }
            
            return default(T)!;
        }

        public bool GenerateBool()
        {
            return GetNextByte() % 2 == 0;
        }

        private int GenerateInt()
        {
            var bytes = GetNextBytes(4);
            return BitConverter.ToInt32(bytes, 0);
        }

        private double GenerateDouble()
        {
            var intValue = GenerateInt();
            return intValue / 1000.0; // 生成合理范围的 double
        }

        private float GenerateFloat()
        {
            var intValue = GenerateInt();
            return intValue / 1000.0f; // 生成合理范围的 float
        }

        private string GenerateString()
        {
            int length = GetNextByte() % 32;
            if (length == 0)
                return string.Empty;
                
            var chars = new char[length];
            for (int i = 0; i < length; i++)
            {
                // 生成可打印的 ASCII 字符
                chars[i] = (char)(32 + (GetNextByte() % 95));
            }
            return new string(chars);
        }

        private byte GetNextByte()
        {
            if (_position >= _data.Length)
                _position = 0;
            return _data[_position++];
        }

        private byte[] GetNextBytes(int count)
        {
            byte[] result = new byte[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = GetNextByte();
            }
            return result;
        }

        /// <summary>
        /// 初始化生成器
        /// </summary>
        private Dictionary<Type, IGenerator> InitializeGenerators()
        {
            var generators = new Dictionary<Type, IGenerator>();
            
            // 注册基础类型生成器
            generators[typeof(DateTime)] = new Generators.DateTimeGenerator();
            generators[typeof(Guid)] = new Generators.GuidGenerator();
            generators[typeof(TimeSpan)] = new Generators.TimeSpanGenerator();
            
            return generators;
        }
    }
}