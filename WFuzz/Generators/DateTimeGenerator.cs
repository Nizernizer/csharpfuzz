using System;

namespace WFuzz.Generators
{
    /// <summary>
    /// DateTime 类型生成器
    /// </summary>
    public class DateTimeGenerator : AbstractGenerator<DateTime>
    {
        private static readonly DateTime MinDate = new DateTime(1900, 1, 1);
        private static readonly DateTime MaxDate = new DateTime(2100, 12, 31);

        protected override DateTime GenerateTyped(FuzzInput input)
        {
            // 生成在合理范围内的随机日期
            long minTicks = MinDate.Ticks;
            long maxTicks = MaxDate.Ticks;
            long range = maxTicks - minTicks;
            
            // 使用输入数据生成随机偏移
            long randomOffset = Math.Abs((long)input.GenerateArgument<int>(0)) % range;
            
            return new DateTime(minTicks + randomOffset);
        }
    }
}