using Godot;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MultimodalFramework
{
    /// <summary>
    /// 框架配置类
    /// </summary>
    public class FrameworkConfig
    {
        [JsonPropertyName("api")]
        public ApiConfig Api { get; set; } = new ApiConfig();
        
        [JsonPropertyName("recording")]
        public RecordingConfig Recording { get; set; } = new RecordingConfig();
        
        [JsonPropertyName("asr")]
        public AsrConfig Asr { get; set; } = new AsrConfig();
        
        /// <summary>
        /// 从文件加载配置
        /// </summary>
        public static FrameworkConfig Load(string path = "config.json")
        {
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.PrintErr($"Failed to open config file: {path}");
                GD.Print("Using default configuration. Please create config.json from config.example.json");
                return new FrameworkConfig();
            }
            
            try
            {
                var content = file.GetAsText();
                return JsonSerializer.Deserialize<FrameworkConfig>(content) ?? new FrameworkConfig();
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Failed to parse config file: {ex.Message}");
                return new FrameworkConfig();
            }
        }
    }
    
    /// <summary>
    /// API配置
    /// </summary>
    public class ApiConfig
    {
        [JsonPropertyName("endpoint")]
        public string Endpoint { get; set; } = "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions";
        
        [JsonPropertyName("key")]
        public string Key { get; set; } = "";
        
        [JsonPropertyName("model")]
        public string Model { get; set; } = "qwen-vl-max";
        
        [JsonPropertyName("temperature")]
        public float Temperature { get; set; } = 0.1f;
        
        [JsonPropertyName("maxTokens")]
        public int MaxTokens { get; set; } = 1024;
    }
    
    /// <summary>
    /// 录音配置
    /// </summary>
    public class RecordingConfig
    {
        [JsonPropertyName("autoStart")]
        public bool AutoStart { get; set; } = false;
    }
    
    /// <summary>
    /// ASR (语音转文字) 配置
    /// </summary>
    public class AsrConfig
    {
        [JsonPropertyName("useOnline")]
        public bool UseOnline { get; set; } = true;
        
        [JsonPropertyName("endpoint")]
        public string Endpoint { get; set; } = "https://api.openai.com/v1/audio/transcriptions";
        
        [JsonPropertyName("key")]
        public string Key { get; set; } = "";
        
        [JsonPropertyName("model")]
        public string Model { get; set; } = "whisper-1";
    }
}
