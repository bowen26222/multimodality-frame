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
        public Control CreateStoryNodeUI(StoryNode storyNode)
        {
            var newPanel = StoryNodePanel.Instantiate() as Button;
            newPanel.Text = storyNode.NodeTitle;
            return newPanel;
        }
    }
}
