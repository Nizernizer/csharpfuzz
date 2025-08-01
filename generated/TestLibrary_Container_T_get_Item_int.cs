using System;
using WFuzz;
using TestLibrary;

namespace WFuzzGen
{
    public class TestLibrary_Container_T_get_Item_int : WFuzz.ICaller
    {
        public object Call(WFuzz.FuzzInput input)
        {
            try
            {
                var instance = input.GenerateArgument<TestLibrary.Container<object>>(0);
                var index0 = input.GenerateArgument<int>(1);
                return instance[index0];
            }
            catch (Exception ex)
            {
                return new WFuzz.ExceptionResult(ex);
            }
        }
    }
}
