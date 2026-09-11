# Contributing

Open an issue describing the behavior you want to change, then send a focused
pull request. Include the Windows build, language, architecture, and a short
reproduction for bugs. Remove personal file paths or device identifiers from
logs before sharing them.

Install the SDK pinned in `global.json` and run `scripts/build.ps1` on Windows.
For packaging changes, also run `scripts/build.ps1 -Package`. Correctness tests
must use small owned buffers and controlled subprocesses; never run system
repairs, drive maintenance, or broad allocation in CI.

Keep UI strings in the English resources, machine-readable identifiers stable,
and numeric protocol parsing culture-independent. Put Windows operations behind
the existing platform boundary. Avoid new dependencies unless their purpose and
redistribution terms are clear. Additional OS support requires an explicit
platform implementation and tests; translation alone does not make localized
Windows command output supported.

Every new public source file must be added by exact repository-relative path to
`scripts/publication-allowlist.txt`. The publication audit rejects unexpected
files, binaries, excluded project material, and common secret formats. Review
that manifest when adding files rather than bypassing the audit.

For RAM changes, test against the scalar reference and inject corruption in
small buffers. Use the separate benchmark harness on an otherwise idle machine
before claiming speed or meeting the 10% performance gate. Do not reduce tested
memory or requested passes to make a comparison pass.

Describe validation in the pull request, including anything not run. Keep the
original copyright notices and contribute under the repository's MIT license.
