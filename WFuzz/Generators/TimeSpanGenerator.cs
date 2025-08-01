using System;

namespace WFuzz.Generators
{
    /// <summary>
    /// TimeSpan 类型生成器
    /// </summary>
    public class TimeSpanGenerator : AbstractGenerator<TimeSpan>
    {
        protected override TimeSpan GenerateTyped(FuzzInput input)
        {
            // 生成合理范围内的TimeSpan（-365天到365天）
            long maxTicks = TimeSpan.FromDays(365).Ticks;
            long minTicks = -maxTicks;
            long range = maxTicks - minTicks;
            
            // 使用输入数据生成随机偏移
            long randomOffset = Math.Abs((long)input.GenerateArgument<int>(0)) % range;
            long ticks = minTicks + randomOffset;
            
            return new TimeSpan(ticks);
        }
    }
}