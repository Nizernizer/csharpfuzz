namespace WFuzz.Generators
{
    /// <summary>
    /// 布尔类型生成器
    /// </summary>
    public class BoolGenerator : AbstractGenerator<bool>
    {
        protected override bool GenerateTyped(FuzzInput input)
        {
            return input.GenerateBool();
        }
    }
}