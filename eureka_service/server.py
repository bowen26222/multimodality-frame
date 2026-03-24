"""
Eureka-Audio 本地语音转文字服务

提供 HTTP API 供 Godot 游戏调用，实现语音转文字功能。

使用方法:
    python server.py --port 8765 --model_path ../Eureka-Audio-Instruct

API:
    POST /transcribe
        - 接收音频文件 (multipart/form-data)
        - 返回识别的文字结果
    
    GET /health
        - 健康检查
"""

import argparse
import os
import sys
import tempfile
import asyncio
from pathlib import Path
from typing import Optional

from fastapi import FastAPI, File, UploadFile, HTTPException
from fastapi.responses import JSONResponse
from pydantic import BaseModel
import uvicorn

# 添加 Eureka-Audio 路径
EUREKA_PATH = os.environ.get("EUREKA_AUDIO_PATH", "")
if EUREKA_PATH and os.path.exists(EUREKA_PATH):
    sys.path.insert(0, EUREKA_PATH)

app = FastAPI(title="Eureka-Audio ASR Service")

# 全局模型实例
model = None
model_path = None


class TranscribeResponse(BaseModel):
    """转写响应模型"""
    text: str
    success: bool
    error: Optional[str] = None


class HealthResponse(BaseModel):
    """健康检查响应"""
    status: str
    model_loaded: bool


@app.on_event("startup")
async def load_model():
    """启动时加载模型"""
    global model
    if model_path:
        try:
            from eureka_infer.api import EurekaAudio
            print(f"Loading Eureka-Audio model from {model_path}...")
            model = EurekaAudio(model_path=model_path)
            print("Model loaded successfully!")
        except Exception as e:
            print(f"Failed to load model: {e}")
            model = None


@app.get("/health", response_model=HealthResponse)
async def health_check():
    """健康检查接口"""
    return HealthResponse(
        status="ok" if model is not None else "model_not_loaded",
        model_loaded=model is not None
    )


@app.post("/transcribe", response_model=TranscribeResponse)
async def transcribe_audio(file: UploadFile = File(...)):
    """
    语音转文字接口
    
    接收音频文件，返回识别的文字结果。
    支持的音频格式: WAV, MP3, FLAC, OGG 等
    """
    if model is None:
        return TranscribeResponse(
            text="",
            success=False,
            error="Model not loaded"
        )
    
    # 保存上传的音频到临时文件
    suffix = Path(file.filename).suffix or ".wav"
    try:
        with tempfile.NamedTemporaryFile(delete=False, suffix=suffix) as tmp:
            content = await file.read()
            tmp.write(content)
            tmp_path = tmp.name
        
        # 调用 Eureka-Audio 进行 ASR
        messages = [
            {
                "role": "system",
                "content": [
                    {
                        "type": "text",
                        "text": "You are an advanced ASR (Automatic Speech Recognition) AI assistant."
                    }
                ]
            },
            {
                "role": "user",
                "content": [
                    {"type": "audio_url", "audio_url": {"url": tmp_path}}
                ]
            }
        ]
        
        result = model.generate(
            messages,
            temperature=0.0,
            do_sample=False,
            max_new_tokens=512
        )
        
        return TranscribeResponse(
            text=result.strip(),
            success=True
        )
        
    except Exception as e:
        return TranscribeResponse(
            text="",
            success=False,
            error=str(e)
        )
    finally:
        # 清理临时文件
        if 'tmp_path' in locals() and os.path.exists(tmp_path):
            os.unlink(tmp_path)


@app.post("/transcribe_base64", response_model=TranscribeResponse)
async def transcribe_audio_base64(data: dict):
    """
    Base64 编码音频转文字接口
    
    接收 Base64 编码的音频数据，返回识别的文字结果。
    请求体: {"audio_base64": "...", "format": "wav"}
    """
    if model is None:
        return TranscribeResponse(
            text="",
            success=False,
            error="Model not loaded"
        )
    
    import base64
    
    audio_base64 = data.get("audio_base64", "")
    audio_format = data.get("format", "wav")
    
    if not audio_base64:
        return TranscribeResponse(
            text="",
            success=False,
            error="No audio data provided"
        )
    
    try:
        # 解码 Base64 数据
        audio_data = base64.b64decode(audio_base64)
        
        # 保存到临时文件
        with tempfile.NamedTemporaryFile(delete=False, suffix=f".{audio_format}") as tmp:
            tmp.write(audio_data)
            tmp_path = tmp.name
        
        # 调用 Eureka-Audio 进行 ASR
        messages = [
            {
                "role": "system",
                "content": [
                    {
                        "type": "text",
                        "text": "You are an advanced ASR (Automatic Speech Recognition) AI assistant."
                    }
                ]
            },
            {
                "role": "user",
                "content": [
                    {"type": "audio_url", "audio_url": {"url": tmp_path}}
                ]
            }
        ]
        
        result = model.generate(
            messages,
            temperature=0.0,
            do_sample=False,
            max_new_tokens=512
        )
        
        return TranscribeResponse(
            text=result.strip(),
            success=True
        )
        
    except Exception as e:
        return TranscribeResponse(
            text="",
            success=False,
            error=str(e)
        )
    finally:
        # 清理临时文件
        if 'tmp_path' in locals() and os.path.exists(tmp_path):
            os.unlink(tmp_path)


def main():
    global model_path
    
    parser = argparse.ArgumentParser(description="Eureka-Audio ASR Service")
    parser.add_argument(
        "--port",
        type=int,
        default=8765,
        help="服务端口 (默认: 8765)"
    )
    parser.add_argument(
        "--host",
        type=str,
        default="127.0.0.1",
        help="服务主机 (默认: 127.0.0.1)"
    )
    parser.add_argument(
        "--model_path",
        type=str,
        default="Eureka-Audio-Instruct",
        help="模型路径 (HuggingFace ID 或本地路径)"
    )
    args = parser.parse_args()
    
    model_path = args.model_path
    
    print(f"Starting Eureka-Audio ASR Service on {args.host}:{args.port}")
    print(f"Model path: {model_path}")
    
    uvicorn.run(
        app,
        host=args.host,
        port=args.port,
        log_level="info"
    )


if __name__ == "__main__":
    main()
