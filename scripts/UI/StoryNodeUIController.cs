using Godot;
using MultimodalFramework.Dialog;
using System;
using System.Collections.Generic;

namespace MultimodalFramework.UI
{

    /// <summary>
    /// StoryUI
    /// 使用委托模式，支持任意数据类型
    /// </summary>
    public partial class StoryNodeUIController : Node
    {
        [Export] public PackedScene StoryNodePanel;
        [Export] public Label TitleLabel;
        [Export] public RichTextLabel SummaryLabel;
        [Export] public Button StoryNameButton;
        [Export] public Control SummaryPanel;
        [Export] public Button BackButton;
        [Export] public Button EnterButton;
        
        public override void _Ready()
        {
            TitleLabel = GetNode<Label>("SummaryPanel/VBoxContainer/Title");
            SummaryLabel = GetNode<RichTextLabel>("SummaryPanel/VBoxContainer/ScrollContainer/Content");
            SummaryPanel = GetNode<Control>("SummaryPanel");
            StoryNameButton = GetNode<Button>("StoryNameButton");
            StoryNameButton.Pressed += OpenSummaryPanel;
            BackButton = GetNode<Button>("SummaryPanel/VBoxContainer/BackButton");
            BackButton.Pressed += CloseSummaryPanel;
            EnterButton = GetNode<Button>("SummaryPanel/VBoxContainer/EnterButton");
            EnterButton.Pressed += OnEnterButtonPressed;

        }
        public void SetStoryNode(StoryNode storyNode)
        {
            TitleLabel.Text = storyNode.NodeTitle;
            SummaryLabel.Text = storyNode.NodeSummary;
        }
        public void OpenSummaryPanel()
        {
            SummaryPanel.Show();
            StoryNameButton.Hide();
        }
        public void CloseSummaryPanel()
        {
            SummaryPanel.Hide();
            StoryNameButton.Show();
        }
        public void OnEnterButtonPressed()
        {
            GD.Print("进入故事节点，加载对话资源...");
            // 在这里可以添加加载对话资源的逻辑，例如：
            // var dialogResource = ResourceLoader.Load<DialogResource>(storyNode.DialogPath);
            // DialogManager.Instance.StartDialog(dialogResource);
        }
    }
}
