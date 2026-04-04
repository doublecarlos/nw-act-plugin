# CLAUDE.md — nw-act-plugin-dev

## Project Overview
Fork of the **Advanced Combat Tracker (ACT) plugin for Neverwinter Online**.
Single deliverable: `Neverwinter.cs` — compiled at runtime by ACT, no build step needed.

## Hard Constraints (do not violate)
- **Single file**: `Neverwinter.cs` must stay self-contained. Do not split.
- **Target**: .NET 4.8 (net48), **C# LangVersion 5** — no tuples, var patterns, or newer syntax.
- **No NuGet** in the plugin; only BCL + `Advanced_Combat_Tracker` assembly.
- **`unk = "UNKNOWN"` and `unkInt = "C[0 Unknown]"`** are strings recognized by ACT internals — never rename or change these values.

## Repository Structure
```
/
├── Neverwinter.cs              ← the plugin (the only deliverable)
├── README.md
├── CLAUDE.md                   ← this file
└── TestHarness/
    ├── TestHarness.csproj      (net48, LangVersion 5)
    ├── ActMocks.cs             (stub ACT interfaces)
    ├── ParserTests.cs          (NUnit tests)
    └── fixtures/
        ├── sample.log                    (hand-crafted log snippets)
        ├── golden-log-generator.py       (regenerates golden files)
        ├── golden_logs/                  (gitignored)
        └── golden_logs_parsed/           (gitignored)
```

To regenerate golden fixtures:
```
cd TestHarness/fixtures
python golden-log-generator.py [input.log]
```

## Testing
`TestHarness/` runs without ACT or a GUI. It mocks ACT interfaces and feeds log lines
through the parser, asserting `MasterSwing` output matches snapshots.
The plugin itself stays unchanged so it deploys into ACT as-is.

## Branches & Git
- `main` — stable/release
- `develop` — active development (current branch)
- PRs: feature branches → `develop` → `main`
