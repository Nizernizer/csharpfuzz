using System;
using System.Threading.Tasks;
using WFuzz;
using TestLibrary;

namespace WFuzzGen
{
    /// <summary>
    /// 测试入口: Calculator.AddAsync(int a, int b)
    /// </summary>
    public class TestLibrary_Calculator_AddAsync_int_int : WFuzz.ICaller
    {
        public object Call(WFuzz.FuzzInput input)
        {
            try
            {
                var instance = input.GenerateArgument<TestLibrary.Calculator>(0);
                var arg1 = input.GenerateArgument<int>(1);
                var arg2 = input.GenerateArgument<int>(2);
                var task = instance.AddAsync(arg1, arg2);
                return task.ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                return new WFuzz.ExceptionResult(ex);
            }
        }
    }
}
