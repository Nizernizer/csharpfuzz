using System;
using WFuzz;
using TestLibrary;

namespace WFuzzGen
{
    /// <summary>
    /// 测试入口: ResourceManager.AddResource(string resource)
    /// </summary>
    public class TestLibrary_ResourceManager_AddResource_string : WFuzz.ICaller
    {
        public object Call(WFuzz.FuzzInput input)
        {
            try
            {
                using var instance = input.GenerateArgument<TestLibrary.ResourceManager>(0);
                var arg1 = input.GenerateArgument<string>(1);
                instance.AddResource(arg1);
                return null;
            }
            catch (Exception ex)
            {
                return new WFuzz.ExceptionResult(ex);
            }
        }
    }
}
