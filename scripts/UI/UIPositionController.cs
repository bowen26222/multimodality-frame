using Godot;
using System;
using System.Collections.Generic;

namespace MultimodalFramework.UI
{
    /// <summary>
    /// UI 坐标模式
    /// </summary>
    public enum UICoordinateMode
    {
        /// <summary>UI 固定在屏幕上（使用 CanvasLayer）</summary>
        Screen,
        /// <summary>UI 放置在世界坐标系中，随相机移动</summary>
        World
    }

    /// <summary>
    /// UI位置控制器：根据外部数据生成UI并放置到指定坐标
    /// 使用委托模式，支持任意数据类型
    /// </summary>
    public partial class UIPositionController : Node
    {
        [Export] public float DefaultTransitionDuration { get; set; } = 0.3f;
        [Export] public UICoordinateMode CoordinateMode { get; set; } = UICoordinateMode.World;
        
        private CanvasLayer _canvasLayer;
        protected Control _rootContainer;
        private readonly Dictionary<string, Control> _uiElements = new();
        private readonly Dictionary<string, Tween> _activeTweens = new();
        
        public override void _Ready()
        {
            if (CoordinateMode == UICoordinateMode.Screen)
            {
                _canvasLayer = new CanvasLayer { Layer = 10 };
                AddChild(_canvasLayer);
                
                _rootContainer = new Control();
                _rootContainer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
                _rootContainer.Name = "UIRoot";
                _canvasLayer.AddChild(_rootContainer);
            }
            else
            {
                _rootContainer = new Control { Name = "UIRoot" };
                AddChild(_rootContainer);
            }
        }
        
        /// <summary>
        /// 根据数据创建UI元素
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="data">数据对象</param>
        /// <param name="elementId">元素唯一ID</param>
        /// <param name="createFunc">创建函数：接收数据，返回 Control</param>
        public Control CreateElement<T>(T data, string elementId, Func<T, Control> createFunc)
        {
            if (string.IsNullOrEmpty(elementId))
            {
                GD.PrintErr("elementId must be valid");
                return null;
            }
            
            if (_uiElements.ContainsKey(elementId))
            {
                RemoveElement(elementId);
            }
            
            var element = createFunc(data);
            if (element == null) return null;
            
            element.Name = elementId;
            _rootContainer.AddChild(element);
            _uiElements[elementId] = element;
            
            return element;
        }
        
        /// <summary>
        /// 批量创建UI元素
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="dataList">数据列表</param>
        /// <param name="getIdFunc">获取元素ID的函数</param>
        /// <param name="createFunc">创建函数</param>
        public void CreateElements<T>(IEnumerable<T> dataList, Func<T, string> getIdFunc, Func<T, Control> createFunc)
        {
            foreach (var data in dataList)
            {
                var id = getIdFunc(data);
                CreateElement(data, id, createFunc);
            }
        }
        
        /// <summary>
        /// 移动UI元素到新位置（带动画）
        /// </summary>
        public void MoveElementTo(string elementId, Vector2 newPosition, float? duration = null)
        {
            if (!_uiElements.TryGetValue(elementId, out var element))
            {
                GD.PrintErr($"UI element '{elementId}' not found");
                return;
            }
            
            duration ??= DefaultTransitionDuration;
            
            if (_activeTweens.TryGetValue(elementId, out var oldTween))
            {
                oldTween.Kill();
            }
            
            if (duration > 0)
            {
                var tween = CreateTween();
                tween.TweenProperty(element, "position", newPosition, duration.Value);
                _activeTweens[elementId] = tween;
            }
            else
            {
                element.Position = newPosition;
            }
        }
        
        /// <summary>
        /// 移除UI元素
        /// </summary>
        public void RemoveElement(string elementId)
        {
            if (_uiElements.TryGetValue(elementId, out var element))
            {
                element.QueueFree();
                _uiElements.Remove(elementId);
                _activeTweens.Remove(elementId);
            }
        }
        
        /// <summary>
        /// 清除所有UI元素
        /// </summary>
        public void ClearAll()
        {
            foreach (var element in _uiElements.Values)
            {
                element.QueueFree();
            }
            _uiElements.Clear();
            _activeTweens.Clear();
        }
        
        /// <summary>
        /// 获取UI元素
        /// </summary>
        public Control GetElement(string elementId)
        {
            return _uiElements.TryGetValue(elementId, out var element) ? element : null;
        }
        
        /// <summary>
        /// 获取UI元素（强类型）
        /// </summary>
        public T GetElement<T>(string elementId) where T : Control
        {
            return GetElement(elementId) as T;
        }
        
        /// <summary>
        /// 获取所有UI元素ID
        /// </summary>
        public IEnumerable<string> GetAllElementIds() => _uiElements.Keys;
        
        /// <summary>
        /// 检查元素是否存在
        /// </summary>
        public bool HasElement(string elementId) => _uiElements.ContainsKey(elementId);
    }
}
