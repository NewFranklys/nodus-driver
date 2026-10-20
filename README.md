# NODUS Driver

**NODUS Driver** is a small, free and open-source Windows utility for checking and installing compatible driver updates through Microsoft Windows Update.

The product goal is deliberately simple: **install it, open it, and it works.** No accounts, ads, subscriptions, telemetry, registry cleaners, "boosters", or third-party driver mirrors.

> Early preview: this project is being tested across different Windows 10/11 PCs and hardware configurations. Feedback is welcome.

## What it does

NODUS Driver currently targets **Windows 10 and Windows 11, x64**.

When opened, it automatically:

1. reads the PnP drivers installed on the PC;
2. asks the Windows Update Agent for driver updates applicable to that machine;
3. shows the compatible updates Windows reports;
4. lets the user choose and install them.

The preview currently requests administrator privileges when the app starts. This keeps the first public build simple and reliable while installation behavior is tested across real PCs.

## Safety model

- Discovery, download and installation are delegated to the Windows Update Agent.
- Installed-driver inventory is read locally with `Win32_PnPSignedDriver`.
- NODUS Driver does not scrape driver-download sites or install arbitrary vendor executables.
- NODUS Driver sends **no telemetry**.
- Logs stay on the PC in `%LOCALAPPDATA%\NODUS Driver\logs`.

## Downloads

Public release installers will be published on the repository's **Releases** page after the preview passes hardware testing.

Development builds are produced automatically by GitHub Actions on Windows as:

- a self-contained `win-x64` portable build;
- `NODUS-Driver-Setup.exe`.

Published self-contained builds do not require users to install the .NET runtime or SDK.

## Feedback

Different PCs are exactly what this preview needs. If something behaves differently on your machine, open a **Driver / hardware report** issue.

Useful information includes your Windows version, PC/motherboard/notebook model, CPU/GPU and the behavior you observed. **Do not post serial numbers, product keys, usernames, or other private information.**

## Scope

NODUS Driver intentionally does one job: **driver updates**.

No cleaner. No optimizer. No gaming boost. No bundled software.

## License

MIT.
