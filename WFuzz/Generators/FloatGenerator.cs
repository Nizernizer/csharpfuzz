using System;

namespace WFuzz.Generators
{
    /// <summary>
    /// 浮点数类型生成器
    /// </summary>
    public class FloatGenerator : AbstractGenerator<float>
    {
        protected override float GenerateTyped(FuzzInput input)
        {
            // 生成合理范围内的浮点数
            int intValue = input.GenerateArgument<int>(0);
            
            // 转换为浮点数并缩放到合理范围
            float normalized = (float)(intValue % 10000) / 100.0f;
            
            // 25% 概率生成特殊值
            int special = Math.Abs(input.GenerateArgument<int>(1)) % 4;
            return special switch
            {
                0 when input.GenerateBool() => float.NaN,
                1 when input.GenerateBool() => float.PositiveInfinity,
                2 when input.GenerateBool() => float.NegativeInfinity,
                _ => normalized
            };
        }
    }
}