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

Both build commands work on any machine out of the box: if no release keystore is configured
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
1. Transfer the matching architecture APK from `build/app/outputs/flutter-apk/` to the phone
2. Open the file, allow "install from unknown sources" if prompted, tap Install

### To publish to Google Play
1. Go to https://play.google.com/console
2. Create the app listing, upload the `.aab` from `build/app/outputs/bundle/release/`
3. Fill in store listing details, submit for review

---

## Cloud builds — no Flutter/Android Studio install required

The repo already includes `.github/workflows/build-mobile.yml`, which builds
the Android APK/AAB and checks both the iOS Simulator and iOS device builds on
GitHub's runners.

1. Push the repo to GitHub
2. **Actions → "Build Mobile Apps (Android + iOS)" → Run workflow**
3. Download the APK/AAB and the unsigned iOS app from the workflow's Artifacts section.
   The unsigned iOS app is for build verification only; it cannot be installed on an iPhone.

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
- **Bundle ID**: com.spazasure.spazasureapp
- **Min Android**: SDK 21 (Android 5.0)
- **Min iOS**: 15.5
- **Backend**: release builds default to `https://api.spazasure.co.za/api`;
  local development can override it with `--dart-define=API_URL=...`
  (see `lib/services/api_service.dart`); defaults to `localhost`/`10.0.2.2`
  for local development rather than any specific deployed server.
