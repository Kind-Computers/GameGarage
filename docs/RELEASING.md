# Releasing Game Garage 0.1 (Beta)

Publish **0.1 (Beta)** from the independent public Game Garage repository using
tag `v0.1`. The publication manifest contains every permitted source path;
do not import existing branches, tags, Git metadata, or unrelated project
folders from the original workspace.

## Build and inspect

Run `scripts/build.ps1 -Package` from a clean checkout on Windows using the
pinned SDK. This restores, builds, executes bounded correctness harnesses,
audits the source and Git paths, and produces:

- `artifacts/GameGarage-0.1-win-x64-setup.exe`
- `artifacts/GameGarage-0.1-win-x64-portable.zip`
- `artifacts/SHA256SUMS.txt`, with one entry for each download.

The build downloads the checksum-pinned NSIS 3.12 compiler into the ignored
`artifacts/toolchain` directory. The installer and portable ZIP use the same
audited payload: WPF application, managed RAM worker, .NET runtime files,
MIT license, runtime licenses, and a per-file SHA-256 inventory. The ZIP has a
single `GameGarage/` root folder that can be extracted onto a USB drive or
another location. The installer defaults to `Program Files\Game Garage`,
registers removal in Windows Installed apps, and creates an all-users Start
menu shortcut. Both distributions require elevation when the app runs.

Publishing uses neither AOT, trimming, nor single-file bundling. The portable
ZIP uses sorted entries and fixed timestamps, and managed compilation is
deterministic. These controls reduce differences between builds; they do not
establish bit-for-bit identity across toolchains or operating environments.
NSIS installer byte identity between builds has not been measured. Both
downloads are unsigned until a signing certificate and process are available.

The package script publishes app and worker independently, retains the Windows
Desktop implementation of WindowsBase over the console compatibility facade,
rejects other conflicting shared dependencies, and checks required runtime
files. It invokes only worker help from a different working directory.
Building packages does not install Game Garage or start a RAM scan.

## Distribution checks in CI

After packaging, the workflows run `scripts/test-distributions.ps1` on a
disposable GitHub-hosted Windows Server 2025 runner. The script refuses to run
on a local developer machine. It checks the production installer, installed
payload hashes and metadata, worker help, same-version reinstallation,
removal, and portable extraction and relocation. It does not run the full
application, RAM sweeps, repairs, or optimization. Removal must preserve
unrelated files rather than deleting the installation directory recursively.

The script writes `artifacts/distribution-validation.json`. Record its
`installerLifecycle` result as `installerLifecycleSmoke` and its
`portableLayout` result as `portableDistributionSmoke` in
`docs/release-validation.json`, alongside the artifact hashes and detailed
results. Mark these fields true only after the corresponding checks pass for
the reviewed build. CI uploads both distributions, download checksums, and the
report for review. The release workflow repeats these checks before attaching
both downloads to GitHub.

## Acceptance evidence

Record real results in `docs/release-validation.json`. Pending fields deliberately
prevent tag publication. Hosted Windows Server checks do not establish Windows
11 interactive setup, UAC behavior, clean-machine operation, UI, or hardware
acceptance. Keep the four existing manual acceptance fields pending until
those checks are actually performed.

- On English Windows 11 x64, check interactive installation and elevation,
  launch from the Start menu, reinstall, and remove the installed app. Start
  the portable app from another working directory and from a USB drive;
  verify it finds its worker. Repeat launch checks on a clean machine without
  a separately installed .NET runtime.
- Check keyboard navigation, focus, 100%, 150%, and 200% display scaling,
  the **Verify System** button feedback, progress/details, cancellation, and
  repair confirmations. Confirm the summary and suggested next steps reflect
  the actual results, including incomplete checks and pending repairs. No checks
  should start automatically; drive optimization should initially be selected
  and can be deselected before choosing **Verify System**.
- In a disposable Windows test environment, exercise SFC, DISM, drive checks,
  requested repairs, and optimization started through **Verify System**. Verify
  scheduled reboot repairs are distinguished from completed repairs. Do not run
  maintenance on the developer machine as a substitute for this check.
- Review the final source, Git history, installer, and ZIP against the allowlist.
  Confirm only Windows Game Garage sources and approved runtime dependencies
  are included and that no credentials or unrelated original work are present.

Run the separate performance harness on an otherwise idle Windows machine:

```powershell
dotnet run -c Release --project benchmarks/GameGarage.RamBenchmarks -- --mib 2048 --threads 2 --runs 5 --output artifacts/ram-benchmark.json
```

The harness uses the original RAM kernel as its baseline, warms both versions,
then alternates complete baseline/candidate runs. Keep memory, thread count,
update passes, runtime, build settings, and allocation scope equal. Timings must
include allocation, initialization, all ten updates, candidate final verification,
and cleanup. Record the raw output and machine configuration. The median
candidate/baseline ratio must be **at most 1.10**, without reducing memory or
updates. Rerun noisy results; do not use CI timing as a hardware release gate.
Use a larger bounded memory budget or additional representative machines when
needed to distinguish cache effects from real RAM throughput.

Once affected checks pass, run `scripts/check-release-gate.ps1 -PrintFingerprint`
and record its output as `sourceSha256`. Changes to source, tests, benchmarks,
SDK, packaged documents, scripts, installer inputs, or workflows invalidate
that fingerprint and require reviewing and repeating affected checks. Complete
the evidence note with Windows build, CPU, installed RAM, scan budget/thread
count, raw timing location, distribution report, and manual checks actually
performed. Recording a fingerprint does not complete any pending check.

## Publish source

The reviewed source can be published to `Kind-Computers/GameGarage` while the
prebuilt beta's manual acceptance checks remain pending. Keep the pending
validation fields unchanged, enable Issues and private vulnerability reporting,
and push the source branch without creating the beta tag or a GitHub release.
Ordinary Windows CI builds and tests the source and distributions; that does
not complete the manual Windows or disposable-VM acceptance checks.

## Publish the beta

1. Commit the reviewed source and completed validation record. Enable GitHub
   Issues and private vulnerability reporting on `Kind-Computers/GameGarage`.
2. Run `scripts/check-release-gate.ps1` and `scripts/build.ps1 -Package` again;
   review the successful hosted distribution checks for that source.
3. Create and push the annotated tag `v0.1`.
4. The tag workflow checks recorded acceptance, rebuilds/tests/packages, checks
   the distributions, and creates **Game Garage 0.1 (Beta)** as a GitHub
   prerelease with the setup EXE, portable ZIP, checksums, and release notes.
5. Check both public downloads and their checksums before announcing the beta.

Only the release job has repository write permission. Ordinary pull requests
build and test with read-only permissions. Actions are pinned to reviewed
upstream commit IDs; Dependabot proposes updates.
