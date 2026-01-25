"""
ClientWeb Backend - Middleware/Proxy for Token Share Users

This application acts as a secure proxy between token share users
and the main Aegis Mint backend. It provides:
- Token user authentication forwarding
- Session management
- Request proxying to main backend
- Additional security layer
- Rate limiting (future)
"""
import logging
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from app.api import auth, shares
from app.core.config import settings

# Configure logging
logging.basicConfig(
    level=logging.INFO if not settings.debug else logging.DEBUG,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)

logger = logging.getLogger(__name__)

app = FastAPI(
    title="Aegis Mint - Share Portal",
    description="Token share user portal for accessing assigned shares",
    version="1.0.0",
    docs_url="/docs" if settings.debug else None,
    redoc_url="/redoc" if settings.debug else None,
)

# CORS middleware
app.add_middleware(
    CORSMiddleware,
    allow_origins=settings.cors_origins,
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Include routers
app.include_router(auth.router, prefix="/api/auth", tags=["auth"])
app.include_router(shares.router, prefix="/api/shares", tags=["shares"])


@app.get("/")
def root():
    """Health check endpoint."""
    return {
        "status": "ok",
        "service": "Aegis Mint Share Portal",
        "version": "1.0.0"
    }


@app.get("/health")
def health():
    """Health check for load balancers."""
    return {"status": "healthy"}


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(
        "main:app",
        host=settings.host,
        port=settings.port,
        reload=settings.debug,
    )
