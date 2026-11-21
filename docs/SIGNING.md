# Android release signing (Maptifier)

`Maptifier → Build → Android AAB (Release)` (`Maptifier.Editor.BuildScripts.BuildAndroidAab`)
signs the AAB with the **upload key** only. It never falls back to Unity's debug key:
if any setting below is missing, the build fails before anything is built.

## Environment variables

| Variable | Required | Meaning |
| --- | --- | --- |
| `MAPTIFIER_KEYSTORE_PATH` | optional | Path to the upload keystore. Default: `~/.maptifier/maptifier-upload.keystore` |
| `MAPTIFIER_KEYSTORE_PASS` | yes | Keystore (store) password |
| `MAPTIFIER_KEY_ALIAS` | yes | Key alias (e.g. `maptifier-upload`) |
| `MAPTIFIER_KEY_PASS` | yes | Key password |

Passwords are never logged. After every build attempt the script clears the passwords,
restores the previous keystore path/alias and `useCustomKeystore`, and saves, so no
secret or machine-specific path is left in `ProjectSettings/ProjectSettings.asset`.

## Create the upload key (once, outside the repo)

25-year validity (9125 days). `keytool` prompts for the passwords and name, so nothing lands in shell history.

```bash
mkdir -p ~/.maptifier && chmod 700 ~/.maptifier
keytool -genkeypair -v \
  -keystore ~/.maptifier/maptifier-upload.keystore \
  -alias maptifier-upload \
  -keyalg RSA -keysize 4096 \
  -validity 9125 \
  -storetype PKCS12
chmod 600 ~/.maptifier/maptifier-upload.keystore
keytool -list -v -keystore ~/.maptifier/maptifier-upload.keystore   # note the SHA-256 fingerprint
```

Back up the keystore and passwords in a password manager. Never commit them:
`.gitignore` ignores `*.keystore`, `*.jks`, and `keystore.properties`.

## Build

```bash
export MAPTIFIER_KEYSTORE_PASS=...   # set from your password manager, not typed into history
export MAPTIFIER_KEY_ALIAS=maptifier-upload
export MAPTIFIER_KEY_PASS=...
Unity -batchmode -quit -projectPath "My project" \
  -executeMethod Maptifier.Editor.BuildScripts.BuildAndroidAab -logFile -
```

Bump the Android bundle version code before every Play upload. CI is not wired for signing yet.
