# Roadmap

What is planned for the next version of LogVice.

Everything released so far is in CHANGELOG.md (latest: LogVice 2.0.0).

## 1. PolicyVice opens LogVice at a policy's lines

PolicyVice and TrackVice already point at a log. The next step is that they call LogVice with the row they mean, so one click goes from a policy or a step to the line behind it.

- **Open at the line.** `--line`, `--time`, `--find` and `--filter` already work. What is left is to agree on what PolicyVice sends for a policy: the log, the time of the evaluation and the policy ID as the text to find.
- **A list of lines.** A session can hold the lines a tool found, so LogVice opens with them marked and you step from one to the next.

## 2. Code signing

The exe and the MSI are unsigned, so SmartScreen warns on a download, and AppLocker or WDAC need your own certificate. Signing them would remove the first and make the second easier.

- **Sign the release build.** Sign `LogVice.exe` first, build the MSI around it, then sign the MSI, with a time stamp.
- **Keep the attestation.** The build provenance stays, so a download can still be checked against the repository.

## 3. A tray and a compact view

A small window that stays on top, for watching a log while you work in another one: the last rows, the counts, and Live on. It would also be a place for a tray icon that shows when an error arrives.

## 4. More JSON shapes

JSON Lines and arrays of objects are read today. More shapes are planned:

- **One object with an array inside**, such as `{"records":[...]}` and `{"value":[...]}` as the Graph API and Azure export logs.
- **Pretty-printed objects, one after the other**, without a newline between them.
- **More field names**, as people send logs that LogVice does not find a time or message in. Send a sample with the names changed.

Wishes are welcome: Help > Suggest a feature.
