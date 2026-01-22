"""
Gunicorn configuration for AegisMint Governance API
"""
import multiprocessing
import os

# Server socket
bind = "127.0.0.1:8000"
backlog = 2048

# Worker processes
workers = multiprocessing.cpu_count() * 2 + 1
worker_class = "uvicorn.workers.UvicornWorker"
worker_connections = 1000
timeout = 120
keepalive = 5

# Logging
accesslog = "/home/apkserve/logs/governance/access.log"
errorlog = "/home/apkserve/logs/governance/access.log"
loglevel = "info"
access_log_format = '%(h)s %(l)s %(u)s %(t)s "%(r)s" %(s)s %(b)s "%(f)s" "%(a)s" %(D)s'

# Process naming
proc_name = "aegismint-governance"

# Server mechanics
daemon = False
pidfile = "/home/apkserve/tmp/governance.pid"
user = None
group = None
tmp_upload_dir = None

# Enable request logging
capture_output = True
enable_stdio_inheritance = True

# Preload application for better performance
preload_app = True

# Graceful timeout
graceful_timeout = 30

# For debugging (set to False in production)
reload = False
reload_engine = "auto"
