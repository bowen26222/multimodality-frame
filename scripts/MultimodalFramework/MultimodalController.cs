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
        
        [Export]
        public bool UseLocalASR { get; set; } = true; // 使用本地语音转文字
        
        [Export]
        public int EurekaServicePort { get; set; } = 8765;
        
        private VoiceRecorder _voiceRecorder;
        private QwenVLClient _apiClient;
        private EurekaServiceManager _eurekaService;
        private OptionRegistry _optionRegistry;
        private bool _isProcessingRequest = false;
        private FrameworkConfig _config;
        private string _pendingAudioBase64;
        
        public override void _Ready()
        {
            // 加载配置文件
            LoadConfig();
            
            // 初始化组件
            _optionRegistry = new OptionRegistry();
            
            // 初始化语音转文字服务
            if (UseLocalASR || (_config?.Asr?.UseLocal ?? true))
            {
                _eurekaService = new EurekaServiceManager
                {
                    ServicePort = _config?.Asr?.ServicePort ?? EurekaServicePort,
                    AutoStart = true
                };
                
                // 应用 ASR 配置
                if (_config?.Asr != null)
                {
                    if (!string.IsNullOrEmpty(_config.Asr.ModelPath))
                    {
                        _eurekaService.ModelPath = _config.Asr.ModelPath;
                    }
                    _eurekaService.StartupTimeoutMs = _config.Asr.StartupTimeoutMs;
                }
                
                AddChild(_eurekaService);
                _eurekaService.ServiceReady += OnEurekaServiceReady;
                _eurekaService.ServiceFailed += OnEurekaServiceFailed;
            }
            
            _voiceRecorder = new VoiceRecorder();
            AddChild(_voiceRecorder);
            _voiceRecorder.RecordingCompleted += OnRecordingCompleted;
            _voiceRecorder.RecordingFailed += OnRecordingFailed;
            
            _apiClient = new QwenVLClient();
            ApplyConfigToClient();
            AddChild(_apiClient);
            _apiClient.SetOptionRegistry(_optionRegistry);
            _apiClient.ResponseReceived += OnResponseReceived;
            _apiClient.RequestFailed += OnRequestFailed;
            
            if (AutoStartRecording || (_config?.Recording.AutoStart ?? false))
            {
                // 等待服务就绪后再开始录制
                if (UseLocalASR && (_eurekaService?.IsServiceReady ?? false))
                {
                    StartListening();
                }
            }
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
        /// 开始语音录制
        /// </summary>
        public void StartListening()
        {
            if (_isProcessingRequest)
            {
                GD.PrintErr("Already processing a request");
                return;
            }
            
            _voiceRecorder.StartRecording();
        }
        
        /// <summary>
        /// 停止语音录制并处理
        /// </summary>
        public void StopListening()
        {
            _voiceRecorder.StopRecording();
        }
        
        /// <summary>
        /// 取消当前录制
        /// </summary>
        public void CancelListening()
        {
            _voiceRecorder.CancelRecording();
        }
        
        /// <summary>
        /// Eureka 服务就绪回调
        /// </summary>
        private void OnEurekaServiceReady()
        {
            GD.Print("Eureka ASR service is ready");
            EmitSignal(SignalName.ServiceStatusChanged, true);
            
            // 如果配置了自动开始录制，现在开始
            if (AutoStartRecording || (_config?.Recording.AutoStart ?? false))
            {
                StartListening();
            }
        }
        
        /// <summary>
        /// Eureka 服务失败回调
        /// </summary>
        private void OnEurekaServiceFailed(string error)
        {
            GD.PrintErr($"Eureka ASR service failed: {error}");
            EmitSignal(SignalName.ServiceStatusChanged, false);
            EmitSignal(SignalName.Error, $"ASR service failed: {error}");
        }
        
        /// <summary>
        /// 录制完成回调
        /// </summary>
        private void OnRecordingCompleted(string audioBase64)
        {
            _isProcessingRequest = true;
            
            if (UseLocalASR && _eurekaService != null && _eurekaService.IsServiceReady)
            {
                // 使用本地 ASR 服务转写
                _pendingAudioBase64 = audioBase64;
                TranscribeAudioAsync(audioBase64);
            }
            else
            {
                // 直接发送音频给大模型（旧方式，可能不支持）
                GD.Print("Local ASR not available, sending audio directly to API");
                _apiClient.SendVoiceForMatching(audioBase64, "wav");
            }
        }
        
        /// <summary>
        /// 异步转写音频
        /// </summary>
        private async void TranscribeAudioAsync(string audioBase64)
        {
            try
            {
                var result = await _eurekaService.TranscribeBase64(audioBase64, "wav");
                
                if (result.Success)
                {
                    string transcribedText = result.Text;
                    GD.Print($"Transcription: {transcribedText}");
                    EmitSignal(SignalName.TranscriptionCompleted, transcribedText);
                    
                    // 发送转写后的文字给大模型进行选项匹配
                    _apiClient.SendTextForMatching(transcribedText);
                }
                else
                {
                    _isProcessingRequest = false;
                    EmitSignal(SignalName.Error, $"Transcription failed: {result.Error}");
                }
            }
            catch (Exception ex)
            {
                _isProcessingRequest = false;
                EmitSignal(SignalName.Error, $"Transcription error: {ex.Message}");
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
        /// ASR 服务是否就绪
        /// </summary>
        public bool IsASRReady => _eurekaService?.IsServiceReady ?? false;
    }
}
