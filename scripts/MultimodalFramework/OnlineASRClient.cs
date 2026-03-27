using Godot;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MultimodalFramework
{
    /// <summary>
    /// 在线 ASR 服务客户端
    /// 使用 OpenAI 兼容的语音转文字 API
    /// </summary>
    public partial class OnlineASRClient : Node
    {
        [Signal]
        public delegate void TranscriptionCompletedEventHandler(string text, bool success, string error);
        
        [Export]
        public string Endpoint { get; set; } = "https://api.openai.com/v1/audio/transcriptions";
        
        [Export]
        public string ApiKey { get; set; } = "";
        
        [Export]
        public string Model { get; set; } = "whisper-1";
        
        private System.Net.Http.HttpClient _httpClient;
        
        public override void _Ready()
        {
            _httpClient = new System.Net.Http.HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60)
            };
        }
        
        /// <summary>
        /// 转写 Base64 编码的音频数据
        /// </summary>
        /// <param name="audioBase64">Base64 编码的音频数据</param>
        /// <param name="format">音频格式 (wav, mp3 等)</param>
        public async void TranscribeBase64(string audioBase64, string format = "mp3")
        {
            GD.Print($"TranscribeBase64 called: format={format}, apiKey={(string.IsNullOrEmpty(ApiKey) ? "null" : "set")}, endpoint={Endpoint}");
            
            if (string.IsNullOrEmpty(ApiKey))
            {
                GD.PrintErr("API Key not configured");
                EmitSignal(SignalName.TranscriptionCompleted, "", false, "API Key not configured");
                return;
            }
            
            try
            {
                // 解码 Base64 数据
                byte[] audioData = Convert.FromBase64String(audioBase64);
                GD.Print($"Audio data size: {audioData.Length} bytes");
                
                // 检查音频数据是否太小（可能是空录音）
                if (audioData.Length < 1000)
                {
                    GD.PrintErr($"Audio data too small ({audioData.Length} bytes), possibly empty recording");
                    EmitSignal(SignalName.TranscriptionCompleted, "", false, $"Audio data too small ({audioData.Length} bytes)");
                    return;
                }
                
                // 创建 multipart/form-data 请求
                using var form = new MultipartFormDataContent();
                
                // 添加音频文件
                var audioContent = new ByteArrayContent(audioData);
                audioContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue($"audio/{format}");
                form.Add(audioContent, "file", $"audio.{format}");
                
                // 添加模型参数
                form.Add(new StringContent(Model), "model");
                
                // 添加响应格式（可选，某些 API 需要）
                // form.Add(new StringContent("json"), "response_format");
                
                GD.Print($"Sending request to {Endpoint} with model {Model}");
                
                // 设置请求头
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiKey);
                
                // 发送请求
                var response = await _httpClient.PostAsync(Endpoint, form);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    // 解析响应
                    var result = JsonSerializer.Deserialize<WhisperResponse>(responseContent);
                    string text = result?.Text ?? "";
                    
                    if (string.IsNullOrEmpty(text))
                    {
                        GD.PrintErr($"ASR returned empty text. Response: {responseContent}");
                        EmitSignal(SignalName.TranscriptionCompleted, "", false, "ASR returned empty text");
                    }
                    else
                    {
                        GD.Print($"ASR Result: {text}");
                        EmitSignal(SignalName.TranscriptionCompleted, text, true, "");
                    }
                }
                else
                {
                    GD.PrintErr($"ASR API Error: {response.StatusCode} - {responseContent}");
                    EmitSignal(SignalName.TranscriptionCompleted, "", false, $"API Error: {response.StatusCode} - {responseContent}");
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"ASR Error: {ex.Message}\nStackTrace: {ex.StackTrace}");
                EmitSignal(SignalName.TranscriptionCompleted, "", false, $"Exception: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 转写音频文件
        /// </summary>
        /// <param name="filePath">音频文件路径</param>
        public async void TranscribeFile(string filePath)
        {
            if (string.IsNullOrEmpty(ApiKey))
            {
                EmitSignal(SignalName.TranscriptionCompleted, "", false, "API Key not configured");
                return;
            }
            
            try
            {
                // 读取文件
                byte[] audioData = System.IO.File.ReadAllBytes(filePath);
                string format = System.IO.Path.GetExtension(filePath).TrimStart('.');
                if (string.IsNullOrEmpty(format)) format = "mp3"; 
                
                // 创建 multipart/form-data 请求
                using var form = new MultipartFormDataContent();
                
                var audioContent = new ByteArrayContent(audioData);
                audioContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue($"audio/{format}");
                form.Add(audioContent, "file", System.IO.Path.GetFileName(filePath));
                form.Add(new StringContent(Model), "model");
                
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiKey);
                
                var response = await _httpClient.PostAsync(Endpoint, form);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<WhisperResponse>(responseContent);
                    string text = result?.Text ?? "";
                    
                    GD.Print($"ASR Result: {text}");
                    EmitSignal(SignalName.TranscriptionCompleted, text, true, "");
                }
                else
                {
                    GD.PrintErr($"ASR API Error: {response.StatusCode} - {responseContent}");
                    EmitSignal(SignalName.TranscriptionCompleted, "", false, $"API Error: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"ASR Error: {ex.Message}");
                EmitSignal(SignalName.TranscriptionCompleted, "", false, ex.Message);
            }
        }
        
        public override void _ExitTree()
        {
            _httpClient?.Dispose();
        }
    }
    
    /// <summary>
    /// Whisper API 响应模型
    /// </summary>
    public class WhisperResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("text")]
        public string Text { get; set; }
    }
}
