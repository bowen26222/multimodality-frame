using Godot;
using MultimodalFramework;
using DialogueManagerRuntime;
using System.Collections.Generic;
using Godot.Collections;

namespace MultimodalFramework.Dialog;

/// <summary>
/// 故事节点内容 - 作为 Resource 可在编辑器中编辑
/// </summary>


[GlobalClass]
public partial class StoryNode : Resource
{
    [Export] public string NodeTitle;
    [Export] public string NodeSummary;
    [Export] public string NodeId;
    [Export] public string DialogPath;
    [Export] public Array<string> Conditions;
    [Export] public Array<StoryNodeContent> Choices;
}
