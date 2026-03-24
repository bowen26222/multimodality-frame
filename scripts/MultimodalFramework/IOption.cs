using System;

namespace MultimodalFramework
{
    /// <summary>
    /// 可匹配选项的接口定义
    /// </summary>
    public interface IOption
    {
        /// <summary>
        /// 选项的唯一标识符
        /// </summary>
        string Id { get; }
        
        /// <summary>
        /// 选项的描述文本，用于AI匹配
        /// </summary>
        string Description { get; }
        
        /// <summary>
        /// 选项的关键词列表，辅助匹配
        /// </summary>
        string[] Keywords { get; }
        
        /// <summary>
        /// 当选项被匹配时执行的函数
        /// </summary>
        /// <param name="parameters">从AI响应中提取的参数</param>
        void Execute(System.Text.Json.JsonElement parameters);
    }
}
