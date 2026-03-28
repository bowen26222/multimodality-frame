using Godot;
using System;

namespace MultimodalFramework.Camera
{
    /// <summary>
    /// 相机控制器：支持边缘检测移动
    /// 相机初始位置在原点，鼠标移向屏幕边缘时向对应方向移动
    /// </summary>
    public partial class CameraController : Camera2D
    {
        [ExportGroup("Movement Settings")]
        [Export] public float MoveSpeed { get; set; } = 300f;
        [Export] public float EdgeThreshold { get; set; } = 50f; // 边缘检测阈值（像素）
        [Export] public bool EnableEdgeMovement { get; set; } = true;
        [Export] public bool EnableKeyboardMovement { get; set; } = true;
        [Export] public float KeyboardSpeed { get; set; } = 400f;
        
        [ExportGroup("Boundary Settings")]
        [Export] public bool UseBoundary { get; set; } = false;
        [Export] public Rect2 Boundary { get; set; } = new Rect2(-2000, -2000, 4000, 4000);
        
        [ExportGroup("Zoom Settings")]
        [Export] public float MinZoom { get; set; } = 0.5f;
        [Export] public float MaxZoom { get; set; } = 2.0f;
        [Export] public float ZoomSpeed { get; set; } = 0.1f;
        [Export] public float ZoomSmoothness { get; set; } = 0.1f;
        
        [ExportGroup("Smoothing")]
        [Export] public bool UseSmoothing { get; set; } = true;
        [Export] public float SmoothingDuration { get; set; } = 0.15f;
        
        private Vector2 _targetPosition = Vector2.Zero;
        private Vector2 _velocity = Vector2.Zero;
        private Vector2 _targetZoom = Vector2.One;
        private bool _isDragging = false;
        private Vector2 _dragStart;
        private Vector2 _cameraStart;
        
        public event Action<Vector2> PositionChanged;
        public event Action<Vector2> ZoomChanged;
        
        public override void _Ready()
        {
            // 确保相机被激活
            Enabled = true;
            
            // 初始位置设为原点
            GlobalPosition = Vector2.Zero;
            _targetPosition = Vector2.Zero;
            _targetZoom = Zoom;
            
            // 配置相机平滑
            if (UseSmoothing)
            {
                PositionSmoothingEnabled = true;
                PositionSmoothingSpeed = 1f / SmoothingDuration;
            }
        }
        
        public override void _Process(double delta)
        {
            var dt = (float)delta;
            
            // 处理缩放平滑
            if (Zoom != _targetZoom)
            {
                Zoom = Zoom.Lerp(_targetZoom, ZoomSmoothness);
                ZoomChanged?.Invoke(Zoom);
            }
            
            // 处理键盘移动
            if (EnableKeyboardMovement)
            {
                ProcessKeyboardMovement(dt);
            }
            
            // 处理边缘移动
            if (EnableEdgeMovement && !_isDragging)
            {
                ProcessEdgeMovement(dt);
            }
            
            // 应用边界限制
            if (UseBoundary)
            {
                ApplyBoundary();
            }
        }
        
        public override void _Input(InputEvent @event)
        {
            // 鼠标滚轮缩放
            if (@event is InputEventMouseButton mouseButton)
            {
                if (mouseButton.ButtonIndex == MouseButton.WheelUp)
                {
                    ZoomIn();
                }
                else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
                {
                    ZoomOut();
                }
                else if (mouseButton.ButtonIndex == MouseButton.Middle)
                {
                    if (mouseButton.Pressed)
                    {
                        StartDrag(mouseButton.Position);
                    }
                    else
                    {
                        EndDrag();
                    }
                }
            }
            
            // 鼠标拖拽
            if (@event is InputEventMouseMotion mouseMotion && _isDragging)
            {
                ProcessDrag(mouseMotion.Position);
            }
        }
        
        /// <summary>
        /// 立即移动相机到指定位置
        /// </summary>
        public void MoveTo(Vector2 position, bool smooth = true)
        {
            _targetPosition = position;
            
            if (UseBoundary)
            {
                _targetPosition = ClampToBoundary(_targetPosition);
            }
            
            if (smooth && UseSmoothing)
            {
                GlobalPosition = _targetPosition;
            }
            else
            {
                GlobalPosition = _targetPosition;
                PositionSmoothingEnabled = false;
                CallDeferred(nameof(ReenableSmoothing));
            }
            
            PositionChanged?.Invoke(GlobalPosition);
        }
        
        /// <summary>
        /// 相对移动相机
        /// </summary>
        public void MoveBy(Vector2 offset)
        {
            MoveTo(GlobalPosition + offset);
        }
        
        /// <summary>
        /// 重置相机到原点
        /// </summary>
        public void ResetToOrigin()
        {
            MoveTo(Vector2.Zero);
            SetZoom(1f);
        }
        
        /// <summary>
        /// 放大
        /// </summary>
        public void ZoomIn()
        {
            SetZoom(Zoom.X + ZoomSpeed);
        }
        
        /// <summary>
        /// 缩小
        /// </summary>
        public void ZoomOut()
        {
            SetZoom(Zoom.X - ZoomSpeed);
        }
        
        /// <summary>
        /// 设置缩放级别
        /// </summary>
        public void SetZoom(float zoomLevel)
        {
            zoomLevel = Mathf.Clamp(zoomLevel, MinZoom, MaxZoom);
            _targetZoom = new Vector2(zoomLevel, zoomLevel);
        }
        
        /// <summary>
        /// 设置边界
        /// </summary>
        public void SetBoundary(Rect2 boundary)
        {
            Boundary = boundary;
            UseBoundary = true;
            ApplyBoundary();
        }
        
        /// <summary>
        /// 获取鼠标在世界坐标系中的位置
        /// </summary>
        public Vector2 GetMouseWorldPosition()
        {
            return GetGlobalMousePosition();
        }
        
        private void ProcessEdgeMovement(float delta)
        {
            var mousePos = GetViewport().GetMousePosition();
            var viewportSize = GetViewportRect().Size;
            var moveDirection = Vector2.Zero;
            
            // 左边缘
            if (mousePos.X < EdgeThreshold)
            {
                moveDirection.X -= 1;
            }
            // 右边缘
            else if (mousePos.X > viewportSize.X - EdgeThreshold)
            {
                moveDirection.X += 1;
            }
            
            // 上边缘
            if (mousePos.Y < EdgeThreshold)
            {
                moveDirection.Y -= 1;
            }
            // 下边缘
            else if (mousePos.Y > viewportSize.Y - EdgeThreshold)
            {
                moveDirection.Y += 1;
            }
            
            if (moveDirection != Vector2.Zero)
            {
                // 考虑缩放因子调整速度
                var effectiveSpeed = MoveSpeed / Zoom.X;
                GlobalPosition += moveDirection.Normalized() * effectiveSpeed * delta;
                PositionChanged?.Invoke(GlobalPosition);
            }
        }
        
        private void ProcessKeyboardMovement(float delta)
        {
            var moveDirection = Vector2.Zero;
            
            if (Input.IsActionPressed("ui_left") || Input.IsKeyPressed(Key.A))
            {
                moveDirection.X -= 1;
            }
            if (Input.IsActionPressed("ui_right") || Input.IsKeyPressed(Key.D))
            {
                moveDirection.X += 1;
            }
            if (Input.IsActionPressed("ui_up") || Input.IsKeyPressed(Key.W))
            {
                moveDirection.Y -= 1;
            }
            if (Input.IsActionPressed("ui_down") || Input.IsKeyPressed(Key.S))
            {
                moveDirection.Y += 1;
            }
            
            if (moveDirection != Vector2.Zero)
            {
                var effectiveSpeed = KeyboardSpeed / Zoom.X;
                GlobalPosition += moveDirection.Normalized() * effectiveSpeed * delta;
                PositionChanged?.Invoke(GlobalPosition);
            }
        }
        
        private void StartDrag(Vector2 mousePosition)
        {
            _isDragging = true;
            _dragStart = mousePosition;
            _cameraStart = GlobalPosition;
        }
        
        private void ProcessDrag(Vector2 mousePosition)
        {
            var offset = (_dragStart - mousePosition) / Zoom.X;
            GlobalPosition = _cameraStart + offset;
            PositionChanged?.Invoke(GlobalPosition);
        }
        
        private void EndDrag()
        {
            _isDragging = false;
        }
        
        private void ApplyBoundary()
        {
            GlobalPosition = ClampToBoundary(GlobalPosition);
        }
        
        private Vector2 ClampToBoundary(Vector2 position)
        {
            var halfViewport = GetViewportRect().Size / (2 * Zoom.X);
            
            position.X = Mathf.Clamp(position.X, 
                Boundary.Position.X + halfViewport.X, 
                Boundary.End.X - halfViewport.X);
            position.Y = Mathf.Clamp(position.Y, 
                Boundary.Position.Y + halfViewport.Y, 
                Boundary.End.Y - halfViewport.Y);
            
            return position;
        }
        
        private void ReenableSmoothing()
        {
            PositionSmoothingEnabled = UseSmoothing;
        }
    }
}
