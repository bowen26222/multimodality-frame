using Godot;
using MultimodalFramework.UI;
using MultimodalFramework.Camera;
using MultimodalFramework.Dialog;
using System.Collections.Generic;

namespace MultimodalFramework.Demo
{
    /// <summary>
    /// 演示场景：展示如何使用 StoryNode 数据生成 UI
    /// </summary>
    public partial class ControllerDemo : Node2D
    {
        private CameraController _camera;
        private UIPositionController _uiController;
        private Label _infoLabel;
        
        // 示例数据：StoryNode 列表
        private List<StoryNode> _storyNodes;
        
        public override void _Ready()
        {
            SetupCamera();
            SetupUIController();
            CreateDemoData();
            CreateUIFromStoryNodes();
            
            GD.Print("Controller Demo Ready");
            GD.Print("- 鼠标移向屏幕边缘移动相机");
            GD.Print("- WASD / 方向键移动相机");
            GD.Print("- 滚轮缩放 / 中键拖拽");
            GD.Print("- R 键重置相机");
        }
        
        private void SetupCamera()
        {
            _camera = new CameraController
            {
                Name = "MainCamera",
                MoveSpeed = 350f,
                EdgeThreshold = 80f,
                EnableEdgeMovement = true,
                EnableKeyboardMovement = true,
                UseSmoothing = true,
                MinZoom = 0.3f,
                MaxZoom = 3.0f
            };
            
            AddChild(_camera);
            _camera.PositionChanged += _ => UpdateInfoLabel();
            _camera.ZoomChanged += _ => UpdateInfoLabel();
        }
        
        private void SetupUIController()
        {
            _uiController = new UIPositionController
            {
                CoordinateMode = UICoordinateMode.World
            };
            AddChild(_uiController);
        }
        
        private void CreateDemoData()
        {
            // 创建示例 StoryNode 数据
            _storyNodes = new List<StoryNode>
            {
                new StoryNode
                {
                    NodeId = "node_start",
                    NodeTitle = "开始",
                    NodeSummary = "故事的起点",
                    DialogPath = "res://dialogs/start.dialogue"
                },
                new StoryNode
                {
                    NodeId = "node_forest",
                    NodeTitle = "森林",
                    NodeSummary = "神秘的森林入口",
                    DialogPath = "res://dialogs/forest.dialogue"
                },
                new StoryNode
                {
                    NodeId = "node_castle",
                    NodeTitle = "城堡",
                    NodeSummary = "古老的城堡遗迹",
                    DialogPath = "res://dialogs/castle.dialogue"
                },
                new StoryNode
                {
                    NodeId = "node_village",
                    NodeTitle = "村庄",
                    NodeSummary = "宁静的小村庄",
                    DialogPath = "res://dialogs/village.dialogue"
                }
            };
        }
        
        private void CreateUIFromStoryNodes()
        {
            // 创建信息标签（屏幕固定）
            var screenUI = new UIPositionController { CoordinateMode = UICoordinateMode.Screen };
            AddChild(screenUI);
            
            // 直接创建并添加信息标签
            _infoLabel = new Label
            {
                Position = new Vector2(20, 20),
                Size = new Vector2(400, 80)
            };
            _infoLabel.AddThemeFontSizeOverride("font_size", 18);
            
            // 使用 CreateElement 添加到控制器
            screenUI.CreateElement(_infoLabel, "info", label => label);
            
            // 使用 StoryNode 数据创建世界坐标 UI
            _uiController.CreateElements(
                _storyNodes,
                node => node.NodeId,  // ID 提取
                CreateStoryNodeUI      // UI 创建函数
            );
            
            UpdateInfoLabel();
        }
        
        /// <summary>
        /// 根据 StoryNode 创建 UI 控件
        /// </summary>
        private Control CreateStoryNodeUI(StoryNode node)
        {
            // 计算位置（示例：环形布局）
            int index = _storyNodes.IndexOf(node);
            float angle = index * (Mathf.Pi * 2 / _storyNodes.Count);
            float radius = 300f;
            
            Vector2 position = new Vector2(
                Mathf.Cos(angle) * radius + 500,
                Mathf.Sin(angle) * radius + 400
            );
            
            // 创建容器
            var container = new PanelContainer
            {
                Position = position,
                CustomMinimumSize = new Vector2(180, 100)
            };
            
            // 添加样式
            var style = new StyleBoxFlat
            {
                BgColor = new Color(0.15f, 0.15f, 0.2f, 0.9f),
                BorderColor = new Color(0.4f, 0.6f, 0.8f)
            };
            style.SetBorderWidthAll(2);
            style.SetCornerRadiusAll(8);
            container.AddThemeStyleboxOverride("panel", style);
            
            // 创建内容
            var vbox = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            container.AddChild(vbox);
            
            // 标题
            var titleLabel = new Label
            {
                Text = node.NodeTitle,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            titleLabel.AddThemeFontSizeOverride("font_size", 20);
            titleLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.9f, 1.0f));
            vbox.AddChild(titleLabel);
            
            // 摘要
            var summaryLabel = new Label
            {
                Text = node.NodeSummary,
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            summaryLabel.AddThemeFontSizeOverride("font_size", 14);
            summaryLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
            vbox.AddChild(summaryLabel);
            
            // 添加点击交互
            container.GuiInput += @event =>
            {
                if (@event is InputEventMouseButton btn && btn.Pressed && btn.ButtonIndex == MouseButton.Left)
                {
                    OnStoryNodeClicked(node, container);
                }
            };
            
            // 鼠标悬停效果
            container.MouseEntered += () =>
            {
                style.BorderColor = new Color(0.6f, 0.8f, 1.0f);
                style.SetBorderWidthAll(3);
            };
            
            container.MouseExited += () =>
            {
                style.BorderColor = new Color(0.4f, 0.6f, 0.8f);
                style.SetBorderWidthAll(2);
            };
            
            return container;
        }
        
        private void OnStoryNodeClicked(StoryNode node, Control element)
        {
            GD.Print($"点击了节点: {node.NodeTitle} ({node.NodeId})");
            
            // 移动相机到节点位置
            _camera.MoveTo(element.Position + element.Size / 2);
        }
        
        private void UpdateInfoLabel()
        {
            if (_infoLabel != null)
            {
                _infoLabel.Text = $"相机: ({_camera.GlobalPosition.X:F0}, {_camera.GlobalPosition.Y:F0}) | 缩放: {_camera.Zoom.X:F2}x\n" +
                                  $"节点数: {_storyNodes.Count} | 按 R 重置相机";
            }
        }
        
        public override void _Input(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.R)
            {
                _camera.ResetToOrigin();
            }
        }
    }
}
