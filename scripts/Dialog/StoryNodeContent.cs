using Godot;
using MultimodalFramework;
using DialogueManagerRuntime;
using System.Collections.Generic;
using Godot.Collections;

namespace TreeStory;

/// <summary>
/// 故事节点内容 - 作为 Resource 可在编辑器中编辑
/// </summary>
[GlobalClass]
public partial class StoryNodeContent : Resource
{
    [Export] public string ForwardNodeId;
    [Export] public string Content;
}