#!/bin/bash
# Start script for AegisMint Governance API in production

# Set working directory
cd "$(dirname "$0")"

# Create logs directory if it doesn't exist
mkdir -p /home/apkserve/logs/governance

# Activate virtual environment if exists
if [ -d "venv" ]; then
    source venv/bin/activate
fi

# Export environment variables
export PYTHONPATH="${PYTHONPATH}:$(pwd)"

# Start Gunicorn with configuration
gunicorn main:app \
    --config gunicorn.conf.py \
    --log-config logging.conf \
    --capture-output \
    --enable-stdio-inheritance

# Note: To run in background with systemd, create a service file instead
