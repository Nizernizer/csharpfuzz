using System;
using WFuzz;
using TestLibrary;

namespace WFuzzGen
{
    /// <summary>
    /// 测试入口: EventPublisher.SendMessage(string message)
    /// </summary>
    public class TestLibrary_EventPublisher_SendMessage_string : WFuzz.ICaller
    {
        public object Call(WFuzz.FuzzInput input)
        {
            try
            {
                var instance = input.GenerateArgument<TestLibrary.EventPublisher>(0);
                var arg1 = input.GenerateArgument<string>(1);
                instance.SendMessage(arg1);
                return null;
            }
            catch (Exception ex)
            {
                return new WFuzz.ExceptionResult(ex);
            }
        }
    }
}
