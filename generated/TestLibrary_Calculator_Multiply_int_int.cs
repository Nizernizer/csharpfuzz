using System;
using WFuzz;
using TestLibrary;

namespace WFuzzGen
{
    public class TestLibrary_Calculator_Multiply_int_int : WFuzz.ICaller
    {
        public object Call(WFuzz.FuzzInput input)
        {
            try
            {
                var arg0 = input.GenerateArgument<int>(0);
                var arg1 = input.GenerateArgument<int>(1);
                return Calculator.Multiply(arg0, arg1);
            }
            catch (Exception ex)
            {
                return new WFuzz.ExceptionResult(ex);
            }
        }
    }
}
