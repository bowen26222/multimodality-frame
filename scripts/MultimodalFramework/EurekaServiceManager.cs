using Godot;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MultimodalFramework
{
    /// <summary>
    /// Eureka-Audio 服务管理器
    /// 负责在游戏启动时自动部署本地语音转文字服务
    /// </summary>
    public partial class EurekaServiceManager : Node
    {
        [Signal]
        public delegate void ServiceReadyEventHandler();
        
        [Signal]
        public delegate void ServiceFailedEventHandler(string error);
        
        [Export]
        public int ServicePort { get; set; } = 8765;
        
        [Export]
        public string ServiceHost { get; set; } = "127.0.0.1";
        
        [Export]
        public bool AutoStart { get; set; } = true;
        
        [Export]
        public int StartupTimeoutMs { get; set; } = 60000; // 60秒超时
        
        [Export]
        public string ModelPath { get; set; } = ""; // 留空则使用默认路径或自动下载
        
        private Process _serviceProcess;
        private HttpClient _httpClient;
        private bool _isServiceReady = false;
        private string _serviceUrl;
        
        public override void _Ready()
        {
            _serviceUrl = $"http://{ServiceHost}:{ServicePort}";
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            
            if (AutoStart)
            {
                StartService();
            }
        }
        
        /// <summary>
        /// 启动 Eureka-Audio 服务
        /// </summary>
        public async void StartService()
        {
            if (_isServiceReady)
            {
                EmitSignal(SignalName.ServiceReady);
                return;
            }
            
            // 先检查服务是否已经在运行
            if (await CheckServiceHealth())
            {
                _isServiceReady = true;
                GD.Print("Eureka-Audio service already running");
                EmitSignal(SignalName.ServiceReady);
                return;
            }
            
            GD.Print("Starting Eureka-Audio service...");
            
            try
            {
                // 获取服务脚本路径
                string basePath = ProjectSettings.GlobalizePath("res://");
                string scriptPath = OS.HasFeature("windows")
                    ? Path.Combine(basePath, "eureka_service", "start_service.bat")
                    : Path.Combine(basePath, "eureka_service", "start_service.sh");
                
                if (!File.Exists(scriptPath))
                {
                    EmitSignal(SignalName.ServiceFailed, $"Service script not found: {scriptPath}");
                    return;
                }
                
                // 构建启动参数
                var startInfo = new ProcessStartInfo
                {
                    FileName = scriptPath,
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WindowStyle = ProcessWindowStyle.Normal
                };
                
                // 如果指定了模型路径，添加环境变量
                if (!string.IsNullOrEmpty(ModelPath))
                {
                    startInfo.Environment["MODEL_PATH"] = ModelPath;
                }
                
                // 启动进程
                _serviceProcess = new Process { StartInfo = startInfo };
                _serviceProcess.Start();
                
                // 等待服务就绪
                await WaitForServiceReady();
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Failed to start Eureka-Audio service: {ex.Message}");
                EmitSignal(SignalName.ServiceFailed, ex.Message);
            }
        }
        
        /// <summary>
        /// 等待服务就绪
        /// </summary>
        private async Task WaitForServiceReady()
        {
            int checkInterval = 1000; // 1秒检查一次
            int maxAttempts = StartupTimeoutMs / checkInterval;
            
            for (int i = 0; i < maxAttempts; i++)
            {
                await Task.Delay(checkInterval);
                
                if (await CheckServiceHealth())
                {
                    _isServiceReady = true;
                    GD.Print("Eureka-Audio service is ready");
                    EmitSignal(SignalName.ServiceReady);
                    return;
                }
                
                GD.Print($"Waiting for service... ({i + 1}/{maxAttempts})");
            }
            
            EmitSignal(SignalName.ServiceFailed, "Service startup timeout");
        }
        
        /// <summary>
        /// 检查服务健康状态
        /// </summary>
        public async Task<bool> CheckServiceHealth()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_serviceUrl}/health");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var health = JsonSerializer.Deserialize<HealthResponse>(content);
                    return health?.ModelLoaded ?? false;
                }
            }
            catch
            {
                // 服务未响应
            }
            return false;
        }
        
        /// <summary>
        /// 转写音频文件
        /// </summary>
        /// <param name="audioPath">音频文件路径</param>
        /// <returns>转写结果</returns>
        public async Task<TranscribeResult> TranscribeFile(string audioPath)
        {
            if (!_isServiceReady)
            {
                return new TranscribeResult
                {
                    Success = false,
                    Error = "Service not ready"
                };
            }
            
            try
            {
                using var form = new MultipartFormDataContent();
                using var fileContent = new ByteArrayContent(File.ReadAllBytes(audioPath));
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
                form.Add(fileContent, "file", Path.GetFileName(audioPath));
                
                var response = await _httpClient.PostAsync($"{_serviceUrl}/transcribe", form);
                var content = await response.Content.ReadAsStringAsync();
                
                return JsonSerializer.Deserialize<TranscribeResult>(content)
                    ?? new TranscribeResult { Success = false, Error = "Invalid response" };
            }
            catch (Exception ex)
            {
                return new TranscribeResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }
        
        /// <summary>
        /// 转写 Base64 编码的音频数据
        /// </summary>
        /// <param name="audioBase64">Base64 编码的音频数据</param>
        /// <param name="format">音频格式 (wav, mp3 等)</param>
        /// <returns>转写结果</returns>
        public async Task<TranscribeResult> TranscribeBase64(string audioBase64, string format = "wav")
        {
            if (!_isServiceReady)
            {
                return new TranscribeResult
                {
                    Success = false,
                    Error = "Service not ready"
                };
            }
            
            try
            {
                var payload = new
                {
                    audio_base64 = audioBase64,
                    format = format
                };
                
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync($"{_serviceUrl}/transcribe_base64", content);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                return JsonSerializer.Deserialize<TranscribeResult>(responseContent)
                    ?? new TranscribeResult { Success = false, Error = "Invalid response" };
            }
            catch (Exception ex)
            {
                return new TranscribeResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }
        
        /// <summary>
        /// 服务是否就绪
        /// </summary>
        public bool IsServiceReady => _isServiceReady;
        
        /// <summary>
        /// 服务 URL
        /// </summary>
        public string ServiceUrl => _serviceUrl;
        
        public override void _ExitTree()
        {
            // 清理资源
            _httpClient?.Dispose();
            
            // 注意：不主动关闭服务进程，让用户手动管理
            // 如果需要自动关闭，取消下面的注释
            // _serviceProcess?.Kill();
        }
    }
    
    /// <summary>
    /// 健康检查响应
    /// </summary>
    public class HealthResponse
    {
        public string Status { get; set; }
        public bool ModelLoaded { get; set; }
    }
    
    /// <summary>
    /// 转写结果
    /// </summary>
    public class TranscribeResult
    {
        public string Text { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
    }
}
