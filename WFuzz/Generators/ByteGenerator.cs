using System;

namespace WFuzz.Generators
{
    /// <summary>
    /// 字节类型生成器
    /// </summary>
    public class ByteGenerator : AbstractGenerator<byte>
    {
        protected override byte GenerateTyped(FuzzInput input)
        {
            return (byte)(Math.Abs(input.GenerateArgument<int>(0)) % 256);
        }
    }
}