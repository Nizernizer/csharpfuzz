using System.Collections.Generic;

namespace WFuzzGen.Models
{
    /// <summary>
    /// 生成器信息
    /// </summary>
    public class GeneratorInfo
    {
        /// <summary>
        /// 类型标识符
        /// </summary>
        public string TypeId { get; set; } = string.Empty;

        /// <summary>
        /// 显示名称
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 可用的生成器列表
        /// </summary>
        public List<GeneratorDefinition> Generators { get; set; }

        public GeneratorInfo()
        {
            Generators = new List<GeneratorDefinition>();
        }
    }

    /// <summary>
    /// 生成器定义
    /// </summary>
    public class GeneratorDefinition
    {
        /// <summary>
        /// 生成器显示名称
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 生成器的混淆名称（完整类名）
        /// </summary>
        public string MangledName { get; set; } = string.Empty;

        /// <summary>
        /// 是否为默认生成器
        /// </summary>
        public bool IsDefault { get; set; }

        /// <summary>
        /// 生成器参数列表
        /// </summary>
        public List<GeneratorArgument> Arguments { get; set; }

        public GeneratorDefinition()
        {
            Arguments = new List<GeneratorArgument>();
        }
    }

    /// <summary>
    /// 生成器参数
    /// </summary>
    public class GeneratorArgument
    {
        /// <summary>
        /// 参数类型标识符
        /// </summary>
        public string TypeId { get; set; } = string.Empty;
    }
}