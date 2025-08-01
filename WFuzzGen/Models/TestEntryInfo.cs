using System;
using System.Collections.Generic;
using System.Reflection;

namespace WFuzzGen.Models
{
    /// <summary>
    /// 测试入口信息
    /// </summary>
    public class TestEntryInfo
    {
        /// <summary>
        /// 函数签名（人类可读）
        /// </summary>
        public string Function { get; set; } = string.Empty;

        /// <summary>
        /// 生成的符号名称
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// 元数据信息
        /// </summary>
        public TestEntryMetadata Metadata { get; set; }

        /// <summary>
        /// 参数信息列表
        /// </summary>
        public List<ArgumentInfo> Arguments { get; set; }

        public TestEntryInfo()
        {
            Arguments = new List<ArgumentInfo>();
            Metadata = new TestEntryMetadata();
        }
    }

    /// <summary>
    /// 测试入口元数据
    /// </summary>
    public class TestEntryMetadata
    {
        public string Assembly { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string DeclaringType { get; set; } = string.Empty;
        public string MemberType { get; set; } = string.Empty; // method|property|indexer|event|operator
        public string MethodType { get; set; } = string.Empty; // instance|static
        public string ReturnType { get; set; } = string.Empty;
        public bool IsAsync { get; set; }
        public bool IsGeneric { get; set; }
        public List<string> GenericConstraints { get; set; }
        public string AccessLevel { get; set; } = string.Empty;
        public bool IsDisposable { get; set; }
        public bool HasSideEffects { get; set; }
        public int ComplexityScore { get; set; }
        public int LineBegin { get; set; }
        public int LineEnd { get; set; }
        public int LineCount { get; set; }
        public string SourceFile { get; set; } = string.Empty;

        public TestEntryMetadata()
        {
            GenericConstraints = new List<string>();
        }
    }

    /// <summary>
    /// 参数信息
    /// </summary>
    public class ArgumentInfo
    {
        public string TypeId { get; set; } = string.Empty;
        public string TypeFullName { get; set; } = string.Empty; // 添加完整类型名
        public string DefaultGenerator { get; set; } = string.Empty;
        public string ParameterName { get; set; } = string.Empty;
        public bool IsOptional { get; set; }
        public object? DefaultValue { get; set; }
        public bool IsInstance { get; set; } // 标记是否为实例参数
        public Type? OriginalType { get; set; } // 保存原始类型信息
    }
}