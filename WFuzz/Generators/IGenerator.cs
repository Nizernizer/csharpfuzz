namespace WFuzz
{
    /// <summary>
    /// 定义类型生成器的基础接口
    /// </summary>
    public interface IGenerator
    {
        /// <summary>
        /// 生成指定类型的实例
        /// </summary>
        /// <typeparam name="T">要生成的类型</typeparam>
        /// <param name="input">模糊测试输入</param>
        /// <returns>生成的类型实例</returns>
        object? Generate<T>(FuzzInput input);
    }
}