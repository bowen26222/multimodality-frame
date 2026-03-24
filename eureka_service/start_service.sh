#!/bin/bash
# Eureka-Audio 服务启动脚本 (Linux/macOS)

# 设置 Eureka-Audio 路径
export EUREKA_AUDIO_PATH="$(dirname "$0")/../temp_eureka"

# 设置模型路径
MODEL_PATH="$(dirname "$0")/../Eureka-Audio-Instruct"

# 如果模型不存在，使用 HuggingFace ID 自动下载
if [ ! -d "$MODEL_PATH" ]; then
    echo "Model not found at $MODEL_PATH"
    echo "Will download from HuggingFace: cslys1999/Eureka-Audio-Instruct"
    MODEL_PATH="cslys1999/Eureka-Audio-Instruct"
fi

# 启动服务
echo "Starting Eureka-Audio ASR Service..."
echo "Model: $MODEL_PATH"
echo "Port: 8765"

python "$(dirname "$0")/server.py" --port 8765 --model_path "$MODEL_PATH"
