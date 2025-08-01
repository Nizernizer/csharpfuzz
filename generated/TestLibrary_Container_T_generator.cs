using System;
using System.Collections.Generic;
using System.Linq;
using WFuzz;
using TestLibrary;

namespace WFuzzGen
{
    public class TestLibrary_Container_T_generator : WFuzz.ICaller
    {
        public object Call(WFuzz.FuzzInput input)
        {
            try
            {
                return new TestLibrary.Container<object>();
            }
            catch (Exception ex)
            {
                return new WFuzz.ExceptionResult(ex);
            }
        }
    }
}
