using System;
using System.Text;

namespace WFuzz.Generators
{
    /// <summary>
    /// 字符串类型生成器
    /// </summary>
    public class StringGenerator : AbstractGenerator<string>
    {
        private static readonly char[] ValidChars = 
        {
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm',
            'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
            'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M',
            'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
            ' ', '.', ',', '!', '?', '-', '_', '@', '#'
        };

        protected override string GenerateTyped(FuzzInput input)
        {
            // 生成随机长度（0-100）
            int length = Math.Abs(input.GenerateArgument<int>(0)) % 100;
            
            if (length == 0)
                return string.Empty;
            
            StringBuilder sb = new StringBuilder(length);
            
            for (int i = 0; i < length; i++)
            {
                int charIndex = Math.Abs(input.GenerateArgument<int>(i + 1)) % ValidChars.Length;
                sb.Append(ValidChars[charIndex]);
            }
            
            return sb.ToString();
        }
    }
}