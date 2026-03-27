using Godot;
using System;
using System.IO;

namespace MultimodalFramework
{
    /// <summary>
    /// 语音录制器，使用Godot的AudioEffectRecord
    /// </summary>
    public partial class VoiceRecorder : Node
    {
        [Signal]
        public delegate void RecordingCompletedEventHandler(string audioBase64);
        
        [Signal]
        public delegate void RecordingFailedEventHandler(string error);
        
        [Export]
        public float RecordingMaxDuration { get; set; } = 30f; // 最大录制时长（秒）
        
        private AudioEffectRecord _recordEffect;
        private AudioStreamPlayer _micPlayer;
        private bool _isRecording = false;
        private float _recordingTime = 0f;
        private string _tempFilePath;
        private int _recordBusIndex = -1;
        
        public override void _Ready()
        {
            // 创建临时文件路径 - 保存到项目目录方便调试
            _tempFilePath = "user://voice_recording.wav";
            
            // 获取或创建录音总线
            _recordBusIndex = AudioServer.GetBusIndex("Record");
            if (_recordBusIndex == -1)
            {
                // 如果不存在Record总线，创建一个
                AudioServer.AddBus();
                _recordBusIndex = AudioServer.BusCount - 1;
                AudioServer.SetBusName(_recordBusIndex, "Record");
            }
            
            // 确保录音总线静音关闭，音量正常
            AudioServer.SetBusMute(_recordBusIndex, false);
            AudioServer.SetBusVolumeDb(_recordBusIndex, 0f);
            
            // 添加录音效果
            _recordEffect = new AudioEffectRecord();
            AudioServer.AddBusEffect(_recordBusIndex, _recordEffect, 0);
            
            // 创建麦克风播放器，将麦克风输入路由到录音总线
            _micPlayer = new AudioStreamPlayer();
            _micPlayer.Stream = new AudioStreamMicrophone();
            _micPlayer.Bus = "Record";
            _micPlayer.VolumeDb = 0f;  // 确保音量正常
            AddChild(_micPlayer);
            
            // 启动麦克风（必须播放才能捕获音频）
            _micPlayer.Play();
            
            // 调试信息
            GD.Print($"VoiceRecorder initialized:");
            GD.Print($"  - Record bus index: {_recordBusIndex}");
            GD.Print($"  - Mic player playing: {_micPlayer.Playing}");
            GD.Print($"  - Input device: {AudioServer.GetInputDevice()}");
            GD.Print($"  - Available input devices: {string.Join(", ", AudioServer.GetInputDeviceList())}");
        }
        
        /// <summary>
        /// 开始录制
        /// </summary>
        public bool StartRecording()
        {
            if (_isRecording)
            {
                GD.PrintErr("Already recording");
                return false;
            }
            
            if (_recordEffect == null)
            {
                EmitSignal(SignalName.RecordingFailed, "Record effect not initialized");
                return false;
            }
            
            _recordEffect.SetRecordingActive(true);
            _isRecording = true;
            _recordingTime = 0f;
            
            GD.Print("Voice recording started");
            return true;
        }
        
        /// <summary>
        /// 停止录制并返回音频数据
        /// </summary>
        public void StopRecording()
        {
            if (!_isRecording || _recordEffect == null)
            {
                return;
            }
            
            _recordEffect.SetRecordingActive(false);
            _isRecording = false;
            
            var recording = _recordEffect.GetRecording();
            if (recording == null)
            {
                EmitSignal(SignalName.RecordingFailed, "No recording data available");
                return;
            }
            
            // 保存为WAV文件
            recording.SaveToWav(_tempFilePath);
            
            // 转换 user:// 路径为全局路径
            string globalPath = ProjectSettings.GlobalizePath(_tempFilePath);
            
            // 读取并转换为Base64
            try
            {
                var audioData = File.ReadAllBytes(globalPath);
                var base64 = Convert.ToBase64String(audioData);
                
                GD.Print($"Voice recording completed, size: {audioData.Length} bytes");
                
                EmitSignal(SignalName.RecordingCompleted, base64);
            }
            catch (Exception ex)
            {
                EmitSignal(SignalName.RecordingFailed, $"Failed to read audio file: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 取消录制
        /// </summary>
        public void CancelRecording()
        {
            if (!_isRecording || _recordEffect == null)
            {
                return;
            }
            
            _recordEffect.SetRecordingActive(false);
            _isRecording = false;
            _recordingTime = 0f;
            
            GD.Print("Voice recording cancelled");
        }
        
        public override void _Process(double delta)
        {
            if (_isRecording)
            {
                _recordingTime += (float)delta;
                
                // 检查是否超过最大时长
                if (_recordingTime >= RecordingMaxDuration)
                {
                    GD.Print("Recording max duration reached, stopping...");
                    StopRecording();
                }
            }
        }
        
        public override void _ExitTree()
        {
            if (_isRecording)
            {
                CancelRecording();
            }
            
            // 清理临时文件
            if (File.Exists(_tempFilePath))
            {
                try
                {
                    File.Delete(_tempFilePath);
                }
                catch { }
            }
        }
        
        /// <summary>
        /// 是否正在录制
        /// </summary>
        public bool IsRecording => _isRecording;
    }
}
