using System;
using System.Collections.Generic;
using System.Linq;
using WFuzz;
using TestLibrary;

namespace WFuzzGen
{
    public class TestLibrary_ResourceManager_generator : WFuzz.ICaller
    {
        public object Call(WFuzz.FuzzInput input)
        {
            try
            {
                var type = typeof(TestLibrary.ResourceManager);
                
                var ctors = type.GetConstructors();
                var defaultCtor = ctors.FirstOrDefault(c => c.GetParameters().Length == 0);
                
                if (defaultCtor != null)
                {
                    return defaultCtor.Invoke(null);
                }
                
                var ctor = ctors.OrderBy(c => c.GetParameters().Length).FirstOrDefault();
                if (ctor != null)
                {
                    var parameters = ctor.GetParameters();
                    var args = new object[parameters.Length];
                    
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        var paramType = parameters[i].ParameterType;
                        
                        if (paramType == typeof(string))
                            args[i] = input.GenerateArgument<string>(i);
                        else if (paramType == typeof(int))
                            args[i] = input.GenerateArgument<int>(i);
                        else if (paramType == typeof(double))
                            args[i] = input.GenerateArgument<double>(i);
                        else if (paramType == typeof(bool))
                            args[i] = input.GenerateArgument<bool>(i);
                        else if (paramType.IsValueType)
                            args[i] = Activator.CreateInstance(paramType);
                        else
                            args[i] = null;
                    }
                    
                    return ctor.Invoke(args);
                }
                
                throw new InvalidOperationException("无法创建类型 TestLibrary.ResourceManager 的实例");
            }
            catch (Exception ex)
            {
                return new WFuzz.ExceptionResult(ex);
            }
        }
    }
}
