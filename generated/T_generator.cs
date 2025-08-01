using System;
using System.Collections.Generic;
using System.Linq;
using WFuzz;
using TestLibrary;

namespace WFuzzGen
{
    public class T_generator : WFuzz.ICaller
    {
        public object Call(WFuzz.FuzzInput input)
        {
            try
            {
                // 为泛型参数 T 返回一个 object
                return new object();
            }
            catch (Exception ex)
            {
                return new WFuzz.ExceptionResult(ex);
            }
        }
    }
}
