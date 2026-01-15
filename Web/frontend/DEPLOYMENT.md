# Deployment Guide - Cache-Busting Strategy

This guide explains the cache-busting strategy implemented for the Aegis Governance frontend.

## What's Implemented

### 1. **Content-Hash Based Filenames**
- All JS, CSS, and asset files are generated with content hashes (e.g., `main.a3f4b2c1.js`)
- When you change code, the hash changes, forcing browsers to download new files
- Configured in `vite.config.ts`

### 2. **Version Tracking**
- `public/version.json` tracks version, build date, and build number
- Automatically updated on each build via `build-version.js`
- Used by the app to detect when a new version is deployed

### 3. **Automatic Update Notifications**
- `VersionCheck` component checks for new versions every 5 minutes
- Shows a banner prompting users to refresh when a new version is detected
- Clears all caches and reloads the page when user clicks "Refresh Now"

### 4. **Apache Cache Headers**
- `.htaccess.template` configures proper cache headers:
  - `index.html` and `version.json`: No caching (always fresh)
  - Hashed assets (JS/CSS): Cache for 1 year (safe because hash changes with content)
  - SPA routing support maintained

## How to Deploy

### Step 1: Build for Production
```bash
cd Web/frontend
npm run build
```

This will:
1. Update `public/version.json` with new build number
2. Build the app with Vite (generates hashed filenames)
3. Copy `.htaccess` to `dist/` folder
4. Create `.vite/manifest.json` with file mappings

### Step 2: Deploy to Server
```bash
# Upload the entire dist/ folder to your server
# Example for /home/apkserve/governance/dist
scp -r dist/* user@apkserve.com:/home/apkserve/governance/dist/
```

### Step 3: Verify Apache Configuration
Your Apache config should have:
```apache
# AegisMint Governance Frontend
Alias /governance /home/apkserve/governance/dist
<Directory /home/apkserve/governance/dist>
    Require all granted
    AllowOverride All
    Options -Indexes +FollowSymLinks
    
    <IfModule mod_rewrite.c>
        RewriteEngine On
        RewriteBase /governance/
        
        # Don't rewrite files or directories
        RewriteCond %{REQUEST_FILENAME} !-f
        RewriteCond %{REQUEST_FILENAME} !-d
        
        # Don't rewrite API calls
        RewriteCond %{REQUEST_URI} !^/govern/
        
        # Rewrite everything else to index.html
        RewriteRule ^ /governance/index.html [L]
    </IfModule>
</Directory>
```

### Step 4: Restart Apache
```bash
sudo systemctl restart apache2
# or
sudo service httpd restart
```

## How It Works for Users

### First Visit
1. User visits `https://apkserve.com/governance`
2. Browser downloads `index.html` (not cached)
3. Loads JS/CSS files with hashes (cached for 1 year)
4. Stores current version in localStorage

### When You Deploy Update
1. New build generates new hashes for changed files
2. `version.json` is updated with new build number
3. Users continue using old cached version (no disruption)

### Version Detection (Every 5 Minutes)
1. App checks `version.json` for updates
2. If new version detected, shows update banner
3. User clicks "Refresh Now":
   - All caches are cleared
   - Page reloads
   - New `index.html` is loaded
   - New hashed JS/CSS files are downloaded
   - Old cached files are ignored

## Updating Version Number

To increment the major version (e.g., 1.0.0 → 2.0.0):

1. Edit `package.json`:
   ```json
   "version": "2.0.0"
   ```

2. Build:
   ```bash
   npm run build
   ```

The build number resets to 1 when version changes.

## Troubleshooting

### Users Not Getting Updates
1. Check Apache headers module is enabled:
   ```bash
   sudo a2enmod headers
   sudo systemctl restart apache2
   ```

2. Verify `.htaccess` in dist folder

3. Check browser network tab:
   - `index.html` should show `Cache-Control: no-cache`
   - JS/CSS files should have hashes in filename

### Manual Cache Clear
If needed, users can:
1. Hard refresh: `Ctrl+Shift+R` (Windows/Linux) or `Cmd+Shift+R` (Mac)
2. Clear browser cache manually
3. Use incognito/private mode

## Benefits

✅ **Zero Downtime**: Users continue working while update deploys
✅ **Automatic Detection**: Users notified when update is available
✅ **Fast Loading**: Hashed assets cached for 1 year
✅ **No Manual Steps**: Everything automated in build process
✅ **Version Tracking**: Know exactly what version is deployed
