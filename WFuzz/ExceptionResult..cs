using System;

namespace WFuzz
{
    /// <summary>
    /// 包装异常结果的类
    /// </summary>
    public class ExceptionResult
    {
        public Exception Exception { get; }
        public ExceptionCategory Category { get; }
        public bool ShouldContinue { get; }
        
        public ExceptionResult(Exception ex)
        {
            Exception = ex ?? throw new ArgumentNullException(nameof(ex));
            Category = ClassifyException(ex);
            ShouldContinue = Category != ExceptionCategory.Fatal;
        }
        
        private ExceptionCategory ClassifyException(Exception ex)
        {
            return ex switch
            {
                ArgumentNullException => ExceptionCategory.Expected,
                ArgumentException => ExceptionCategory.Expected,
                InvalidOperationException => ExceptionCategory.Expected,
                NotImplementedException => ExceptionCategory.Expected,
                OutOfMemoryException => ExceptionCategory.Fatal,
                StackOverflowException => ExceptionCategory.Fatal,
                _ => ExceptionCategory.Unexpected
            };
        }
    }
    
    /// <summary>
    /// 异常分类枚举
    /// </summary>
    public enum ExceptionCategory
    {
        Expected,    // 预期的业务异常
        Unexpected,  // 意外异常，需要记录
        Fatal        // 致命异常，停止测试
    }
}