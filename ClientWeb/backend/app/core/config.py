"""Configuration settings for ClientWeb backend."""
from pydantic_settings import BaseSettings
from typing import List


class Settings(BaseSettings):
    """Application settings."""
    
    # App config
    app_name: str = "Aegis Mint Share Portal"
    debug: bool = True
    host: str = "127.0.0.1"
    port: int = 8001
    
    # CORS
    cors_origins: List[str] = ["http://localhost:5174", "http://127.0.0.1:5174"]
    
    # Backend API URL (main Aegis Mint backend)
    backend_api_url: str = "http://127.0.0.1:8000"
    
    # JWT Secret (should match main backend for token verification)
    jwt_secret_key: str = "your-secret-key-here-change-in-production"
    jwt_algorithm: str = "HS256"
    
    # Session
    session_timeout_minutes: int = 60
    
    class Config:
        env_file = ".env"
        case_sensitive = False


settings = Settings()
