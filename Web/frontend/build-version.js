const fs = require('fs');
const path = require('path');

// Read package.json for version
const packageJsonPath = path.join(__dirname, 'package.json');
const packageJson = JSON.parse(fs.readFileSync(packageJsonPath, 'utf8'));

// Parse current version from package.json
let [major, minor, patch] = packageJson.version.split('.').map(Number);

// Read current version.json
const versionPath = path.join(__dirname, 'public', 'version.json');
let versionData = { version: packageJson.version, buildDate: '', buildNumber: 1 };

if (fs.existsSync(versionPath)) {
  const existingVersion = JSON.parse(fs.readFileSync(versionPath, 'utf8'));
  // Increment build number
  versionData.buildNumber = (existingVersion.buildNumber || 0) + 1;
}

// Auto-increment version with rollover logic
patch += 1;
if (patch > 9) {
  patch = 0;
  minor += 1;
  if (minor > 9) {
    minor = 0;
    major += 1;
  }
}

const newVersion = `${major}.${minor}.${patch}`;
versionData.version = newVersion;

// Update build date
versionData.buildDate = new Date().toISOString();

// Write updated version.json
fs.writeFileSync(versionPath, JSON.stringify(versionData, null, 2));

// Update package.json with new version
packageJson.version = newVersion;
fs.writeFileSync(packageJsonPath, JSON.stringify(packageJson, null, 2) + '\n');

console.log(`✓ Updated version: v${newVersion} (build ${versionData.buildNumber})`);
console.log(`✓ Updated package.json and version.json`);

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
