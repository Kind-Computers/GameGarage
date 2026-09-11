# Game Garage

**A compact Windows utility suite from Kind Computers.** Choose a few checks,
watch their progress, and open the details when something needs attention.
Game Garage takes its inspiration from the quick, practical utilities of the
1980s. Its original working name was "Snortin' Utilities."

Use **Verify System** as a first check after changing a PC configuration,
including overclocking adjustments. Review the selected checks and their details
to decide what to investigate next. Comparing saved runs is planned for a future
version.

**Source is available now. The official prebuilt beta is pending manual
Windows checks and validation in a disposable Windows VM.** You can build and
test the source using the instructions below. The release acceptance record
remains pending until those checks are performed.

Game Garage 0.1 (Beta) targets **Windows 11 x64 with English-language Windows**. The
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

Build both Windows x64 packages with `scripts/build.ps1 -Package` as described
below. Once the beta is published, both downloads and `SHA256SUMS.txt` will
also be available from [Releases](https://github.com/Kind-Computers/GameGarage/releases).

| Download | How to use it |
| --- | --- |
| `GameGarage-0.1-win-x64-setup.exe` | Run the installer and accept elevation. It installs for all users under `Program Files\Game Garage` and adds a Start menu shortcut. Remove it through Windows Installed apps. |
| `GameGarage-0.1-win-x64-portable.zip` | Extract the complete ZIP onto a USB drive or another folder. Open `GameGarage\GameGarage.exe`; keep every file together and the drive connected while it runs. No installation is needed. |

Both packages include the same application and runtime. Opening the app requests
administrator access for its Windows system tools. Select tools and choose
**Verify System**; no sweep starts automatically. The tools check the Windows
computer where Game Garage is running.

**Verify System** starts the selected tools with a brief amber and coral pixel
effect. The compact interface uses warm arcade colors.
When the run ends, the summary explains what the selected checks reported and
suggests next steps for outstanding issues, incomplete checks, or pending repairs.
The individual results and diagnostic details remain available.

No separately installed .NET runtime is required. The beta is unsigned.
To check either download, run `Get-FileHash` on its filename with
`-Algorithm SHA256` and compare the result with its entry in `SHA256SUMS.txt`.

Repairs require confirmation. Drive optimization is selected initially and can
be deselected before choosing **Verify System**. It runs only when you
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
cancel a console run. `/Background` is not included in this beta.

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
The second also downloads the checksum-pinned NSIS 3.12 compiler into
`artifacts/toolchain` and creates the installer, self-contained portable ZIP,
and SHA-256 checksums under `artifacts/`. It does not install the app on your
computer. Tests use owned buffers, controlled subprocesses, and WPF fixtures;
they do not run broad RAM sweeps or Windows repair/maintenance commands.
Hosted Windows CI separately checks installation, reinstallation, removal,
and the portable layout in its disposable VM.

Read [CONTRIBUTING.md](CONTRIBUTING.md), the [roadmap](ROADMAP.md), and
[release instructions](docs/RELEASING.md). Report reproducible problems through
[GitHub Issues](https://github.com/Kind-Computers/GameGarage/issues). See
[SECURITY.md](SECURITY.md) for private security reporting.

Copyright (c) 2024-2026 Fredric Echols and Kind Computers, LLC.
Licensed under [MIT](LICENSE); bundled runtime notices are described in
[NOTICES.md](NOTICES.md).
