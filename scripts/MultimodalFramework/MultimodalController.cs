using Godot;
using System;
using System.Text.Json;

namespace MultimodalFramework
{
    /// <summary>
    /// 多模态交互主控制器，整合语音录制、API调用和选项执行
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
        
        [Export]
        public string ApiKey { get; set; } = "";
        
        [Export]
        public bool AutoStartRecording { get; set; } = false;
        
        private VoiceRecorder _voiceRecorder;
        private QwenVLClient _apiClient;
        private OptionRegistry _optionRegistry;
        private bool _isProcessingRequest = false;
        
        public override void _Ready()
        {
            // 初始化组件
            _optionRegistry = new OptionRegistry();
            
            _voiceRecorder = new VoiceRecorder();
            AddChild(_voiceRecorder);
            _voiceRecorder.RecordingCompleted += OnRecordingCompleted;
            _voiceRecorder.RecordingFailed += OnRecordingFailed;
            
            _apiClient = new QwenVLClient();
            _apiClient.ApiKey = ApiKey;
            AddChild(_apiClient);
            _apiClient.SetOptionRegistry(_optionRegistry);
            _apiClient.ResponseReceived += OnResponseReceived;
            _apiClient.RequestFailed += OnRequestFailed;
            
            if (AutoStartRecording)
            {
                StartListening();
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
        /// 录制完成回调
        /// </summary>
        private void OnRecordingCompleted(string audioBase64)
        {
            _isProcessingRequest = true;
            _apiClient.SendVoiceForMatching(audioBase64, "wav");
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
    }
}
