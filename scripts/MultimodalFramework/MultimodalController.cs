using Godot;
using System;
using System.Text.Json;

namespace MultimodalFramework
{
    /// <summary>
    /// 多模态交互主控制器，整合语音录制、语音转文字、API调用和选项执行
    /// </summary>
    public partial class MultimodalController : Node
    {
        [Signal]
        public delegate void OptionMatchedEventHandler(string optionId, float confidence);
        
        [Signal]
        public delegate void OptionExecutedEventHandler(string optionId, bool success);
        
        [Signal]
        public delegate void NoMatchEventHandler(string reason, string userIntent);
        
        [Signal]
        public delegate void ErrorEventHandler(string message);
        
        [Signal]
        public delegate void TranscriptionCompletedEventHandler(string text);
        
        [Signal]
        public delegate void ServiceStatusChangedEventHandler(bool isReady);
        
        [Export]
        public string ApiKey { get; set; } = "";
        
        [Export]
        public bool AutoStartRecording { get; set; } = false;
        
        [Export]
        public string ConfigPath { get; set; } = "config.json";
        
        private VoiceRecorder _voiceRecorder;
        private LLMClient _apiClient;
        private OnlineASRClient _asrClient;
        private OptionRegistry _optionRegistry;
        private bool _isProcessingRequest = false;
        private FrameworkConfig _config;
        
        public override void _Ready()
        {
            // 加载配置文件
            LoadConfig();
            
            // 初始化组件
            _optionRegistry = new OptionRegistry();
            
            // 初始化在线语音转文字服务
            _asrClient = new OnlineASRClient();
            if (_config?.Asr != null)
            {
                GD.Print($"ASR Config: endpoint={_config.Asr.Endpoint}, model={_config.Asr.Model}, key={(_config.Asr.Key?.Length > 0 ? "set" : "null")}");
                _asrClient.Endpoint = _config.Asr.Endpoint;
                _asrClient.ApiKey = _config.Asr.Key;
                _asrClient.Model = _config.Asr.Model;
            }
            else
            {
                GD.PrintErr("ASR config is null, using defaults");
            }
            AddChild(_asrClient);
            _asrClient.TranscriptionCompleted += OnTranscriptionCompleted;
            
            _voiceRecorder = new VoiceRecorder();
            AddChild(_voiceRecorder);
            _voiceRecorder.RecordingCompleted += OnRecordingCompleted;
            _voiceRecorder.RecordingFailed += OnRecordingFailed;
            
            _apiClient = new LLMClient();
            ApplyConfigToClient();
            AddChild(_apiClient);
            _apiClient.SetOptionRegistry(_optionRegistry);
            _apiClient.ResponseReceived += OnResponseReceived;
            _apiClient.RequestFailed += OnRequestFailed;
            
            if (AutoStartRecording || (_config?.Recording.AutoStart ?? false))
            {
                StartListening();
            }
            
            EmitSignal(SignalName.ServiceStatusChanged, true);
        }
        
        /// <summary>
        /// 加载配置文件
        /// </summary>
        private void LoadConfig()
        {
            _config = FrameworkConfig.Load(ConfigPath);
        }
        
        /// <summary>
        /// 应用配置到API客户端
        /// </summary>
        private void ApplyConfigToClient()
        {
            // 优先使用代码设置的 ApiKey，其次使用配置文件
            if (!string.IsNullOrEmpty(ApiKey))
            {
                _apiClient.ApiKey = ApiKey;
            }
            else if (_config != null && !string.IsNullOrEmpty(_config.Api.Key))
            {
                _apiClient.ApiKey = _config.Api.Key;
            }
            
            // 应用其他配置
            if (_config != null)
            {
                _apiClient.ApiEndpoint = _config.Api.Endpoint;
                _apiClient.ModelName = _config.Api.Model;
                _apiClient.Temperature = _config.Api.Temperature;
                _apiClient.MaxTokens = _config.Api.MaxTokens;
            }
        }
        
        /// <summary>
        /// 获取选项注册中心，用于注册选项
        /// </summary>
        public OptionRegistry GetOptionRegistry()
        {
            return _optionRegistry;
        }
        
        /// <summary>
        /// 开始监听（开始录制）
        /// </summary>
        public void StartListening()
        {
            if (_isProcessingRequest)
            {
                GD.Print("Cannot start recording while processing request");
                return;
            }
            
            _voiceRecorder.StartRecording();
            GD.Print("Started listening...");
        }
        
        /// <summary>
        /// 停止监听（停止录制并处理）
        /// </summary>
        public void StopListening()
        {
            _voiceRecorder.StopRecording();
            GD.Print("Stopped listening");
        }
        
        /// <summary>
        /// 切换监听状态
        /// </summary>
        public void ToggleListening()
        {
            if (_voiceRecorder.IsRecording)
            {
                StopListening();
            }
            else
            {
                StartListening();
            }
        }
        
        /// <summary>
        /// 录制完成回调
        /// </summary>
        private void OnRecordingCompleted(string audioBase64)
        {
            _isProcessingRequest = true;
            GD.Print("Recording completed, sending to ASR service...");
            
            // 使用在线 ASR 服务转写 (VoiceRecorder 保存为 WAV 格式)
            _asrClient.TranscribeBase64(audioBase64, "wav");
        }
        
        /// <summary>
        /// ASR 转写完成回调
        /// </summary>
        private void OnTranscriptionCompleted(string text, bool success, string error)
        {
            if (success && !string.IsNullOrEmpty(text))
            {
                GD.Print($"Transcription: {text}");
                EmitSignal(SignalName.TranscriptionCompleted, text);
                
                // 发送转写后的文字给大模型进行选项匹配
                _apiClient.SendTextForMatching(text);
            }
            else
            {
                _isProcessingRequest = false;
                string errorMsg = string.IsNullOrEmpty(error) ? "Unknown ASR error" : error;
                GD.PrintErr($"ASR failed: {errorMsg}");
                EmitSignal(SignalName.Error, $"Transcription failed: {errorMsg}");
            }
        }
        
        /// <summary>
        /// 录制失败回调
        /// </summary>
        private void OnRecordingFailed(string error)
        {
            _isProcessingRequest = false;
            EmitSignal(SignalName.Error, $"Recording failed: {error}");
        }
        
        /// <summary>
        /// API响应回调
        /// </summary>
        private void OnResponseReceived(string optionId, float confidence, string parametersJson, bool matched, string reason, string userIntent)
        {
            _isProcessingRequest = false;
            
            if (matched)
            {
                EmitSignal(SignalName.OptionMatched, optionId, confidence);
                ExecuteOption(optionId, parametersJson);
            }
            else
            {
                EmitSignal(SignalName.NoMatch, reason ?? "未找到匹配选项", userIntent ?? "");
                GD.Print($"No match: {reason}");
                GD.Print($"User intent: {userIntent}");
            }
        }
        
        /// <summary>
        /// API请求失败回调
        /// </summary>
        private void OnRequestFailed(string error)
        {
            _isProcessingRequest = false;
            EmitSignal(SignalName.Error, $"API request failed: {error}");
        }
        
        /// <summary>
        /// 执行匹配到的选项
        /// </summary>
        private void ExecuteOption(string optionId, string parametersJson)
        {
            var option = _optionRegistry.GetOption(optionId);
            if (option == null)
            {
                EmitSignal(SignalName.OptionExecuted, optionId, false);
                EmitSignal(SignalName.Error, $"Option not found: {optionId}");
                return;
            }
            
            try
            {
                var parameters = JsonDocument.Parse(parametersJson).RootElement;
                option.Execute(parameters);
                EmitSignal(SignalName.OptionExecuted, optionId, true);
                GD.Print($"Option executed: {optionId}");
            }
            catch (Exception ex)
            {
                EmitSignal(SignalName.OptionExecuted, optionId, false);
                EmitSignal(SignalName.Error, $"Failed to execute option {optionId}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 设置API密钥
        /// </summary>
        public void SetApiKey(string apiKey)
        {
            ApiKey = apiKey;
            if (_apiClient != null)
            {
                _apiClient.ApiKey = apiKey;
            }
        }
        
        /// <summary>
        /// 是否正在处理请求
        /// </summary>
        public bool IsProcessingRequest => _isProcessingRequest;
        
        /// <summary>
        /// 是否正在录制
        /// </summary>
        public bool IsRecording => _voiceRecorder?.IsRecording ?? false;
        
        /// <summary>
        /// ASR 服务是否就绪（在线服务始终就绪）
        /// </summary>
        public bool IsASRReady => true;
    }
}
