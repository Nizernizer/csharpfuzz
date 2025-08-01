using System;
using System.Collections.Generic;

namespace WFuzzGen.Models
{
    /// <summary>
    /// 类型信息
    /// </summary>
    public class TypeInfo
    {
        /// <summary>
        /// 类型的完整名称
        /// </summary>
        public string FullName { get; set; } = string.Empty;

        /// <summary>
        /// 类型的命名空间
        /// </summary>
        public string Namespace { get; set; } = string.Empty;

        /// <summary>
        /// 类型名称（不含命名空间）
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 是否为基础类型
        /// </summary>
        public bool IsBasicType { get; set; }

        /// <summary>
        /// 是否为数组类型
        /// </summary>
        public bool IsArray { get; set; }

        /// <summary>
        /// 是否为泛型类型
        /// </summary>
        public bool IsGeneric { get; set; }

        /// <summary>
        /// 是否为可空类型
        /// </summary>
        public bool IsNullable { get; set; }

        /// <summary>
        /// 是否可以生成
        /// </summary>
        public bool CanGenerate { get; set; }

        /// <summary>
        /// 泛型参数列表
        /// </summary>
        public List<TypeInfo> GenericArguments { get; set; }

        /// <summary>
        /// 数组元素类型
        /// </summary>
        public TypeInfo? ElementType { get; set; }

        /// <summary>
        /// 对应的System.Type
        /// </summary>
        public Type? SystemType { get; set; }

        public TypeInfo()
        {
            GenericArguments = new List<TypeInfo>();
        }
    }
}