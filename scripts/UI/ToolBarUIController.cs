using Godot;
using System;

public partial class ToolBarUIController : Node
{
    [Export] public Button ClickButton;
    [Export] public Button MoveButton;
    [Export] public Button ConnectButton;

    public override void _Ready()
    {
        ClickButton = GetNode<Button>("ToolList/ClickButton");
        MoveButton = GetNode<Button>("ToolList/MoveButton");
        ConnectButton = GetNode<Button>("ToolList/ConnectButton");
    }
}
