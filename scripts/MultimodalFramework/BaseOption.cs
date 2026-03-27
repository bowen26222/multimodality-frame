using System;
using System.Text.Json;

namespace MultimodalFramework
{
    /// <summary>
    /// 基础选项实现类，提供简单的选项定义方式
    /// </summary>
    public class BaseOption : IOption
    {
        private readonly string _id;
        private readonly string _description;
        private readonly Action<JsonElement> _executeAction;
        
        /// <summary>
        /// 创建一个基础选项
        /// </summary>
        /// <param name="id">选项ID</param>
        /// <param name="description">选项描述</param>
        /// <param name="executeAction">执行函数</param>
        public BaseOption(string id, string description, Action<JsonElement> executeAction)
        {
            _id = id;
            _description = description;
            _executeAction = executeAction;
        }
        
        public string Id => _id;
        public string Description => _description;
        
        public void Execute(JsonElement parameters)
        {
            _executeAction?.Invoke(parameters);
        }
    }
    
    /// <summary>
    /// 选项构建器，提供流畅的API来创建选项
    /// </summary>
    public class OptionBuilder
    {
        private string _id;
        private string _description;
        private Action<JsonElement> _executeAction;
        
        /// <summary>
        /// 设置选项ID
        /// </summary>
        public OptionBuilder WithId(string id)
        {
            _id = id;
            return this;
        }
        
        /// <summary>
        /// 设置选项描述
        /// </summary>
        public OptionBuilder WithDescription(string description)
        {
            _description = description;
            return this;
        }
        
        /// <summary>
        /// 设置执行函数
        /// </summary>
        public OptionBuilder WithAction(Action<JsonElement> action)
        {
            _executeAction = action;
            return this;
        }
        
        /// <summary>
        /// 设置无参数执行函数
        /// </summary>
        public OptionBuilder WithAction(Action action)
        {
            _executeAction = _ => action?.Invoke();
            return this;
        }
        
        /// <summary>
        /// 构建选项
        /// </summary>
        public BaseOption Build()
        {
            if (string.IsNullOrEmpty(_id))
            {
                throw new InvalidOperationException("Option ID is required");
            }
            
            return new BaseOption(_id, _description ?? "", _executeAction);
        }
    }
}
