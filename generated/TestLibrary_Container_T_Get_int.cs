using System;
using WFuzz;
using TestLibrary;

namespace WFuzzGen
{
    /// <summary>
    /// 测试入口: Container`1.Get(int index)
    /// </summary>
    public class TestLibrary_Container_T_Get_int : WFuzz.ICaller
    {
        public object Call(WFuzz.FuzzInput input)
        {
            try
            {
                var instance = input.GenerateArgument<TestLibrary.Container<object>>(0);
                var arg1 = input.GenerateArgument<int>(1);
                return instance.Get(arg1);
            }
            catch (Exception ex)
            {
                return new WFuzz.ExceptionResult(ex);
            }
        }
    }
}
