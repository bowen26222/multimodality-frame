using Godot;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MultimodalFramework
{
    /// <summary>
    /// 大模型 API 客户端，支持文本输入和结构化输出
    /// </summary>
    public partial class LLMClient : Node
    {
        [Signal]
        public delegate void ResponseReceivedEventHandler(string optionId, float confidence, string parametersJson, bool matched, string reason, string userIntent);
        
        [Signal]
        public delegate void RequestFailedEventHandler(string error);
        
        [Export]
        public string ApiEndpoint { get; set; } = "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions";
        
        [Export]
        public string ApiKey { get; set; } = "";
        
        [Export]
        public string ModelName { get; set; } = "qwen-vl-max";
        
        [Export]
        public float Temperature { get; set; } = 0.1f;
        
        [Export]
        public int MaxTokens { get; set; } = 1024;
        
        private HttpRequest _httpRequest;
        private OptionRegistry _optionRegistry;
        
        public override void _Ready()
        {
            _httpRequest = new HttpRequest();
            AddChild(_httpRequest);
            _httpRequest.RequestCompleted += OnRequestCompleted;
        }
        
        /// <summary>
        /// 设置选项注册中心
        /// </summary>
        public void SetOptionRegistry(OptionRegistry registry)
        {
            _optionRegistry = registry;
        }
        
        /// <summary>
        /// 发送文本进行选项匹配
        /// </summary>
        /// <param name="text">用户输入的文本</param>
        public void SendTextForMatching(string text)
        {
            if (string.IsNullOrEmpty(ApiKey))
            {
                EmitSignal(SignalName.RequestFailed, "API Key not configured");
                return;
            }
            
            if (_optionRegistry == null)
            {
                EmitSignal(SignalName.RequestFailed, "Option registry not set");
                return;
            }
            
            var requestBody = BuildTextRequestBody(text);
            SendRequest(requestBody);
        }
        
        /// <summary>
        /// 发送语音进行识别和匹配（旧方式，可能不被所有模型支持）
        /// </summary>
        /// <param name="audioBase64">Base64编码的音频数据</param>
        /// <param name="audioFormat">音频格式（wav, mp3等）</param>
        public void SendVoiceForMatching(string audioBase64, string audioFormat = "wav")
        {
            if (string.IsNullOrEmpty(ApiKey))
            {
                EmitSignal(SignalName.RequestFailed, "API Key not configured");
                return;
            }
            
            if (_optionRegistry == null)
            {
                EmitSignal(SignalName.RequestFailed, "Option registry not set");
                return;
            }
            
            var requestBody = BuildAudioRequestBody(audioBase64, audioFormat);
            SendRequest(requestBody);
        }
        
        /// <summary>
        /// 构建文本请求体
        /// </summary>
        private string BuildTextRequestBody(string text)
        {
            var optionsDescription = _optionRegistry.GenerateOptionsDescription();
            
            var systemPrompt = $@"你是一个多模态交互助手。你的任务是分析用户的输入，并从可用选项中选择最匹配的一个。

{optionsDescription}

请严格按照以下JSON格式输出：
{{
    ""parameters"": {{}} // 从用户输入中提取的参数
    ""confidence"": 0.0-1.0的置信度,
    ""matched"": true/false,
    ""option_id"": ""选项ID（如果匹配到）"",
}}

如果没有匹配到任何选项，请输出：
{{
    ""user_intent"": ""用户可能的意图描述""
    ""reason"": ""未匹配的原因"",
    ""matched"": false,
}}

注意：
1. 只有当置信度超过0.7时才认为匹配成功
2. 参数字段应包含执行选项所需的任何额外信息
3. 必须严格按照JSON格式输出，不要包含其他文字";

            var request = new
            {
                model = ModelName,
                messages = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["role"] = "system",
                        ["content"] = systemPrompt
                    },
                    new Dictionary<string, object>
                    {
                        ["role"] = "user",
                        ["content"] = text
                    }
                },
                temperature = Temperature,
                max_tokens = MaxTokens,
                response_format = new Dictionary<string, string> { ["type"] = "json_object" }
            };
            
            return JsonSerializer.Serialize(request);
        }
        
        /// <summary>
        /// 构建音频请求体（旧方式）
        /// </summary>
        private string BuildAudioRequestBody(string audioBase64, string audioFormat)
        {
            var optionsDescription = _optionRegistry.GenerateOptionsDescription();
            
            var systemPrompt = $@"你是一个多模态交互助手。你的任务是分析用户的输入，并从可用选项中选择最匹配的一个。

{optionsDescription}

请严格按照以下JSON格式输出：
{{
    ""matched"": true/false,
    ""option_id"": ""选项ID（如果匹配到）"",
    ""confidence"": 0.0-1.0的置信度,
    ""parameters"": {{}} // 从用户输入中提取的参数
}}

如果没有匹配到任何选项，请输出：
{{
    ""matched"": false,
    ""reason"": ""未匹配的原因"",
    ""user_intent"": ""用户可能的意图描述""
}}

注意：
1. 只有当置信度超过0.7时才认为匹配成功
2. 参数字段应包含执行选项所需的任何额外信息
3. 必须严格按照JSON格式输出，不要包含其他文字";

            // 构建消息内容
            var contentArray = new List<object>
            {
                new
                {
                    type = "audio",
                    audio = $"data:audio/{audioFormat};base64,{audioBase64}"
                },
                new
                {
                    type = "text",
                    text = "请分析这段语音并匹配最合适的选项"
                }
            };
            
            var request = new
            {
                model = ModelName,
                messages = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["role"] = "system",
                        ["content"] = systemPrompt
                    },
                    new Dictionary<string, object>
                    {
                        ["role"] = "user",
                        ["content"] = contentArray
                    }
                },
                temperature = Temperature,
                max_tokens = MaxTokens,
                response_format = new Dictionary<string, string> { ["type"] = "json_object" }
            };
            
            return JsonSerializer.Serialize(request);
        }
        
        /// <summary>
        /// 发送HTTP请求
        /// </summary>
        private void SendRequest(string body)
        {
            var headers = new string[]
            {
                "Content-Type: application/json",
                $"Authorization: Bearer {ApiKey}"
            };
            
            var error = _httpRequest.Request(
                ApiEndpoint,
                headers,
                HttpClient.Method.Post,
                body
            );
            
            if (error != Error.Ok)
            {
                EmitSignal(SignalName.RequestFailed, $"HTTP request failed: {error}");
            }
        }
        
        /// <summary>
        /// 处理API响应
        /// </summary>
        private void OnRequestCompleted(long result, long responseCode, string[] headers, byte[] body)
        {
            if (responseCode != 200)
            {
                var errorBody = Encoding.UTF8.GetString(body);
                EmitSignal(SignalName.RequestFailed, $"API returned {responseCode}: {errorBody}");
                return;
            }
            
            try
            {
                var responseText = Encoding.UTF8.GetString(body);
                GD.Print($"API Response: {responseText}");
                ParseAndEmitResponse(responseText);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Failed to parse response: {ex.Message}");
                EmitSignal(SignalName.RequestFailed, $"Failed to parse response: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 清理 Markdown 代码块格式，提取纯 JSON 内容
        /// </summary>
        /// <param name="content">可能包含 Markdown 代码块的内容</param>
        /// <returns>清理后的 JSON 字符串</returns>
        private string StripMarkdownCodeBlock(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return content;
            
            var trimmed = content.Trim();
            
            // 检查是否以 ``` 开头（Markdown 代码块）
            if (trimmed.StartsWith("```"))
            {
                // 使用正则表达式匹配代码块
                // 支持 ```json ... ``` 或 ``` ... ``` 格式
                var match = Regex.Match(trimmed, @"^```(?:json)?\s*\n?([\s\S]*?)\n?```$", RegexOptions.Multiline);
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }
                
                // 如果正则没匹配到，尝试简单的字符串处理
                var lines = trimmed.Split('\n');
                var jsonLines = new List<string>();
                bool inCodeBlock = false;
                
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    if (trimmedLine.StartsWith("```"))
                    {
                        if (!inCodeBlock)
                        {
                            inCodeBlock = true;
                            continue;
                        }
                        else
                        {
                            break; // 结束代码块
                        }
                    }
                    
                    if (inCodeBlock)
                    {
                        jsonLines.Add(line);
                    }
                }
                
                if (jsonLines.Count > 0)
                {
                    return string.Join("\n", jsonLines).Trim();
                }
            }
            
            return content.Trim();
        }
        
        /// <summary>
        /// 解析API响应并发送信号
        /// </summary>
        private void ParseAndEmitResponse(string responseText)
        {
            var jsonDoc = JsonDocument.Parse(responseText);
            var root = jsonDoc.RootElement;
            
            // 提取choices[0].message.content
            var content = root
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
            
            // 清理可能的 Markdown 代码块格式
            content = StripMarkdownCodeBlock(content);
            
            GD.Print($"Parsed content: {content}");
            
            // 解析结构化输出
            var resultJson = JsonDocument.Parse(content);
            var resultRoot = resultJson.RootElement;
            
            bool matched = resultRoot.GetProperty("matched").GetBoolean();
            string optionId = "";
            float confidence = 0f;
            string parametersJson = "{}";
            string reason = "";
            string userIntent = "";
            
            if (matched)
            {
                optionId = resultRoot.GetProperty("option_id").GetString() ?? "";
                confidence = (float)resultRoot.GetProperty("confidence").GetDouble();
                
                if (resultRoot.TryGetProperty("parameters", out var paramsElement))
                {
                    parametersJson = paramsElement.GetRawText();
                }
            }
            else
            {
                if (resultRoot.TryGetProperty("reason", out var reasonElement))
                {
                    reason = reasonElement.GetString() ?? "";
                }
                if (resultRoot.TryGetProperty("user_intent", out var intentElement))
                {
                    userIntent = intentElement.GetString() ?? "";
                }
            }
            
            EmitSignal(SignalName.ResponseReceived, optionId, confidence, parametersJson, matched, reason, userIntent);
        }
    }
}
