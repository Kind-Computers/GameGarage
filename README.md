# Game Garage

**A compact Windows utility suite from Kind Computers.** Choose a few checks,
watch their progress, and open the details when something needs attention.
Game Garage takes its inspiration from the quick, practical utilities of the
1980s. Its original working name was "Snortin' Utilities."

**Source is available now. The official prebuilt preview is pending manual
Windows checks and validation in a disposable Windows VM.** You can build and
test the source using the instructions below. The release acceptance record
remains pending until those checks are performed.

This preview targets **Windows 11 x64 with English-language Windows**. The
Windows interface and native memory allocation are platform-specific; small
shared diagnostic contracts and English string resources leave room for future
platforms and translations.

## Tools

| Tool | What it does |
| --- | --- |
| RAM / CPU data-path check | Writes and verifies patterns in memory owned by the worker process. |
| Windows file integrity | Runs Windows System File Checker (SFC). |
| Windows image integrity | Uses DISM to inspect the Windows component store, with an optional repair. |
| Drive integrity | Uses CHKDSK; requested repairs may need a restart. |
| Driver signature review | Reports signature information returned by Windows DRIVERQUERY. |
| Drive optimization | Runs Windows DEFRAG /O maintenance for supported drives; selected initially and can be deselected. |

Windows file integrity uses SFC's protection and repair mechanisms; Game Garage
does not maintain its own reference file hashes. RAM results cover the tested
CPU/memory data path during that run. A pass is not hardware certification, and
a mismatch does not identify a particular physical DIMM or component.

## Run a packaged build

1. Build the Windows x64 ZIP with `scripts/build.ps1 -Package` as described below.
   Once the preview is published, its ZIP and `SHA256SUMS.txt` will also be
   available from [Releases](https://github.com/Kind-Computers/GameGarage/releases).
2. Extract the entire ZIP into one folder. Keep `StabilityTest.exe` and all
   accompanying files beside `GameGarage.exe`.
3. Open `GameGarage.exe` and accept the Windows elevation prompt. The suite
   requires administrator access for its Windows system tools.
4. Select tools and choose **Run selected tools**. No sweep starts automatically.

No separately installed .NET runtime is required. The preview is unsigned.
To check a download, compare `Get-FileHash .\GameGarage-0.1.0-preview.1-win-x64.zip
-Algorithm SHA256` with the corresponding entry in `SHA256SUMS.txt`.

Repairs require confirmation. Drive optimization is selected initially and can
be deselected before choosing **Run selected tools**. It runs only when you
start the selected tools; nothing runs automatically at startup. Cancelling a
run prevents later tools from starting.
Cancellation sends a cooperative Ctrl+Break request to the running Windows tool
and waits for it to exit. Game Garage never force-kills the tool. Details
distinguish cancellation, incomplete coverage,
execution failures, detected issues, completed repairs, and repairs scheduled
for reboot.

## RAM scan

The current algorithm identifier is `xor-address-v1`. The worker performs ten
update passes by default, followed by a final read-only
verification. Each word has a position-dependent expected value; seeded patterns
and their complements exercise both bit states. The fastest supported kernel
handles most memory, with small deterministic portions exercising other supported
scalar/SIMD implementations.

The default scan uses approximately half the logical processors and allocates
available memory while reserving 1 GiB for other work. It can consume substantial
RAM and CPU time. Memory covered by this process is not the same as all installed
physical memory. Incomplete or released coverage is reported explicitly.

For a repeatable command-line run:

```powershell
.\StabilityTest.exe /Passes 10 /ReserveGB 2 /Threads 50% /Seed 12345
```

`/Passes` takes a positive integer. `/ReserveGB` accepts a whole GiB count or a
percentage of installed memory; `/Threads` accepts a count or a percentage of
logical processors. `/Seed` accepts a signed 32-bit integer. Omit the seed to
choose one for the run; the worker records it with the algorithm version,
coverage, and results. Use `--help` for current argument details and Ctrl+C to
cancel a console run. `/Background` is not included in this preview.

Worker exit codes: `0` passed, `1` detected issues, `2` execution error,
`3` cancelled, `4` incomplete/inconclusive. An empty scan cannot pass.

The performance acceptance target is at most 10% median end-to-end slowdown
against the original scan at equal memory, thread, and update-pass counts.
This is a release gate to measure on hardware, not a claim that every machine
will meet it. See [release validation](docs/RELEASING.md).

## Build and contribute

Install the SDK pinned in `global.json`, Git, and Windows PowerShell 5.1 or later.
From a Windows checkout:

```powershell
.\scripts\build.ps1
.\scripts\build.ps1 -Package
```

The first command restores, builds, and runs bounded console test harnesses.
The second also creates the self-contained ZIP and SHA-256 checksums under
`artifacts/`. Tests use owned buffers, controlled subprocesses, and WPF fixtures; they do not
run broad RAM sweeps or Windows repair/maintenance commands.

Read [CONTRIBUTING.md](CONTRIBUTING.md), the [roadmap](ROADMAP.md), and
[release instructions](docs/RELEASING.md). Report reproducible problems through
[GitHub Issues](https://github.com/Kind-Computers/GameGarage/issues). See
[SECURITY.md](SECURITY.md) for private security reporting.

Copyright (c) 2024-2026 Fredric Echols and Kind Computers, LLC.
Licensed under [MIT](LICENSE); bundled runtime notices are described in
[NOTICES.md](NOTICES.md).
