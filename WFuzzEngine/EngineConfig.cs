using System;
using System.Collections.Generic;
using System.Text;

namespace WFuzzEngine
{
    /// <summary>
    /// 引擎配置
    /// </summary>
    public class EngineConfig
    {
        public string EngineName { get; set; } = "AFLSharp"; // 默认引擎

        // AFL 相关配置
        public string? InputDirectory { get; set; }
        public string? OutputDirectory { get; set; }
        public int BitmapSize { get; set; } = 65536; // 默认 64KB 位图
        public int TimeoutMs { get; set; } = 1000;
        public List<string> DictionaryPaths { get; set; } = new List<string>();
        public int MemoryLimitMb { get; set; } = 200;

        // 其他通用配置
        public int MaxIterations { get; set; } = -1; // -1 表示无限制

        public override string ToString()
        {
            return $"Engine: {EngineName}, InputDir: {InputDirectory}, OutputDir: {OutputDirectory}, BitmapSize: {BitmapSize}";
        }
    }
}