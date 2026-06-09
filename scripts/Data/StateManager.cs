using Godot;
using System;
using System.Collections.Generic;

public partial class StateManager : Node
{
    public enum StateType
    {
        String,
        Bool,
        Number,
    }

    public static StateManager Instance { get; private set; }

    public Dictionary<string, StateType> StateData { get; private set; } = new();
    public Dictionary<string, string> StringData { get; private set; } = new();
    public Dictionary<string, bool> BoolData { get; private set; } = new();
    public Dictionary<string, float> NumberData { get; private set; } = new();

    public override void _Ready()
    {
        Instance = this;
    }

    /// <summary>
    /// 泛型设置状态。仅支持 string / bool / float 三种类型，其它类型抛异常。
    /// 使用：StateManager.Instance.SetState("hp", 100f);
    /// </summary>
    public void SetState<T>(string key, T value)
    {
        switch (value)
        {
            case string s:
                StateData[key] = StateType.String;
                StringData[key] = s;
                break;
            case bool b:
                StateData[key] = StateType.Bool;
                BoolData[key] = b;
                break;
            case float f:
                StateData[key] = StateType.Number;
                NumberData[key] = f;
                break;
            case int i:
                // 方便 SetState("hp", 100) 这种字面量写法
                StateData[key] = StateType.Number;
                NumberData[key] = i;
                break;
            case double d:
                StateData[key] = StateType.Number;
                NumberData[key] = (float)d;
                break;
            default:
                throw new ArgumentException(
                    $"Unsupported state type: {typeof(T).Name}. Only string/bool/float are supported.");
        }
    }

    /// <summary>
    /// 泛型获取状态。key 不存在或类型不匹配时抛异常。
    /// 使用：float hp = StateManager.Instance.GetState&lt;float&gt;("hp");
    /// </summary>
    public T GetState<T>(string key)
    {
        if (!StateData.TryGetValue(key, out var type))
            throw new KeyNotFoundException($"State '{key}' does not exist.");

        object value = type switch
        {
            StateType.String => StringData[key],
            StateType.Bool => BoolData[key],
            StateType.Number => NumberData[key],
            _ => throw new InvalidOperationException($"Unknown state type for '{key}'."),
        };

        if (value is T typed)
            return typed;

        throw new InvalidCastException(
            $"State '{key}' is of type {type}, cannot cast to {typeof(T).Name}.");
    }

    /// <summary>
    /// 带默认值的泛型获取，key 不存在或类型不匹配时返回默认值，不抛异常。
    /// </summary>
    public T GetStateOr<T>(string key, T defaultValue = default)
    {
        if (!StateData.TryGetValue(key, out var type)) return defaultValue;

        object value = type switch
        {
            StateType.String => StringData[key],
            StateType.Bool => BoolData[key],
            StateType.Number => NumberData[key],
            _ => null,
        };

        return value is T typed ? typed : defaultValue;
    }

    public bool HasState(string key) => StateData.ContainsKey(key);

    public StateType GetStateType(string key)
    {
        if (!StateData.TryGetValue(key, out var type))
            throw new KeyNotFoundException($"State '{key}' does not exist.");
        return type;
    }

    public bool RemoveState(string key)
    {
        if (!StateData.TryGetValue(key, out var type)) return false;
        StateData.Remove(key);
        switch (type)
        {
            case StateType.String: StringData.Remove(key); break;
            case StateType.Bool: BoolData.Remove(key); break;
            case StateType.Number: NumberData.Remove(key); break;
        }
        return true;
    }
}
