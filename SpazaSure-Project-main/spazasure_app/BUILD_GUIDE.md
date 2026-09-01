# SpazaSure Release Build Guide

## Local debug build (any machine, no signing needed)
```bash
flutter pub get
flutter run                                   # launches on connected device/emulator
# or, to point at your own local backend instead of the shared QA server:
flutter run --dart-define=API_URL=http://10.0.2.2:5181/api   # Android emulator
flutter run --dart-define=API_URL=http://localhost:5181/api  # iOS simulator / desktop / web
```
See `lib/services/api_service.dart` for the full list of `--dart-define=API_URL=...`
options (local, emulator, physical device, or the shared QA server).

## Android APK/AAB — local release build
`flutter build apk --release` and `flutter build appbundle --release` now work
on any machine out of the box: if no release keystore is configured
(`android/key.properties` doesn't exist), the build automatically falls back
to debug signing so it still produces an installable APK for local testing.
It just won't be signed with your Play Store release key.

To produce a **Play Store-signed** build locally, you need your own keystore:
```bash
keytool -genkey -v -keystore ~/spazasure-release.jks -keyalg RSA \
  -keysize 2048 -validity 10000 -alias spazasure
```
Then create `android/key.properties` (gitignored — never commit this file):
```properties
storePassword=<your password>
keyPassword=<your password>
keyAlias=spazasure
storeFile=/absolute/path/to/spazasure-release.jks
```

### To install an APK on a phone
1. Transfer the `.apk` (from `build/app/outputs/flutter-apk/`) to the phone
2. Open the file, allow "install from unknown sources" if prompted, tap Install

### To publish to Google Play
1. Go to https://play.google.com/console
2. Create the app listing, upload the `.aab` from `build/app/outputs/bundle/release/`
3. Fill in store listing details, submit for review

---

## Cloud builds — no Flutter/Android Studio install required

The repo already includes `.github/workflows/build-mobile.yml`, which builds
both the Android APK/AAB and an unsigned iOS IPA entirely on GitHub's runners.

1. Push the repo to GitHub
2. **Actions → "Build Mobile Apps (Android APK + iOS IPA)" → Run workflow**
3. Download the built APK/AAB/IPA from the workflow's Artifacts section

For a **release-signed** Android build from CI, add these as repository secrets
(Settings → Secrets and variables → Actions):
- `ANDROID_KEYSTORE_BASE64` — your keystore, base64-encoded (`base64 -w0 your.jks`)
- `KEYSTORE_PASSWORD`
- `KEY_PASSWORD`

> A previous version of this repo had a keystore committed as a plaintext file
> (`SpazaSure.Backend/keystore_base64.txt`). That file has been removed and
> `.gitignore`'d. If this repo has ever been pushed anywhere with that file in
> its history, treat the key as compromised: generate a fresh keystore, update
> the GitHub secrets above, and re-sign future releases with the new key.

### iOS (requires a Mac, or use CI above)
iOS apps can't be built on Windows/Linux directly.
- **Local:** `cd ios && pod install && cd .. && flutter build ipa --release` on a Mac with Xcode
- **Cloud, no Mac needed:** the GitHub Actions workflow above builds it on
  `macos-latest` runners automatically (unsigned — add your own signing/
  provisioning step if you need an App Store build)

---

## App Details
- **App Name**: SpazaSure
- **Bundle ID**: com.spazasure.spazasure_app
- **Min Android**: SDK 21 (Android 5.0)
- **Min iOS**: 12.0
- **Backend**: configurable at build/run time via `--dart-define=API_URL=...`
  (see `lib/services/api_service.dart`); defaults to `localhost`/`10.0.2.2`
  for local development rather than any specific deployed server.
