# Debug signing key

`debug.keystore` signs every APK `pr-build` and `rc-build` produce. It is
committed on purpose, and it is **not a secret**.

| | |
| --- | --- |
| File | `.github/signing/debug.keystore` (JKS) |
| Store password | `android` |
| Key alias | `androiddebugkey` |
| Key password | `android` |
| Certificate | `CN=Multiplying Frogs Debug, O=Android Debug, C=US`, RSA 2048, valid to 2076 |
| SHA-256 | `7F:DC:54:D4:B2:EE:59:86:B6:D1:D9:FE:C8:04:E6:EE:B9:49:EA:52:27:6F:97:63:66:8E:48:53:2D:0A:32:76` |

## Why it is committed

Android installs an update only when it is signed with the same certificate as
the installed app. Without a keystore, Unity signs with a debug key generated
fresh in each CI container, so no debug or RC build installed over the last
one (#532). One committed key makes every debug build share a certificate.

## Why committing it is safe

- It only ever signs the **`.debug`** application ID. The release app has a
  different ID, so this key cannot update, replace or impersonate it.
- The release key is a different key, held only in repository secrets and
  reachable only from `release-build`. Neither `pr-build` nor `rc-build` names
  those secrets; `.github/scripts/tests/test_release_signing.py` enforces that.

## Do not replace it casually

A new key here means every installed `.debug` app needs one uninstall before
the next build will install. If it must change, update the SHA-256 above in the
same commit — the test reads it from this file — and the copy in
`docs/engineering/ci-cd.md`.
