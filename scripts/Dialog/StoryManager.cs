using Godot;
using MultimodalFramework;
using DialogueManagerRuntime;
using System.Collections.Generic;
using MultimodalFramework.UI;

namespace MultimodalFramework.Dialog;
/// <summary>
/// 对话和AI接入中继
/// </summary>
[GlobalClass]
public partial class StoryManager : Node
{   
    public static StoryManager Instance {get; private set;}

    private UIPositionController myUIPositionController;
    public override void _Ready()
    {
        Instance = this;
        myUIPositionController = new ();
    }
    public void SetStorys(string path)
    {
        
    }
    public List<StoryNode> FindAllStoryNodes()
    {
        var storyNodes = new List<StoryNode>();
        
        // 获取所有 StoryNode 类型的资源
        var resourceList = ResourceLoader.ListDirectory("res://story/");
        foreach (var file in resourceList)
        {
            if (file.EndsWith(".tres"))
            {
                var node = ResourceLoader.Load<StoryNode>($"res://story/{file}");
                if (node != null)
                    storyNodes.Add(node);
            }
        }
        
        return storyNodes;
    }
    public Control CreateStoryNode(StoryNode storyNode)
    {
        // 使用 UIPositionController 创建 UI 元素
        var control = myUIPositionController.CreateElement(storyNode, storyNode.NodeId, CreateStoryNodeControl);
        return control;
    }

    private Control CreateStoryNodeControl(StoryNode storyNode)
    {
        // 创建一个简单的 Control 来展示 StoryNode 的内容
        var container = new VBoxContainer();
        var titleLabel = new Label { Text = $"Title: {storyNode.NodeTitle}" };
        var summaryLabel = new Label { Text = $"Summary: {storyNode.NodeSummary}" };
        var dialogPathLabel = new Label { Text = $"Dialog Path: {storyNode.DialogPath}" };
        
        container.AddChild(titleLabel);
        container.AddChild(summaryLabel);
        container.AddChild(dialogPathLabel);
        
        return container;
    }
}
