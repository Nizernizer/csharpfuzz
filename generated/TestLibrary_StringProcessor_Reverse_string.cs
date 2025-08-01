using System;
using WFuzz;
using TestLibrary;

namespace WFuzzGen
{
    public class TestLibrary_StringProcessor_Reverse_string : WFuzz.ICaller
    {
        public object Call(WFuzz.FuzzInput input)
        {
            try
            {
                var arg0 = input.GenerateArgument<string>(0);
                return StringProcessor.Reverse(arg0);
            }
            catch (Exception ex)
            {
                return new WFuzz.ExceptionResult(ex);
            }
        }
    }
}
