# 多模态AI交互框架

一个基于Godot 4的多模态AI交互框架，支持语音输入、在线语音转文字、多模态模型识别和选项匹配执行。

## 功能特性

- 🎤 **语音录制**：使用Godot内置的AudioEffectRecord进行语音录制
- 🗣️ **在线语音转文字**：支持 OpenAI 兼容的 ASR API（如 Whisper）
- 🤖 **多模态识别**：支持 Qwen、Minimax 等大模型进行意图识别
- 🎯 **选项匹配**：通过结构化输出实现精准的意图识别和选项匹配
- ⚡ **函数执行**：匹配成功后自动执行绑定的函数

## 架构设计

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│  VoiceRecorder  │────▶│  OnlineASRClient│────▶│  QwenVLClient   │
│   (语音录制)     │     │  (语音转文字)    │     │   (意图识别)     │
└─────────────────┘     └─────────────────┘     └─────────────────┘
         │                      │                       │
         └──────────────────────┴───────────────────────┘
                                │
                    ┌───────────▼───────────┐
                    │  MultimodalController │
                    │      (主控制器)        │
                    └───────────────────────┘
```

## 快速开始

### 1. 配置文件

复制 `config.example.json` 为 `config.json`，并填写你的 API 密钥：

```json
{
  "api": {
    "endpoint": "https://api.ppio.com/openai/v1/chat/completions",
    "key": "your-api-key-here",
    "model": "minimax/minimax-m2.5",
    "temperature": 0.1,
    "maxTokens": 1024
  },
  "recording": {
    "autoStart": false
  },
  "asr": {
    "useOnline": true,
    "endpoint": "https://api.ppio.com/openai/v1/audio/transcriptions",
    "key": "your-asr-api-key-here",
    "model": "openai/whisper-large-v3"
  }
}
```

### 配置说明

| 字段 | 说明 |
|------|------|
| `api.endpoint` | 大模型 API 端点 |
| `api.key` | 大模型 API 密钥 |
| `api.model` | 使用的模型名称 |
| `asr.endpoint` | 语音转文字 API 端点（OpenAI 兼容格式） |
| `asr.key` | ASR API 密钥（可与主 API 使用相同密钥） |
| `asr.model` | ASR 模型名称（如 whisper-1, openai/whisper-large-v3） |

### 2. 注册选项

使用`OptionBuilder`创建并注册选项：

```csharp
var registry = _controller.GetOptionRegistry();

registry.Register(new OptionBuilder()
    .WithId("attack")
    .WithDescription("让角色进行攻击")
    .WithKeywords("攻击", "打", "战斗")
    .WithAction(parameters => 
    {
        // 执行攻击逻辑
        Player.Attack();
    })
    .Build());
```

### 3. 开始语音识别

```csharp
// 开始录音
_controller.StartListening();

// 停止录音并处理
_controller.StopListening();
```

### 4. 处理结果

连接信号处理匹配结果：

```csharp
// 转写完成
_controller.TranscriptionCompleted += (text) => 
{
    GD.Print($"语音转文字结果: {text}");
};

// 匹配成功
_controller.OptionMatched += (optionId, confidence) => 
{
    GD.Print($"匹配成功: {optionId}, 置信度: {confidence}");
};

// 未匹配
_controller.NoMatch += (reason, userIntent) => 
{
    GD.Print($"未匹配: {reason}");
};

// 服务状态
_controller.ServiceStatusChanged += (isReady) => 
{
    GD.Print($"ASR服务状态: {(isReady ? "就绪" : "未就绪")}");
};
```

## 核心组件

### OnlineASRClient

在线语音转文字客户端：

- 支持 OpenAI 兼容的 ASR API
- 支持 Base64 编码和文件路径输入
- 信号：`TranscriptionCompleted`

### IOption 接口

定义可匹配选项的标准接口：

```csharp
public interface IOption
{
    string Id { get; }           // 选项唯一标识
    string Description { get; }  // 选项描述
    string[] Keywords { get; }   // 关键词列表
    void Execute(JsonElement parameters);  // 执行函数
}
```

### OptionRegistry

选项注册中心，管理所有可匹配的选项：

- `Register(IOption)` - 注册选项
- `Unregister(string)` - 注销选项
- `GetOption(string)` - 获取选项
- `GenerateOptionsDescription()` - 生成AI提示词

### VoiceRecorder

语音录制组件：

- `StartRecording()` - 开始录制
- `StopRecording()` - 停止录制
- `CancelRecording()` - 取消录制
- 信号：`RecordingCompleted`, `RecordingFailed`

### QwenVLClient

大模型 API 客户端：

- `SendTextForMatching(string text)` - 发送文本进行匹配
- 信号：`ResponseReceived`, `RequestFailed`

### MultimodalController

主控制器，整合所有组件：

- `StartListening()` - 开始语音录制
- `StopListening()` - 停止录制并处理
- `GetOptionRegistry()` - 获取选项注册中心
- `IsASRReady` - ASR 服务是否就绪（在线服务始终为 true）
- 信号：`TranscriptionCompleted`, `OptionMatched`, `OptionExecuted`, `NoMatch`, `Error`, `ServiceStatusChanged`

## 自定义选项

### 方式一：使用BaseOption

```csharp
var option = new BaseOption(
    "heal",
    "使用治疗技能恢复生命值",
    new[] { "治疗", "回血", "恢复" },
    parameters => Player.Heal()
);
registry.Register(option);
```

### 方式二：实现IOption接口

```csharp
public class AttackOption : IOption
{
    public string Id => "attack";
    public string Description => "执行攻击动作";
    public string[] Keywords => new[] { "攻击", "打" };
    
    public void Execute(JsonElement parameters)
    {
        // 复杂的攻击逻辑
        var target = parameters.TryGetProperty("target", out var t) 
            ? t.GetString() 
            : "nearest";
        CombatSystem.Attack(target);
    }
}
```

## 结构化输出格式

AI返回的JSON格式：

**匹配成功：**
```json
{
    "matched": true,
    "option_id": "attack",
    "confidence": 0.95,
    "parameters": {
        "target": "enemy_1"
    }
}
```

**未匹配：**
```json
{
    "matched": false,
    "reason": "未找到匹配的选项",
    "user_intent": "用户想要保存游戏"
}
```

## 注意事项

1. 确保麦克风权限已授权
2. API密钥请妥善保管，不要提交到版本控制
3. 录制时长默认最大30秒，可通过`RecordingMaxDuration`调整
4. 匹配置信度阈值默认0.7，可在提示词中调整

## 系统要求

- Godot 4.x
- .NET 6.0+

## 许可证

MIT License
