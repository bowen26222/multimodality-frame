using Godot;
using MultimodalFramework;

/// <summary>
/// 演示场景：展示如何使用多模态交互框架
/// </summary>
public partial class DemoScene : Node3D
{
    private MultimodalController _controller;
    private Label _statusLabel;
    private Button _recordButton;
    
    public override void _Ready()
    {
        // 创建UI
        SetupUI();
        
        // 创建多模态控制器
        _controller = new MultimodalController();
        AddChild(_controller);
        
        // 配置API密钥（请替换为你的实际API密钥）
        _controller.SetApiKey("your-api-key-here");
        
        // 注册示例选项
        RegisterOptions();
        
        // 连接信号
        _controller.OptionMatched += OnOptionMatched;
        _controller.OptionExecuted += OnOptionExecuted;
        _controller.NoMatch += OnNoMatch;
        _controller.Error += OnError;
        
        GD.Print("Multimodal Framework Demo Ready");
        GD.Print("Press SPACE to start/stop recording");
    }
    
    private void SetupUI()
    {
        // 创建CanvasLayer
        var canvas = new CanvasLayer();
        AddChild(canvas);
        
        // 创建Control容器
        var control = new Control();
        control.AnchorsPreset = (int)Control.LayoutPreset.FullRect;
        canvas.AddChild(control);
        
        // 状态标签
        _statusLabel = new Label();
        _statusLabel.Position = new Vector2I(20, 20);
        _statusLabel.Size = new Vector2I(760, 100);
        _statusLabel.Text = "按空格键开始录音";
        _statusLabel.AddThemeFontSizeOverride("font_size", 24);
        control.AddChild(_statusLabel);
        
        // 录音按钮
        _recordButton = new Button();
        _recordButton.Position = new Vector2I(300, 400);
        _recordButton.Size = new Vector2I(200, 60);
        _recordButton.Text = "开始录音";
        _recordButton.AddThemeFontSizeOverride("font_size", 20);
        _recordButton.Pressed += OnRecordButtonPressed;
        control.AddChild(_recordButton);
    }
    
    private void RegisterOptions()
    {
        var registry = _controller.GetOptionRegistry();
        
        // 示例1：移动命令
        registry.Register(new OptionBuilder()
            .WithId("move_forward")
            .WithDescription("让角色向前移动")
            .WithKeywords("前进", "向前", "走", "移动")
            .WithAction(parameters => 
            {
                GD.Print("执行：向前移动");
                // 在这里实现实际的移动逻辑
            })
            .Build());
        
        // 示例2：攻击命令
        registry.Register(new OptionBuilder()
            .WithId("attack")
            .WithDescription("让角色进行攻击")
            .WithKeywords("攻击", "打", "战斗", "进攻")
            .WithAction(parameters =>
            {
                GD.Print("执行：攻击");
                // 在这里实现实际的攻击逻辑
            })
            .Build());
        
        // 示例3：跳跃命令（带参数）
        registry.Register(new OptionBuilder()
            .WithId("jump")
            .WithDescription("让角色跳跃")
            .WithKeywords("跳", "跳跃", "起跳")
            .WithAction(parameters =>
            {
                float height = 1.0f;
                if (parameters.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    if (parameters.TryGetProperty("height", out var heightProp))
                    {
                        height = (float)heightProp.GetDouble();
                    }
                }
                GD.Print($"执行：跳跃，高度 {height}");
            })
            .Build());
        
        // 示例4：打开菜单
        registry.Register(new OptionBuilder()
            .WithId("open_menu")
            .WithDescription("打开游戏菜单")
            .WithKeywords("菜单", "设置", "选项", "暂停")
            .WithAction(() =>
            {
                GD.Print("执行：打开菜单");
            })
            .Build());
        
        // 示例5：使用物品
        registry.Register(new OptionBuilder()
            .WithId("use_item")
            .WithDescription("使用指定的物品")
            .WithKeywords("使用", "物品", "道具", "装备")
            .WithAction(parameters =>
            {
                string itemName = "未知物品";
                if (parameters.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    if (parameters.TryGetProperty("item", out var itemProp))
                    {
                        itemName = itemProp.GetString();
                    }
                }
                GD.Print($"执行：使用物品 - {itemName}");
            })
            .Build());
    }
    
    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            if (keyEvent.Keycode == Key.Space)
            {
                ToggleRecording();
            }
        }
    }
    
    private void ToggleRecording()
    {
        if (_controller.IsRecording)
        {
            _controller.StopListening();
            _statusLabel.Text = "正在处理...";
            _recordButton.Text = "处理中";
        }
        else if (!_controller.IsProcessingRequest)
        {
            _controller.StartListening();
            _statusLabel.Text = "正在录音...按空格停止";
            _recordButton.Text = "停止录音";
        }
    }
    
    private void OnRecordButtonPressed()
    {
        ToggleRecording();
    }
    
    private void OnOptionMatched(string optionId, float confidence)
    {
        _statusLabel.Text = $"匹配成功！\n选项: {optionId}\n置信度: {confidence:P0}";
        _recordButton.Text = "开始录音";
    }
    
    private void OnOptionExecuted(string optionId, bool success)
    {
        if (success)
        {
            GD.Print($"选项 {optionId} 执行成功");
        }
    }
    
    private void OnNoMatch(string reason, string userIntent)
    {
        _statusLabel.Text = $"未匹配到选项\n原因: {reason}\n用户意图: {userIntent}";
        _recordButton.Text = "开始录音";
    }
    
    private void OnError(string message)
    {
        _statusLabel.Text = $"错误: {message}";
        _recordButton.Text = "开始录音";
        GD.PrintErr(message);
    }
}
