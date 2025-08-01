using System;
using WFuzz;
using TestLibrary;

namespace WFuzzGen
{
    public class TestLibrary_StringProcessor_set_Item_int : WFuzz.ICaller
    {
        public object Call(WFuzz.FuzzInput input)
        {
            try
            {
                var instance = input.GenerateArgument<TestLibrary.StringProcessor>(0);
                var index0 = input.GenerateArgument<int>(1);
                var value = input.GenerateArgument<string>(2);
                instance[index0] = value;
                return null;
            }
            catch (Exception ex)
            {
                return new WFuzz.ExceptionResult(ex);
            }
        }
    }
}
