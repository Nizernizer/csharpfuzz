using System;

namespace WFuzz.Generators
{
    /// <summary>
    /// Guid 类型生成器
    /// </summary>
    public class GuidGenerator : AbstractGenerator<Guid>
    {
        protected override Guid GenerateTyped(FuzzInput input)
        {
            // 生成16个字节来构造Guid
            byte[] guidBytes = new byte[16];
            
            for (int i = 0; i < 16; i++)
            {
                guidBytes[i] = (byte)(Math.Abs(input.GenerateArgument<int>(i)) % 256);
            }
            
            return new Guid(guidBytes);
        }
    }
}