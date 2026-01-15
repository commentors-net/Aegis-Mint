const fs = require('fs');
const path = require('path');

// Read package.json for version
const packageJson = JSON.parse(fs.readFileSync(path.join(__dirname, 'package.json'), 'utf8'));

// Read current version.json
const versionPath = path.join(__dirname, 'public', 'version.json');
let versionData = { version: packageJson.version, buildDate: '', buildNumber: 1 };

if (fs.existsSync(versionPath)) {
  const existingVersion = JSON.parse(fs.readFileSync(versionPath, 'utf8'));
  // Increment build number if version is the same
  if (existingVersion.version === packageJson.version) {
    versionData.buildNumber = (existingVersion.buildNumber || 0) + 1;
  }
}

// Update build date
versionData.buildDate = new Date().toISOString();

// Write updated version.json
fs.writeFileSync(versionPath, JSON.stringify(versionData, null, 2));

console.log(`✓ Updated version.json: v${versionData.version} (build ${versionData.buildNumber})`);

// Copy .htaccess template to dist after build
const distPath = path.join(__dirname, 'dist');
if (fs.existsSync(distPath)) {
  const htaccessTemplate = path.join(__dirname, '.htaccess.template');
  const htaccessDist = path.join(distPath, '.htaccess');
  
  if (fs.existsSync(htaccessTemplate)) {
    fs.copyFileSync(htaccessTemplate, htaccessDist);
    console.log('✓ Copied .htaccess to dist/');
  }
}
