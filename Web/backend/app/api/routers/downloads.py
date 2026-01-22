import re
from datetime import datetime
from typing import List

from fastapi import APIRouter, Depends, HTTPException, status
from pydantic import BaseModel, HttpUrl
from sqlalchemy.orm import Session

from app.api.deps import get_db, require_role
from app.models import DownloadLink, User, UserRole

router = APIRouter(prefix="/api/admin/downloads", tags=["downloads"])


class AddLinkRequest(BaseModel):
    url: str


class FileInfo(BaseModel):
    filename: str
    url: str
    created_at: str


def extract_filename_from_url(url: str) -> str:
    """Extract filename from GitHub release URL"""
    # Extract filename from URL like: https://github.com/user/repo/releases/download/tag/filename.exe
    match = re.search(r'/([^/]+\.exe)$', url)
    if match:
        return match.group(1)
    raise ValueError("Could not extract filename from URL")


def validate_github_url(url: str) -> bool:
    """Validate that URL is a GitHub release URL pointing to .exe file"""
    pattern = r'^https://github\.com/[^/]+/[^/]+/releases/download/[^/]+/[^/]+\.exe$'
    return bool(re.match(pattern, url))


@router.get("", response_model=List[FileInfo])
def list_files(
    _: User = Depends(require_role(UserRole.SUPER_ADMIN)),
    db: Session = Depends(get_db),
):
    """List all download links"""
    links = db.query(DownloadLink).order_by(DownloadLink.created_at.desc()).all()
    return [
        FileInfo(
            filename=link.filename,
            url=link.url,
            created_at=link.created_at.isoformat(),
        )
        for link in links
    ]


@router.post("/add")
def add_link(
    request: AddLinkRequest,
    user: User = Depends(require_role(UserRole.SUPER_ADMIN)),
    db: Session = Depends(get_db),
):
    """Add a new GitHub release download link"""
    # Validate URL format
    if not validate_github_url(request.url):
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="Invalid URL. Must be a GitHub release URL pointing to an .exe file.",
        )
    
    # Extract filename
    try:
        filename = extract_filename_from_url(request.url)
    except ValueError as e:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=str(e),
        )
    
    # Check if URL already exists
    existing = db.query(DownloadLink).filter(DownloadLink.url == request.url).first()
    if existing:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail="This URL has already been added",
        )
    
    # Check if filename already exists
    existing_filename = db.query(DownloadLink).filter(DownloadLink.filename == filename).first()
    if existing_filename:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=f"A link with filename '{filename}' already exists",
        )
    
    # Create new link
    link = DownloadLink(
        url=request.url,
        filename=filename,
        created_by=user.email,
    )
    db.add(link)
    db.commit()
    db.refresh(link)
    
    return {"filename": filename, "message": "Download link added successfully"}


@router.get("/url/{filename}")
def get_download_url(
    filename: str,
    _: User = Depends(require_role(UserRole.SUPER_ADMIN)),
    db: Session = Depends(get_db),
):
    """Get the GitHub URL for a specific filename"""
    link = db.query(DownloadLink).filter(DownloadLink.filename == filename).first()
    if not link:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="File not found",
        )
    
    return {"url": link.url}


@router.delete("/{filename}")
def delete_link(
    filename: str,
    _: User = Depends(require_role(UserRole.SUPER_ADMIN)),
    db: Session = Depends(get_db),
):
    """Delete a download link"""
    link = db.query(DownloadLink).filter(DownloadLink.filename == filename).first()
    if not link:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail="File not found",
        )
    
    db.delete(link)
    db.commit()
    
    return {"filename": filename, "message": "Download link deleted successfully"}
