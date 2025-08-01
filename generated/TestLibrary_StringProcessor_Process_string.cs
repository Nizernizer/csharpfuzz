using System;
using WFuzz;
using TestLibrary;

namespace WFuzzGen
{
    /// <summary>
    /// 测试入口: StringProcessor.Process(string input)
    /// </summary>
    public class TestLibrary_StringProcessor_Process_string : WFuzz.ICaller
    {
        public object Call(WFuzz.FuzzInput input)
        {
            try
            {
                var instance = input.GenerateArgument<TestLibrary.StringProcessor>(0);
                var arg1 = input.GenerateArgument<string>(1);
                return instance.Process(arg1);
            }
            catch (Exception ex)
            {
                return new WFuzz.ExceptionResult(ex);
            }
        }
    }
}
