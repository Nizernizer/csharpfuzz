using System;
using WFuzz;
using TestLibrary;

namespace WFuzzGen
{
    /// <summary>
    /// 测试入口: Calculator.Divide(double a, double b)
    /// </summary>
    public class TestLibrary_Calculator_Divide_double_double : WFuzz.ICaller
    {
        public object Call(WFuzz.FuzzInput input)
        {
            try
            {
                var instance = input.GenerateArgument<TestLibrary.Calculator>(0);
                var arg1 = input.GenerateArgument<double>(1);
                var arg2 = input.GenerateArgument<double>(2);
                return instance.Divide(arg1, arg2);
            }
            catch (Exception ex)
            {
                return new WFuzz.ExceptionResult(ex);
            }
        }
    }
}
