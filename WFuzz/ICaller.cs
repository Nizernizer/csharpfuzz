namespace WFuzz
{
    /// <summary>
    /// 定义测试调用者的基础接口
    /// </summary>
    public interface ICaller
    {
        /// <summary>
        /// 执行测试调用
        /// </summary>
        /// <param name="input">模糊测试输入</param>
        /// <returns>调用结果或异常结果</returns>
        object Call(FuzzInput input);
    }
}