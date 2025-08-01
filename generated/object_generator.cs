using System;
using System.Collections.Generic;
using System.Linq;
using WFuzz;
using System;

namespace WFuzzGen
{
    public class object_generator : WFuzz.ICaller
    {
        public object Call(WFuzz.FuzzInput input)
        {
            try
            {
                // 为 object 类型返回一个简单的字符串
                return input.GenerateArgument<string>(0);
            }
            catch (Exception ex)
            {
                return new WFuzz.ExceptionResult(ex);
            }
        }
    }
}
