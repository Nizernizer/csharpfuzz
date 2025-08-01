using System;
using WFuzz;
using TestLibrary;

namespace WFuzzGen
{
    /// <summary>
    /// 测试入口: Vector2D.Equals(object obj)
    /// </summary>
    public class TestLibrary_Vector2D_Equals_object : WFuzz.ICaller
    {
        public object Call(WFuzz.FuzzInput input)
        {
            try
            {
                var instance = input.GenerateArgument<TestLibrary.Vector2D>(0);
                var arg1 = input.GenerateArgument<object>(1);
                return instance.Equals(arg1);
            }
            catch (Exception ex)
            {
                return new WFuzz.ExceptionResult(ex);
            }
        }
    }
}
