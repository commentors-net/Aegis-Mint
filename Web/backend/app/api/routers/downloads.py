import os
from pathlib import Path
from typing import List

from fastapi import APIRouter, Depends, File, HTTPException, UploadFile, status
from fastapi.responses import FileResponse
from pydantic import BaseModel

from app.api.deps import require_role
from app.models import User, UserRole

router = APIRouter(prefix="/api/admin/downloads", tags=["downloads"])

# Configure upload directory
UPLOAD_DIR = Path("uploads/installers")
UPLOAD_DIR.mkdir(parents=True, exist_ok=True)


class FileInfo(BaseModel):
    filename: str
    size: int
    uploaded_at: str


@router.get("", response_model=List[FileInfo])
def list_files(_: User = Depends(require_role(UserRole.SUPER_ADMIN))):
    """List all uploaded .exe files"""
    files = []
    if UPLOAD_DIR.exists():
        for file_path in UPLOAD_DIR.glob("*.exe"):
            stat = file_path.stat()
            files.append(
                FileInfo(
                    filename=file_path.name,
                    size=stat.st_size,
                    uploaded_at=stat.st_mtime.__str__(),
                )
            )
    # Sort by upload time, newest first
    files.sort(key=lambda x: x.uploaded_at, reverse=True)
    return files


@router.post("/upload")
async def upload_file(
    file: UploadFile = File(...),
    _: User = Depends(require_role(UserRole.SUPER_ADMIN)),
):
    """Upload a .exe file"""
    # Validate file extension
    if not file.filename or not file.filename.lower().endswith(".exe"):
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="Only .exe files are allowed",
        )

    # Save file
    file_path = UPLOAD_DIR / file.filename
    try:
        with open(file_path, "wb") as f:
            content = await file.read()
            f.write(content)
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=f"Failed to save file: {str(e)}",
        )

    return {"filename": file.filename, "message": "File uploaded successfully"}


@router.get("/download/{filename}")
def download_file(
    filename: str,
    _: User = Depends(require_role(UserRole.SUPER_ADMIN)),
):
    """Download a specific .exe file"""
    # Validate filename to prevent directory traversal
    if ".." in filename or "/" in filename or "\\" in filename:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="Invalid filename",
        )

    file_path = UPLOAD_DIR / filename
    if not file_path.exists() or not file_path.is_file():
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="File not found",
        )

    return FileResponse(
        path=file_path,
        filename=filename,
        media_type="application/octet-stream",
    )


@router.delete("/{filename}")
def delete_file(
    filename: str,
    _: User = Depends(require_role(UserRole.SUPER_ADMIN)),
):
    """Delete a specific .exe file"""
    # Validate filename to prevent directory traversal
    if ".." in filename or "/" in filename or "\\" in filename:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="Invalid filename",
        )

    file_path = UPLOAD_DIR / filename
    if not file_path.exists() or not file_path.is_file():
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="File not found",
        )

    try:
        os.remove(file_path)
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=f"Failed to delete file: {str(e)}",
        )

    return {"filename": filename, "message": "File deleted successfully"}
