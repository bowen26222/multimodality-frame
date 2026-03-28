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
        
        return 
    }
}
