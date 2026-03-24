# 多模态AI交互框架

一个基于Godot 4的多模态AI交互框架，支持语音输入、本地语音转文字、多模态模型识别和选项匹配执行。

## 功能特性

- 🎤 **语音录制**：使用Godot内置的AudioEffectRecord进行语音录制
- 🗣️ **本地语音转文字**：集成 Eureka-Audio 开源模型，支持本地离线语音识别
- 🤖 **多模态识别**：支持 Qwen、Minimax 等大模型进行意图识别
- 🎯 **选项匹配**：通过结构化输出实现精准的意图识别和选项匹配
- ⚡ **函数执行**：匹配成功后自动执行绑定的函数

## 架构设计

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│  VoiceRecorder  │────▶│ EurekaService   │────▶│  QwenVLClient   │
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

### 1. 安装依赖

#### Python 环境（用于 Eureka-Audio 服务）

```bash
# 安装 Eureka-Audio 服务依赖
cd eureka_service
pip install -r requirements.txt

# 下载模型（首次运行会自动下载）
# 模型大小约 3.5GB，需要耐心等待
```

#### Godot 项目配置

1. 复制 `config.example.json` 为 `config.json`
2. 填写你的 API 密钥

### 2. 配置文件

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
    "useLocal": true,
    "servicePort": 8765,
    "serviceHost": "127.0.0.1",
    "modelPath": "",
    "startupTimeoutMs": 60000
  }
}
```

### 3. 注册选项

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

### 4. 开始语音识别

```csharp
// 开始录音
_controller.StartListening();

// 停止录音并处理
_controller.StopListening();
```

### 5. 处理结果

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

### EurekaServiceManager

本地语音转文字服务管理器：

- 游戏启动时自动部署 Eureka-Audio 服务
- 提供 HTTP API 供 Godot 调用
- 支持文件和 Base64 编码的音频输入

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
- `SendVoiceForMatching(string audioBase64, string format)` - 发送语音（旧方式）
- 信号：`ResponseReceived`, `RequestFailed`

### MultimodalController

主控制器，整合所有组件：

- `StartListening()` - 开始语音录制
- `StopListening()` - 停止录制并处理
- `GetOptionRegistry()` - 获取选项注册中心
- `IsASRReady` - ASR 服务是否就绪
- 信号：`TranscriptionCompleted`, `OptionMatched`, `OptionExecuted`, `NoMatch`, `Error`, `ServiceStatusChanged`

## Eureka-Audio 服务

### 手动启动服务

```bash
# Windows
eureka_service\start_service.bat

# Linux/macOS
./eureka_service/start_service.sh
```

### API 接口

服务启动后提供以下接口：

- `GET /health` - 健康检查
- `POST /transcribe` - 上传音频文件转写
- `POST /transcribe_base64` - Base64 编码音频转写

### 模型下载

首次运行时，模型会自动从 HuggingFace 下载：

- 模型：`cslys1999/Eureka-Audio-Instruct`
- 大小：约 3.5GB
- 国内用户可使用 ModelScope 镜像：`lys1999/Eureka-Audio-Instruct`

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
5. Eureka-Audio 模型首次加载需要较长时间，请耐心等待
6. 建议使用 GPU 运行 Eureka-Audio 以获得更好的性能

## 系统要求

- Godot 4.x
- Python 3.8+（用于 Eureka-Audio 服务）
- CUDA 11.x+（推荐，用于 GPU 加速）
- 内存：至少 8GB（模型加载需要约 4GB）

## 许可证

MIT License
