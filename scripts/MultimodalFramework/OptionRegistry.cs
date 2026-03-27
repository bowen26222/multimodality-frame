using System;
using System.Collections.Generic;
using System.Text.Json;

namespace MultimodalFramework
{
    /// <summary>
    /// 选项注册中心，管理所有可匹配的选项
    /// </summary>
    public class OptionRegistry
    {
        private readonly Dictionary<string, IOption> _options = new();
        
        /// <summary>
        /// 注册一个选项
        /// </summary>
        public void Register(IOption option)
        {
            if (string.IsNullOrEmpty(option.Id))
            {
                throw new ArgumentException("Option ID cannot be null or empty");
            }
            _options[option.Id] = option;
        }
        
        /// <summary>
        /// 注销一个选项
        /// </summary>
        public void Unregister(string optionId)
        {
            _options.Remove(optionId);
        }
        
        /// <summary>
        /// 获取所有已注册的选项
        /// </summary>
        public IReadOnlyDictionary<string, IOption> GetAllOptions()
        {
            return _options;
        }
        
        /// <summary>
        /// 获取选项
        /// </summary>
        public IOption GetOption(string optionId)
        {
            return _options.TryGetValue(optionId, out var option) ? option : null;
        }
        
        /// <summary>
        /// 生成用于AI提示词的选项描述文本
        /// </summary>
        public string GenerateOptionsDescription()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("可用选项列表：");
            sb.AppendLine();
            
            foreach (var kvp in _options)
            {
                var option = kvp.Value;
                sb.AppendLine($"- ID: {option.Id}");
                sb.AppendLine($"  描述: {option.Description}");
                sb.AppendLine();
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// 清空所有注册的选项
        /// </summary>
        public void Clear()
        {
            _options.Clear();
        }
    }
}
