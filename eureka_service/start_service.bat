@echo off
REM Eureka-Audio 服务启动脚本 (Windows)

SETLOCAL

REM 设置 Eureka-Audio 路径
SET EUREKA_AUDIO_PATH=%~dp0..\temp_eureka

REM 设置模型路径 (可以是 HuggingFace ID 或本地路径)
SET MODEL_PATH=%~dp0..\Eureka-Audio-Instruct

REM 如果模型不存在，使用 HuggingFace ID 自动下载
IF NOT EXIST "%MODEL_PATH%" (
    echo Model not found at %MODEL_PATH%
    echo Will download from HuggingFace: cslys1999/Eureka-Audio-Instruct
    SET MODEL_PATH=cslys1999/Eureka-Audio-Instruct
)

REM 设置环境变量
SET EUREKA_AUDIO_PATH=%EUREKA_AUDIO_PATH%

REM 启动服务
echo Starting Eureka-Audio ASR Service...
echo Model: %MODEL_PATH%
echo Port: 8765

python "%~dp0server.py" --port 8765 --model_path "%MODEL_PATH%"

ENDLOCAL
