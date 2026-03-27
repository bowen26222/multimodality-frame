using Godot;
using MultimodalFramework;
using DialogueManagerRuntime;

namespace TreeStory;
/// <summary>
/// 对话和AI接入中继
/// </summary>
[GlobalClass]
public partial class StoryManager : Node
{   
    public static StoryManager Instance {get; private set;}
    public override void _Ready()
    {
        Instance = this;
    }
    public void SetStorys(string path)
    {
        GD.Print(path);
    }
}
