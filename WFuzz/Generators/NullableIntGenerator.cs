using System;

namespace WFuzz.Generators
{
    /// <summary>
    /// 可空整数类型生成器
    /// </summary>
    public class NullableIntGenerator : AbstractGenerator<int?>
    {
        protected override int? GenerateTyped(FuzzInput input)
        {
            // 30% 概率返回 null
            if (input.GenerateBool() && input.GenerateBool())
            {
                return null;
            }
            
            // 否则生成正常的int值
            var intGenerator = new IntGenerator();
            var result = intGenerator.Generate<int>(input);
            return result as int?;
        }
    }
}