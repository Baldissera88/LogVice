# Releasing LogVice

How a release is made. The author builds and tests on the PC, then publishes a GitHub release with only the release notes. Everything else is done by the [Build workflow](../.github/workflows/build.yml) on GitHub. Nothing is signed: the exe and the MSI are unsigned, and the build is attested instead.

## What you need

- Git and the [GitHub CLI](https://cli.github.com/) (`gh auth login` once), or the GitHub web page for the release.
- To check a build before you push: Mono and msitools for `build.sh`, `package.sh` and `tests/run.sh`, or Windows with .NET Framework 4.5 or later for `build.ps1`.
- A repository secret `WINGET_TOKEN` for the winget pull request: a classic GitHub token with the `public_repo` scope. Without it the workflow only keeps the manifests as an artifact.

## Steps

1. **Bump the version.** On a clean `main`:

   ```sh
   ./bump.sh 2.1.0
   ```

   or `.\bump.ps1 2.1.0` on Windows. It writes the version to `LogVice.wxs` with a new product code (the upgrade code stays, so the MSI upgrades the old version) and to `src/AssemblyInfo.cs`. In `README.md` it sets the version badge, the uninstall command and the detection product code. In `CHANGELOG.md` it adds a row to the At a glance table, a dated section with the product code, and the product code in the install table at the end. It refuses a version that is not higher than the current one.

2. **Write the changelog.** Fill in the new section in `CHANGELOG.md` in the same style as the others: one short sentence on the release, then New, Changed and Fixed. Write the highlights in the At a glance row too. This text is what users read.

3. **Build.** `./build.sh` must print `EXE_OK`, or `.\build.ps1` on Windows. `./package.sh` builds the exe and the MSI and must print `PACKAGE_OK`.

4. **Test.** `./tests/run.sh` must end with `ALL TESTS PASSED`. Go through the checklist below for anything that changed. Then commit and push:

   ```sh
   git commit -am "LogVice 2.1.0"
   git push
   ```

   The workflow runs on the push, builds and tests, and keeps `LogVice.exe`, `LogVice.msi` and `SHA256SUMS.txt` as an artifact of the run. Check that it is green before you publish.

5. **Sync the winget fork.** Before you publish, open [Baldissera88/winget-pkgs](https://github.com/Baldissera88/winget-pkgs) on GitHub and select **Sync fork**. The pull request that `wingetcreate` opens starts from the fork, so an old fork gives a pull request with conflicts or old files.

6. **Publish the GitHub release.** On GitHub, create a release with the tag `v<version>`, for example `v2.1.0`, on the pushed commit, with the title `LogVice 2.1.0`. In the description put only the release notes: the New, Changed and Fixed parts of the changelog section, without its title, date and table. Do not attach files, and do not write a *Verify this download* block or a *Deployment* table. Do not mark a normal release as a pre-release: LogVice asks GitHub for the latest release, which leaves pre-releases out, and the winget pull request is skipped for them.

   When the release is published, the workflow:

   1. Builds `LogVice.exe` and `LogVice.msi` with `package.sh`, and runs the tests.
   2. Reads the version and product code from the MSI. It stops when the tag and the version in `LogVice.wxs` differ.
   3. Writes `SHA256SUMS.txt` for the exe and the MSI.
   4. Signs a build provenance attestation for both files, so `gh attestation verify` can show that GitHub built them from this repository.
   5. Uploads `LogVice.exe`, `LogVice.msi` and `SHA256SUMS.txt` to the release.
   6. Rewrites the release description: a **Verify this download** block first, with the checksums and the commands `Get-FileHash` and `gh attestation verify`, and the commit; then **What's new** with your notes; then a **Deployment** table with the product code and the install and uninstall commands. LogVice reads the *What's new* part for its own update dialog and What's new page, so keep those headings as they are.
   7. Writes the winget manifests with `winget/manifests.sh` and keeps them as the `winget-<version>` artifact.
   8. For a release that is not a pre-release, a second job submits the manifests to [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs) with `wingetcreate`, which opens the pull request from the fork. It needs the `WINGET_TOKEN` secret. Without it, the job says so and stops.

7. **Check the release.** Open the release page and read the description. Download the files and compare them:

   ```powershell
   Get-FileHash .\LogVice.msi -Algorithm SHA256     # compare with SHA256SUMS.txt
   gh attestation verify .\LogVice.msi -R Baldissera88/LogVice
   ```

   Then look at the winget pull request, and answer the questions of its checks. If the workflow failed, fix the cause, push, and run the workflow again from the **Actions** page. A release that has no `SHA256SUMS.txt` is offered to users only as a download, not as an update they can install from LogVice.

## Screenshots

The pictures in `README.md` and `docs/GUIDE.md` come from the app itself, with made-up logs of CONTOSO-LT0417. Take them again when the look of LogVice changes:

```sh
LogVice.exe --demo --theme vice --size 1400x860 --shot docs/images/main-vice.png
```

Use `--theme light` and `--theme terminal` for the other themes, `--page welcome` for the start page, `--page browse`, `--page follow`, `--page lookup`, `--page ticket`, `--page settings` and `--page settings-highlights` for the others, `--page update-note` for the update note, and `--select "Upload timed out after"` to pick a JSON Lines row. A run with `--shot` saves nothing to the registry and makes no update check. Look at each picture before you commit it, and remove pictures that no page links to.

## Before a release: checklist

Try these on a Windows PC for the parts the release changes, and at least:

- [ ] Open a log, several logs merged, an event log and a JSON Lines log.
- [ ] Follow an item on a PC with a ConfigMgr client or the Intune Management Extension.
- [ ] Save a session, open it again, and use Back and Forward.
- [ ] Copy for ticket, and paste the text into a mail.
- [ ] The MSI: a clean install, an upgrade over the previous release with LogVice open, and an uninstall. Double-click a `.logvice` file.
- [ ] The update from the previous release, once published: Settings > Updates > Check now, then Install, with the MSI install and with a copy in a folder. LogVice opens again in the new version with your logs.
- [ ] The policy `DisableUpdateCheck` turns the check off.
- [ ] Anything that failed is in the changelog or fixed before the release.
