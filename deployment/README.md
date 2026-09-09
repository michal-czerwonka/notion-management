# Deployment

GitHub Actions contains two workflows that run automatically after a push to `main` (including a merged pull request) and can also be started manually from the **Actions** tab:

- `Deploy Function App (dev)` configures and deploys the Azure Function App.
- `Build Android App (dev)` creates a debug APK, exposes it as a GitHub Actions artifact for seven days, and uploads it to Google Drive.

## Configuration model

`deployment/config/development.json` is the versioned, non-secret configuration for the GitHub `dev` environment: Azure resource names, schedules, Notion data-source IDs, CORS origins and the public API URL embedded in the development APK.

Local configuration remains outside Git:

- `NotionManagementFunctionApp/local.settings.json` configures the Function App and secrets for local execution.
- `NotionManagementApp/.env.local-emulator` configures the emulator APK.
- `NotionManagementApp/.env.development-phone` configures the locally built APK for a physical phone.

Do not add tokens, keys, connection strings, or the ntfy topic to `development.json`.

## Required GitHub repository secrets

Create these at **Settings → Secrets and variables → Actions → Secrets**:

- `AZURE_CLIENT_ID` — client ID of the Microsoft Entra application used by GitHub Actions.
- `AZURE_TENANT_ID` — Microsoft Entra tenant ID.
- `AZURE_SUBSCRIPTION_ID` — Azure subscription ID.
- `NOTION_TOKEN` — Notion integration token.
- `NTFY_TOPIC` — private ntfy topic.
- `GOOGLE_DRIVE_CLIENT_ID` — OAuth client ID used to upload APK files.
- `GOOGLE_DRIVE_CLIENT_SECRET` — OAuth client secret used to refresh Google Drive access.
- `GOOGLE_DRIVE_REFRESH_TOKEN` — OAuth refresh token authorized for the target Google Drive account.
- `GOOGLE_DRIVE_APK_FOLDER_ID` — ID of the Google Drive folder where development APK files are stored.
- `ANDROID_DEV_KEYSTORE_BASE64` — Base64-encoded development keystore file.
- `ANDROID_DEV_KEYSTORE_PASSWORD` — password protecting the development keystore.
- `ANDROID_DEV_KEY_ALIAS` — alias of the development signing key.
- `ANDROID_DEV_KEY_PASSWORD` — password protecting the development signing key.

Repository secrets work in private repositories on GitHub Free. GitHub Free does not provide environment secrets for private repositories, so this project deliberately uses repository secrets rather than a GitHub Environment.

Do not store the refresh token in the repository or `development.json`. When the OAuth application is in Google testing mode, refresh tokens expire after seven days and `GOOGLE_DRIVE_REFRESH_TOKEN` must be replaced after a new authorization.

## Android development signing key

Generate one development keystore locally and keep it indefinitely. Do not use the default Gradle debug keystore: each clean GitHub-hosted runner creates its own key, so Android rejects it as an update to an APK built elsewhere.

From the repository root, run the following command and choose strong passwords when prompted:

```powershell
New-Item -ItemType Directory -Force NotionManagementApp\android\keystore
keytool -genkeypair -v -keystore NotionManagementApp\android\keystore\notion-management-dev.jks -alias notion-management-dev -keyalg RSA -keysize 4096 -validity 10000
```

Copy `NotionManagementApp/android/signing.properties.example` to `NotionManagementApp/android/signing.properties`, then replace the password placeholders and alias with the values selected above. Both paths are ignored by Git.

Create `ANDROID_DEV_KEYSTORE_BASE64` from the keystore without committing or printing it to a terminal:

```powershell
$keystoreBytes = [System.IO.File]::ReadAllBytes('NotionManagementApp\android\keystore\notion-management-dev.jks')
[Convert]::ToBase64String($keystoreBytes) | Set-Clipboard
```

Paste the clipboard content into the GitHub Secret, then add the three password and alias secrets. The next GitHub Android build is the first one signed with this key. Before installing it, uninstall any copy signed by the old local or GitHub default debug key. Future local and GitHub builds using this configuration update each other normally.

## Azure OpenID Connect setup

Create a Microsoft Entra application/service principal and configure a federated credential that trusts this GitHub repository's `main` branch. Grant the service principal the narrowest role that can update Function App settings, CORS and deploy code; start with **Website Contributor** scoped to the target Function App. The workflow uses `id-token: write` and `azure/login@v2`, so it never stores an Azure client secret or a Function App publish profile in GitHub.

If Azure denies a required operation, expand the role only for that operation and keep the scope at the Function App rather than the subscription or resource group.

## Function App deployment process

1. GitHub authenticates to Azure with OIDC.
2. The workflow validates required secrets and `development.json`.
3. It applies Function App settings, including Notion and ntfy secrets, and configured CORS origins.
4. It publishes the .NET project locally on the runner, creates a ZIP with the publish output and performs Azure Functions ZIP deployment.
5. It calls the deployed Inbox API and fails if the endpoint does not return a successful response.

## Android build process

1. GitHub installs Node.js 24, JDK 21 and Android SDK API 36.
2. It installs npm dependencies from the lockfile.
3. It writes a temporary `.env.development-phone` from `development.json`; this file is ignored and never committed.
4. It restores the development signing keystore from GitHub Secrets.
5. Vite builds the web application, Capacitor synchronizes the Android project, and the Gradle Wrapper builds a signed debug APK.
6. GitHub publishes the APK as `NotionManagementApp-dev-debug-apk` for seven days.
7. It refreshes a short-lived Google access token and uploads a uniquely named APK to the configured Google Drive folder.

The APK is debug-signed. It is suitable for manual installation and development testing, not for Play Store distribution.
