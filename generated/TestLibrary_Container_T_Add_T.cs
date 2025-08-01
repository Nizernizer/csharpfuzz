using System;
using WFuzz;
using TestLibrary;

namespace WFuzzGen
{
    /// <summary>
    /// 测试入口: Container`1.Add(T item)
    /// </summary>
    public class TestLibrary_Container_T_Add_T : WFuzz.ICaller
    {
        public object Call(WFuzz.FuzzInput input)
        {
            try
            {
                var instance = input.GenerateArgument<TestLibrary.Container<object>>(0);
                var arg1 = input.GenerateArgument<object>(1);
                instance.Add(arg1);
                return null;
            }
            catch (Exception ex)
            {
                return new WFuzz.ExceptionResult(ex);
            }
        }
    }
}
