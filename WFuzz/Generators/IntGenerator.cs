using System;

namespace WFuzz.Generators
{
    /// <summary>
    /// 整数类型生成器
    /// </summary>
    public class IntGenerator : AbstractGenerator<int>
    {
        protected override int GenerateTyped(FuzzInput input)
        {
            // 从输入数据生成4个字节来构造int
            byte[] bytes = new byte[4];
            
            // 简单的字节获取策略
            for (int i = 0; i < 4; i++)
            {
                bytes[i] = input.GenerateBool() ? (byte)1 : (byte)0; // 临时实现
            }
            
            return BitConverter.ToInt32(bytes, 0);
        }
    }
}