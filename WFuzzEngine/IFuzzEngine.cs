using System;
using System.Threading;
using System.Threading.Tasks;
using WFuzz;

namespace WFuzzEngine
{
    /// <summary>
    /// 模糊测试引擎接口
    /// </summary>
    public interface IFuzzEngine
    {
        /// <summary>
        /// 引擎名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 运行模糊测试
        /// </summary>
        /// <param name="caller">测试调用器</param>
        /// <param name="config">引擎配置</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>任务</returns>
        Task RunAsync(ICaller caller, EngineConfig config, CancellationToken cancellationToken);
    }
}