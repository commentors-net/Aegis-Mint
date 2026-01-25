from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
import logging
import logging.config
import os

from app.api.routers import (
    admin, 
    auth, 
    desktop, 
    governance, 
    admin_ca, 
    share_recovery, 
    share_operations, 
    token_deployment, 
    downloads, 
    mint_approval,
    share_files,
    admin_share_assignments,
    user_shares,
    token_share_users,
    token_user_auth
)
from app.core.config import get_settings
from app.db.base import Base
from app.db.session import engine
from app.services.auth_service import ensure_super_admin_exists
from app.db.init_db import seed_data
from app.api.routers import debug

import pyotp

# Configure logging
if os.path.exists("logging.conf"):
    logging.config.fileConfig("logging.conf", disable_existing_loggers=False)
else:
    # Fallback to basic config
    logging.basicConfig(
        level=logging.INFO,
        format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
    )

logger = logging.getLogger(__name__)

settings = get_settings()
logger.info(f"Starting {settings.app_name} with environment: {os.getenv('TOKENCONTROL_DATABASE_URL', 'Not set')[:30]}...")

docs_url = "/docs" if settings.enable_docs else None
redoc_url = "/redoc" if settings.enable_docs else None
openapi_url = "/openapi.json" if settings.enable_docs else None

app = FastAPI(
    title=settings.app_name, 
    version="0.1.0", 
    docs_url=docs_url, 
    redoc_url=redoc_url, 
    openapi_url=openapi_url,
    root_path=settings.root_path  # For reverse proxy with path prefix
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=settings.cors_origins,
    allow_methods=["*"],
    allow_headers=["*"],
    allow_credentials=True,
)

app.include_router(auth.router)
app.include_router(desktop.router)
app.include_router(governance.router)
app.include_router(admin.router)
app.include_router(admin_ca.router, tags=["admin-ca"])
app.include_router(share_recovery.router)
app.include_router(share_operations.router)
app.include_router(token_deployment.router)
app.include_router(downloads.router)
app.include_router(mint_approval.router)
app.include_router(share_files.router)
app.include_router(admin_share_assignments.router)
app.include_router(user_shares.router)
app.include_router(token_share_users.router)
app.include_router(token_user_auth.router)
if settings.enable_docs:
    app.include_router(debug.router)


@app.on_event("startup")
def on_startup():
    logger.info("Application startup initiated")
    Base.metadata.create_all(bind=engine)
    # Seed test data for local/dev
    from app.db.session import SessionLocal

    db = SessionLocal()
    try:
        ensure_super_admin_exists(db)
        seed_data(db)
        logger.info("Database initialization completed")
    except Exception as e:
        logger.error(f"Error during startup: {e}", exc_info=True)
        raise
    finally:
        db.close()
