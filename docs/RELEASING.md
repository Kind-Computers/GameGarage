# Releasing the Windows preview

Release **v0.1.0-preview.1** from the independent public Game Garage repository.
The publication manifest contains every permitted source path; do not import
existing branches, tags, Git metadata, or unrelated project folders. The public
repository starts with fresh history.

## Build and inspect

Run `scripts/build.ps1 -Package` from a clean checkout on Windows using the
pinned SDK. This restores, builds, executes bounded correctness harnesses,
audits the source and Git paths, and produces:

- `artifacts/GameGarage-0.1.0-preview.1-win-x64.zip`
- `artifacts/SHA256SUMS.txt`

The ZIP includes the WPF application, managed RAM worker, .NET runtime files,
MIT license, runtime licenses, and a per-file SHA-256 inventory. Publishing
uses neither AOT, trimming, nor single-file bundling. Sorted archive entries
and fixed ZIP timestamps eliminate filesystem timestamps as a source of
archive differences; the pinned SDK/runtime and deterministic compilation
make repeated builds on the same toolchain comparable. This does not claim
bit-for-bit identity across different compiler/runtime or compression versions.

The package script publishes app and worker independently, retains the Windows Desktop implementation of WindowsBase over the console
compatibility facade, rejects other conflicting shared dependencies, and checks
required runtime files. It invokes only worker
help from a different working directory; it does not silently start a RAM scan.
The download is unsigned until a signing certificate and process are available.

## Acceptance evidence

Record real results in `docs/release-validation.json`. Pending fields deliberately
prevent tag publication. CI's Windows Server runner covers builds and fixtures;
it does not establish Windows 11 UI or hardware acceptance.

- On English Windows 11 x64, start the extracted app from another working
  directory and verify it finds its worker. Repeat on a clean machine without
  a separately installed .NET runtime.
- Check keyboard navigation, focus, 100%, 150%, and 200% display scaling,
  progress/details, cancellation, and repair confirmations. No checks should
  start automatically; drive optimization should initially be unchecked.
- In a disposable Windows test environment, exercise SFC, DISM, drive checks,
  requested repairs, and opt-in optimization. Verify scheduled reboot repairs
  are distinguished from completed repairs. Do not run maintenance on the
  developer machine as a substitute for this check.
- Review the final source, initial Git history, and ZIP against the allowlist.
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

Once all checks pass, run `scripts/check-release-gate.ps1 -PrintFingerprint`
and record its output as `sourceSha256`. Any subsequent source, test, benchmark,
SDK, or packaging change invalidates that fingerprint and requires reviewing
and repeating affected checks. Complete the evidence note with Windows build,
CPU, installed RAM, scan budget/thread count, raw timing location, and the
manual checks actually performed.

## Publish source

The reviewed source can be published to `Kind-Computers/GameGarage` while the
prebuilt preview's manual acceptance checks remain pending. Keep the pending
validation fields unchanged, enable Issues and private vulnerability reporting,
and push the source branch without creating the preview tag or a GitHub release.
Ordinary Windows CI builds and tests the source; that does not complete the
manual Windows or disposable-VM acceptance checks.

## Publish the preview

1. Commit the reviewed source and completed validation record. Enable GitHub
   Issues and private vulnerability reporting on `Kind-Computers/GameGarage`.
2. Run `scripts/check-release-gate.ps1` and `scripts/build.ps1 -Package` again.
3. Create and push the annotated tag `v0.1.0-preview.1`.
4. The tag workflow checks recorded acceptance, rebuilds/tests/packages, and
   creates a GitHub prerelease with the ZIP, checksums, and prepared release notes.
5. Check the public download and checksum before announcing the preview.

Only the release job has repository write permission. Ordinary pull requests
build and test with read-only permissions. Actions are pinned to reviewed
upstream commit IDs; Dependabot proposes updates.
