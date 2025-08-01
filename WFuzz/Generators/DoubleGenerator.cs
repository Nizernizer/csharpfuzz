using System;

namespace WFuzz.Generators
{
    /// <summary>
    /// 双精度浮点数类型生成器
    /// </summary>
    public class DoubleGenerator : AbstractGenerator<double>
    {
        protected override double GenerateTyped(FuzzInput input)
        {
            // 生成合理范围内的双精度浮点数
            long longValue = (long)input.GenerateArgument<int>(0) << 32 | 
                             (uint)input.GenerateArgument<int>(1);
            
            // 转换为双精度浮点数并缩放到合理范围
            double normalized = (double)(longValue % 1000000) / 1000.0;
            
            // 25% 概率生成特殊值
            int special = Math.Abs(input.GenerateArgument<int>(2)) % 4;
            return special switch
            {
                0 when input.GenerateBool() => double.NaN,
                1 when input.GenerateBool() => double.PositiveInfinity,
                2 when input.GenerateBool() => double.NegativeInfinity,
                _ => normalized
            };
        }
    }
}