from datetime import datetime

from sqlalchemy import Column, DateTime, Integer, String

from app.db.base import Base


class DownloadLink(Base):
    __tablename__ = "download_links"

    id = Column(Integer, primary_key=True, index=True)
    url = Column(String, nullable=False, unique=True)
    filename = Column(String, nullable=False)
    created_at = Column(DateTime, default=datetime.utcnow, nullable=False)
    created_by = Column(String, nullable=False)
