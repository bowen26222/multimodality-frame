using Godot;
using MultimodalFramework;
using DialogueManagerRuntime;
using System.Collections.Generic;
using Godot.Collections;

namespace TreeStory;
/// <summary>
/// 故事节点内容
/// </summary>
[GlobalClass]
public partial class StoryNode : Resource
{
    [Export]
    public Array<string> Contents;
}
