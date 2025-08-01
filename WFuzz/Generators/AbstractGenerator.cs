using System;

namespace WFuzz.Generators
{
    /// <summary>
    /// 抽象生成器基类
    /// </summary>
    public abstract class AbstractGenerator<T> : IGenerator
    {
        public object? Generate<TTarget>(FuzzInput input)
        {
            if (typeof(TTarget) != typeof(T))
            {
                throw new ArgumentException($"Generator for {typeof(T)} cannot generate {typeof(TTarget)}");
            }
            
            return GenerateTyped(input);
        }

        /// <summary>
        /// 生成指定类型的实例
        /// </summary>
        /// <param name="input">模糊测试输入</param>
        /// <returns>生成的类型实例</returns>
        protected abstract T GenerateTyped(FuzzInput input);
    }
}