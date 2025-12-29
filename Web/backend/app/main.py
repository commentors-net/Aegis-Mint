from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from app.api.routers import admin, auth, desktop, governance, admin_ca
from app.core.config import get_settings
from app.db.base import Base
from app.db.session import engine
from app.services.auth_service import ensure_super_admin_exists
from app.db.init_db import seed_data
from app.api.routers import debug

import pyotp

settings = get_settings()

docs_url = "/docs" if settings.enable_docs else None
redoc_url = "/redoc" if settings.enable_docs else None
openapi_url = "/openapi.json" if settings.enable_docs else None

app = FastAPI(title=settings.app_name, version="0.1.0", docs_url=docs_url, redoc_url=redoc_url, openapi_url=openapi_url)

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
app.include_router(admin_ca.router, prefix="/api/admin/ca", tags=["admin-ca"])
if settings.enable_docs:
    app.include_router(debug.router)


@app.on_event("startup")
def on_startup():
    Base.metadata.create_all(bind=engine)
    # Seed test data for local/dev
    from app.db.session import SessionLocal

    db = SessionLocal()
    try:
        ensure_super_admin_exists(db)
        seed_data(db)
    finally:
        db.close()
