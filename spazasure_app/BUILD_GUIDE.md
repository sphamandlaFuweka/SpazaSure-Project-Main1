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
Build smaller, device-specific APKs with:
```bash
flutter build apk --release --split-per-abi
```
This creates separate APKs for `armeabi-v7a`, `arm64-v8a`, and `x86_64` in
`build/app/outputs/flutter-apk/`. Install only the APK matching the device;
most modern Android phones use `app-arm64-v8a-release.apk`. Avoid sending the
same universal APK to every device, as it includes native libraries for every
supported CPU architecture.

For Google Play, prefer an App Bundle:
```bash
flutter build appbundle --release
```
Google Play delivers an optimized install for each device automatically.

Both build commands work without a release key for local testing. In that case
Gradle signs them with the debug key; **never upload those test builds to Google
Play**.

### Play Store signing

Use Google Play App Signing and keep the upload key backed up somewhere secure
outside the repository. If the app has already been published, use the upload
key registered in Play Console; do not generate a replacement without first
checking the existing app's signing setup.

On Windows PowerShell, create an upload key and a Base64 copy outside the repo:
```powershell
keytool -genkeypair -v -keystore "$env:USERPROFILE\spazasure-upload.jks" `
  -keyalg RSA -keysize 2048 -validity 10000 -alias spazasure
[Convert]::ToBase64String([IO.File]::ReadAllBytes("$env:USERPROFILE\spazasure-upload.jks")) |
  Set-Content -NoNewline "$env:USERPROFILE\spazasure-upload-base64.txt"
```
Never commit or share the `.jks`, its Base64 copy, or its passwords. Back up
the original key securely. Play App Signing manages the app-signing key; this
key is only used to authenticate uploads.

For CI signing, add these **repository Actions secrets**:
- `ANDROID_KEYSTORE_BASE64` — contents of `spazasure-upload-base64.txt`
- `KEYSTORE_PASSWORD` — the upload key's keystore password
- `KEY_PASSWORD` — the alias key password
- `KEY_ALIAS` — the alias used when creating the upload key

In **Actions → Build Mobile Apps (Android + iOS) → Run workflow**, select
**Require upload-key signing and produce a Play Store AAB**. This option fails
if any signing secret is missing and uploads the AAB as
`SpazaSure-Play-Store-AAB`. The regular CI artifacts are debug-signed
test builds and are explicitly marked `CI-TEST-ONLY`. The workflow builds an
artifact only; it does not publish to Play Console.

### To install an APK on a phone
1. Transfer the matching architecture APK from `build/app/outputs/flutter-apk/` to the phone
2. Open the file, allow "install from unknown sources" if prompted, tap Install

### To publish to Google Play
1. Run the workflow with Play Store signing enabled and download
   `SpazaSure-Play-Store-AAB`.
2. Upload that signed AAB to the app's internal testing track in
   [Google Play Console](https://play.google.com/console).
3. Finish the listing and policy declarations, test the release, then promote
   it manually. The workflow does not publish to the store.

---

## Cloud builds — no Flutter/Android Studio install required

The repo already includes `.github/workflows/build-mobile.yml`, which builds
the Android APK/AAB and checks both the iOS Simulator and iOS device builds on
GitHub's runners.

1. Push the repo to GitHub
2. **Actions → "Build Mobile Apps (Android + iOS)" → Run workflow** for a test build.
3. For a Play-signed AAB, configure the upload-key secrets above and select the
   Play Store release option before running the workflow.
4. Download the appropriate artifact. The unsigned iOS app is for build
   verification only; it cannot be installed on an iPhone.

### Before submitting to Play Console

- Confirm the package ID is the intended permanent listing ID:
  `com.spazasure.spazasure_app`.
- Test the signed AAB through an internal testing track before production.
- Complete the store listing, content rating, target-audience, ads, app-access,
  and Data safety declarations accurately in Play Console.
- Provide reviewer access instructions and a working test account if login is
  required to review the app.
- The repository privacy notice has the company and contact details supplied
  for the approved notice. Deploy the portal changes and verify the live
  `https://portal.spazasure.co.za/privacy` page before entering that URL in
  Play Console.

> A previous version of this repo had a keystore committed as a plaintext file
> (`SpazaSure.Backend/keystore_base64.txt`). That file has been removed and
> `.gitignore`'d. If this repo has ever been pushed anywhere with that file in
> its history, treat the key as compromised: generate a fresh keystore, update
> the GitHub secrets above, and re-sign future releases with the new key.

### iOS and TestFlight
iOS apps cannot be built or signed for iPhone from Windows/Linux. Use a Mac
with Xcode, or the macOS GitHub Actions runner for unsigned build verification.

The project has an iOS Podfile and targets **iOS 15.5 or later**. On a Mac:

```bash
cd spazasure_app
flutter pub get
cd ios
pod install
cd ..
flutter run
```

Test on a physical iPhone, especially QR scanning, photo upload, location,
OCR, deep links, and notifications. The CI artifact is unsigned and is not an
installable IPA.

To distribute through TestFlight:

1. Enroll in the Apple Developer Program and create an App Store Connect app
   with bundle ID `com.spazasure.spazasureapp`.
2. In Xcode, select your Apple Developer team and enable automatic signing for
   the Runner target. Create an App Store distribution build with
   `flutter build ipa --release`, then upload it to App Store Connect.
3. For push notifications, enable the Push Notifications capability for the
   App ID, create an APNs authentication key, and upload it in Firebase
   Console → Project settings → Cloud Messaging. Test on a real iPhone.
4. Complete the App Store privacy disclosures, age rating, screenshots, and
   review details before submitting. The privacy notice URL is
   `https://portal.spazasure.co.za/privacy`; confirm its content is final and
   published before using it as the store privacy-policy URL. The disclosures
   should reflect the app's actual Firebase Analytics, account, location, order,
   and document-upload behavior; verify the data collected by the backend and
   SDKs before submitting.

Apple signing credentials and App Store Connect access must remain with the
app owner; do not commit certificates, profiles, API keys, or passwords.

---

## App Details
- **App Name**: SpazaSure
- **Android application ID**: `com.spazasure.spazasure_app`
- **Minimum Android API**: 24 (Android 7.0)
- **Target Android API**: 36 (from the pinned Flutter 3.47.3 SDK)
- **Current version**: `1.0.24+26` (`+26` is the Play version code)
- **iOS bundle ID**: `com.spazasure.spazasureapp`
- **Min iOS**: 15.5
- **Backend**: release builds default to `https://api.spazasure.co.za/api`;
  local development can override it with `--dart-define=API_URL=...`
  (see `lib/services/api_service.dart`); defaults to `localhost`/`10.0.2.2`
  for local development rather than any specific deployed server.
