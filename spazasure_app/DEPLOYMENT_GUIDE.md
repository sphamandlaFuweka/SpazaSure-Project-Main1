# SpazaSure App - Deployment & Publishing Guide

This document covers everything you need to know about hosting, publishing, and updating your SpazaSure app.

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Hetzner Server (Backend Hosting)](#hetzner-server-backend-hosting)
3. [Publishing to Google Play Store](#publishing-to-google-play-store)
4. [Pushing Updates](#pushing-updates)
5. [GitHub + CI/CD Automation](#github--cicd-automation)
6. [Version Numbering](#version-numbering)
7. [Important Rules & Warnings](#important-rules--warnings)

---

## Architecture Overview

Your app has two parts:

| Component | Where it lives | Purpose |
|-----------|---------------|---------|
| API + Dashboard | Hetzner Server | Backend logic, database, admin panel |
| Mobile App (Flutter) | Google Play Store | What users download and use |

```
┌─────────────────┐          HTTPS           ┌──────────────────────────┐
│  User's Phone   │  ◄─────────────────────►  │  Hetzner Server          │
│  (Flutter App)  │     API requests          │  - API backend           │
│                 │                           │  - Dashboard (web panel) │
└─────────────────┘                           └──────────────────────────┘
       │
       │ Downloaded from
       ▼
┌─────────────────┐
│  Google Play    │
│  Store          │
└─────────────────┘
```

---

## Hetzner Server (Backend Hosting)

Your Hetzner server (https://console.hetzner.com/projects/14885313/servers) handles:
- Your API (the backend your Flutter app talks to)
- Your Dashboard (admin web panel)

### What you need to configure:

1. **Domain Name** - Point a domain to your Hetzner server's IP address (e.g., `api.spazasure.co.za`)
2. **SSL/HTTPS** - Set up Let's Encrypt / Certbot for secure communication
3. **Reverse Proxy** - Use Nginx or Caddy in front of your API
4. **Process Manager** - Use `systemd`, `pm2`, or Docker so your API restarts automatically if the server reboots

### Updating the Backend

When you change your API or dashboard code:

1. Push code to your server (via `git pull`, `scp`, or your deployment method)
2. Restart your API service (`systemctl restart your-api` or `pm2 restart all`)
3. Changes are live immediately — no Google Play involved

---

## Publishing to Google Play Store

### Prerequisites

- Google Play Developer account ($25 one-time fee) at [play.google.com/console](https://play.google.com/console)
- Identity verification (takes a few days)
- A signing keystore (created once, used forever)
- Privacy policy URL (host on your Hetzner server)

### Step 1: Create a Signing Key

```bash
keytool -genkey -v -keystore spazasure-key.jks -keyalg RSA -keysize 2048 -validity 10000 -alias spazasure
```

**SAVE THIS KEY SAFELY. Lose it and you can NEVER update your app.**

### Step 2: Configure Signing in Android

Add your keystore details to `android/app/build.gradle` so release builds are signed.

### Step 3: Build the Release

```bash
flutter build appbundle --release
```

Output file: `build/app/outputs/bundle/release/app-release.aab`

### Step 4: Create App Listing on Google Play Console

Fill in:
- App name and description
- Screenshots (phone and tablet)
- Privacy policy URL
- Content rating questionnaire
- Target audience and category

### Step 5: Upload and Publish

1. Go to **Production** → **Create new release**
2. Upload the `app-release.aab`
3. Add release notes
4. Click **Review release** → **Start rollout to production**

### Step 6: Wait for Review

Google reviews new apps (typically 1–7 days for first submission).

---

## Pushing Updates

Every time you update the app:

### 1. Bump the version in `pubspec.yaml`

```yaml
# Format: version: NAME+CODE
# NAME = what users see
# CODE = must increase every time (Google rejects otherwise)

version: 1.0.0+1   # First release
version: 1.0.1+2   # Bug fix
version: 1.1.0+3   # New feature
version: 2.0.0+4   # Major update
```

### 2. Build the new release

```bash
flutter build appbundle --release
```

### 3. Upload to Google Play Console

1. Go to [play.google.com/console](https://play.google.com/console)
2. Select SpazaSure
3. **Production** → **Create new release**
4. Upload the new `.aab` file
5. Add release notes (what changed)
6. **Review release** → **Start rollout to production**

### 4. Wait for review

Updates are usually reviewed faster (few hours to 2 days).

### How Users Get Updates

- Auto-update ON → updates automatically
- Auto-update OFF → they see "Update available" in Play Store
- You can add an in-app version check to prompt users or block old versions

---

## GitHub + CI/CD Automation

GitHub automates the entire build and publish process so you just push code and it handles the rest.

### The Automated Flow

```
You write code locally
       │
       ▼
git push to GitHub
       │
       ▼
GitHub Actions automatically:
  - Builds the .aab
  - Signs it with your key
  - Uploads it to Google Play
       │
       ▼
Google reviews → Users get the update
```

### One-Time Setup

#### 1. Push your project to GitHub

```bash
git init
git remote add origin https://github.com/your-username/spazasure-app.git
git add .
git commit -m "initial commit"
git push -u origin main
```

#### 2. Add Secrets to GitHub

Go to your GitHub repo → Settings → Secrets and variables → Actions

| Secret Name | Value |
|-------------|-------|
| `KEYSTORE_BASE64` | Your `.jks` file encoded as base64 |
| `KEYSTORE_PASSWORD` | Your keystore password |
| `KEY_ALIAS` | Your key alias (e.g., `spazasure`) |
| `KEY_PASSWORD` | Your key password |
| `PLAY_SERVICE_ACCOUNT_JSON` | Google Play service account JSON |

To encode your keystore as base64 (run locally):
```bash
base64 spazasure-key.jks > keystore-base64.txt
```

Copy the contents of `keystore-base64.txt` into the `KEYSTORE_BASE64` secret.

#### 3. Create a Google Play Service Account

1. Go to Google Cloud Console → Create a service account
2. Go to Google Play Console → Settings → API access
3. Link the service account and grant "Release manager" permissions
4. Download the JSON key file
5. Paste the JSON content into the `PLAY_SERVICE_ACCOUNT_JSON` GitHub secret

#### 4. Create the GitHub Actions Workflow

Create the file `.github/workflows/deploy.yml`:

```yaml
name: Deploy to Google Play

on:
  push:
    tags:
      - 'v*'  # Triggers when you push a tag like v1.1.0

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: subosito/flutter-action@v2
        with:
          flutter-version: '3.24.0'

      - run: flutter pub get

      - run: flutter build appbundle --release

      - name: Sign AAB
        run: |
          echo "${{ secrets.KEYSTORE_BASE64 }}" | base64 --decode > keystore.jks
          jarsigner -keystore keystore.jks \
            -storepass "${{ secrets.KEYSTORE_PASSWORD }}" \
            -keypass "${{ secrets.KEY_PASSWORD }}" \
            build/app/outputs/bundle/release/app-release.aab \
            "${{ secrets.KEY_ALIAS }}"

      - name: Upload to Google Play
        uses: r0adkll/upload-google-play@v1
        with:
          serviceAccountJsonPlainText: ${{ secrets.PLAY_SERVICE_ACCOUNT_JSON }}
          packageName: com.spazasure.app
          releaseFiles: build/app/outputs/bundle/release/app-release.aab
          track: production
```

### Pushing an Update (After Setup)

Once everything is configured, updating your app is just:

```bash
# Make your code changes, then:
git add .
git commit -m "Added new feature X"
git tag v1.1.0
git push origin main --tags
```

GitHub Actions handles building, signing, and uploading automatically.

---

## Version Numbering

```
version: MAJOR.MINOR.PATCH+BUILD_NUMBER
```

| Part | When to increase | Example |
|------|-----------------|---------|
| MAJOR | Breaking changes, major redesign | 1.0.0 → 2.0.0 |
| MINOR | New features | 1.0.0 → 1.1.0 |
| PATCH | Bug fixes | 1.0.0 → 1.0.1 |
| BUILD_NUMBER | Every single release (must always go up) | +1 → +2 → +3 |

---

## Important Rules & Warnings

### Signing Key
- **NEVER lose your signing keystore file** — without it, you can never update your app on Google Play
- Keep backups in multiple secure locations
- Use the SAME key for every update

### Version Code
- The build number after `+` MUST increase with every upload
- Google Play rejects uploads if the version code isn't higher than the last one

### Testing
- Always test on a real device before uploading
- Run `flutter build appbundle --release` and install the release build to verify

### Privacy Policy
- Google requires a privacy policy URL before publishing
- Host it on your Hetzner server (e.g., `https://spazasure.co.za/privacy-policy`)

### Google Play Requirements
- App must target latest Android API level
- Must have proper content ratings
- Must comply with Google Play policies
- Screenshots required for store listing

---

## Quick Reference: Update Checklist

- [ ] Make code changes
- [ ] Bump version in `pubspec.yaml` (increase build number)
- [ ] Test on real device
- [ ] Commit and push to GitHub
- [ ] Tag the release (`git tag v1.x.x`)
- [ ] Push tags (`git push origin main --tags`)
- [ ] GitHub Actions builds and uploads automatically
- [ ] Verify in Google Play Console that the release is processing
- [ ] Add release notes in Play Console if not automated

---

## Contact & Resources

- [Google Play Console](https://play.google.com/console)
- [Hetzner Cloud Console](https://console.hetzner.com/projects/14885313/servers)
- [Flutter Deployment Docs](https://docs.flutter.dev/deployment/android)
- [GitHub Actions Documentation](https://docs.github.com/en/actions)
