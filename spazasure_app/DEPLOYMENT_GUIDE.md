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

Before starting, confirm the Android package ID
`com.spazasure.spazasure_app` is the permanent ID for this listing. It cannot
be changed after publishing.

1. Create and verify a Google Play Developer account.
2. Set up **Play App Signing** in Play Console. Keep a secure backup of the
   upload key; if this app is already listed, use the upload key associated
   with that listing.
3. Configure the signing secrets and run the Play Store release workflow in
   [BUILD_GUIDE.md](BUILD_GUIDE.md). This produces a signed AAB artifact but
   does not publish it.
4. Upload the AAB to an **internal testing** track first. Test install,
   sign-in, notifications, camera/OCR, location, deep links, and account
   deletion before moving to a wider track.
5. Complete the Play Console store listing and App content declarations:
   privacy policy, Data safety, ads, app access/reviewer instructions, target
   audience, content rating, and any applicable financial-features
   declarations.

The repository privacy notice now contains the approved company and contact
details, with its version synchronized to backend consent tracking. Deploy the
portal update and verify the public notice before entering its URL in Play
Console. The app also collects sensitive data (including identity documents,
location, and financial details); base Data safety answers on the actual app,
backend, and SDK behavior.

The project targets Android API 36 via its pinned Flutter SDK, meeting Google
Play's current target API requirement. Recheck that requirement before each
submission.

---

## Pushing Updates

Every time you update the app:

### 1. Bump the version in `pubspec.yaml`

```yaml
# Format: version: NAME+CODE
# NAME = what users see
# CODE = must increase every time (Google rejects otherwise)

version: 1.0.24+26  # Current project version
```

Keep the package ID unchanged and increase the build number after `+` for
every Play upload.

### 2. Build and test a signed AAB

Run the workflow's **Play Store release** option, then download
`SpazaSure-Play-Store-AAB`. Do not upload CI artifacts labelled
`CI-TEST-ONLY`; those may be debug-signed.

### 3. Upload the update in Play Console

Upload it to internal testing, verify the release, then promote it to the
appropriate track and add release notes. The workflow does not automatically
publish or roll out releases.

### How users get updates

- Users with automatic updates enabled receive the update automatically.
- Other users can update from the Play Store listing.

---

## GitHub + CI/CD Automation

The existing **Build Mobile Apps (Android + iOS)** workflow runs analysis and
tests on pushes to `qa` and `main`. Regular workflow runs create test-only
artifacts. A manual run with **Require upload-key signing and produce a Play
Store AAB** checked requires the upload-key secrets and creates the signed
Play artifact. Nothing is uploaded to Google Play automatically; this avoids
publishing before the listing, policy declarations, and testing are complete.

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
- Use Play App Signing. Keep the upload key and its passwords backed up securely
  and outside the repository.
- If this app is already on Play, use its existing upload key. If that key is
  lost, follow Play Console's upload-key reset process; do not change the app ID.

### Version Code
- The build number after `+` MUST increase with every upload
- Google Play rejects uploads if the version code isn't higher than the last one

### Testing
- Run analysis and tests, then test the signed AAB through an internal testing
  track before promoting it.

### Privacy Policy
- Google requires a publicly accessible privacy policy at
  `https://portal.spazasure.co.za/privacy`, both in the listing and in the app.
- The current notice still has draft placeholders for the legal entity and
  Information Officer. Do not submit until those details are confirmed and the
  notice has been reviewed.

### Google Play Requirements
- The project currently targets Android API 36. Recheck Google's target API
  deadline before each submission.
- Complete Data safety, ads, app access, target audience, content rating, and
  applicable financial-features declarations.
- Prepare the required store listing assets, including app icon, feature
  graphic, and screenshots.

---

## Quick Reference: Update Checklist

- [ ] Make code changes
- [ ] Run `flutter analyze` and `flutter test`
- [ ] Bump `pubspec.yaml` version and increase build number
- [ ] Commit and push to GitHub
- [ ] Run the Play Store release workflow with upload-key signing enabled
- [ ] Test the signed AAB through Play internal testing
- [ ] Complete declarations, reviewer access, and release notes in Play Console
- [ ] Manually promote the tested release in Play Console

---

## Contact & Resources

- [Google Play Console](https://play.google.com/console)
- [Hetzner Cloud Console](https://console.hetzner.com/projects/14885313/servers)
- [Flutter Deployment Docs](https://docs.flutter.dev/deployment/android)
- [GitHub Actions Documentation](https://docs.github.com/en/actions)
