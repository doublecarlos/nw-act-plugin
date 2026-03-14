# CLAUDE.md — nw-act-plugin-dev

## Project Overview
This is a fork of the **Advanced Combat Tracker (ACT) plugin for Neverwinter Online**.
The plugin parses Neverwinter combat logs and feeds data into ACT for display.

The entire plugin lives in a **single standalone C# source file**: `Neverwinter.cs`.
ACT compiles this file at runtime — no build step is needed for the plugin itself.

Current version: **1.2.8.2** (see `[assembly: AssemblyVersion]` near top of file)

## ACT Plugin Constraints
ACT loads `.cs` plugin files by compiling them at runtime. This imposes strict constraints:

- **Target framework**: .NET 4.8 (net48)
- **C# language version**: must remain compatible with what ACT uses internally (treat as C# 5 / LangVersion 5 to be safe — no newer syntax like `var` patterns, tuples, etc.)
- **No NuGet packages** in the plugin file itself; only BCL types and the `Advanced_Combat_Tracker` assembly
- **Single-file**: `Neverwinter.cs` must remain self-contained; do not split it into multiple files

## Repository Structure
```
/
├── Neverwinter.cs              ← the plugin (the only deliverable)
├── README.md
├── CLAUDE.md                   ← this file
└── TestHarness/
    ├── TestHarness.csproj      (net48, LangVersion 5)
    ├── ActMocks.cs             (stub implementations of ACT interfaces)
    ├── ParserTests.cs          (NUnit unit tests — hand-crafted log snippets)
    ├── GoldenTests.cs          (NUnit regression tests — real encounter logs)
    ├── GoldenFileGenerator.cs  (NUnit test that writes golden_logs_parsed/*.json)
    └── fixtures/
        ├── sample.log                    (hand-crafted log snippets, one per scenario)
        ├── golden-log-generator.py       (splits combatlog.log and runs the .NET generator)
        ├── golden_logs/
        │   └── encounter-N.log           (real combat sessions split from combatlog.log)
        └── golden_logs_parsed/
            └── encounter-N-parsed.json   (expected damage_out totals per combatant)
```

### Fixture tooling
To regenerate **all** golden files from a new `combatlog.log`, run the full pipeline:
```
python golden-log-generator.py [input.log]
```
1. Splits `combatlog.log` into `golden_logs/encounter-N.log` on gaps ≥ 20 s (drops ≤ 10-line noise)
2. Runs the .NET `GoldenFileGenerator` test to write `golden_logs_parsed/encounter-N-parsed.json`

To regenerate only the parsed expectations (e.g. after a logic change, when the encounter logs already exist):
```
dotnet test TestHarness/ --filter "FullyQualifiedName~GoldenFileGenerator"
```

Default input: `combatlog.log` in the same directory.
`combatlog.log`, `golden_logs/`, and `golden_logs_parsed/` are all gitignored — run the script to regenerate them.

## Log Line Format
The Neverwinter combat log uses `::` and `,` as separators (split on `["::","," ]`).
Each line has exactly 13 fields after splitting:

```
[0]  timestamp   e.g. "13:07:09:11:01:08.4"
[1]  ownDsp      owner display name
[2]  ownInt      owner internal ID, e.g. "P[201028460@1546238 Correk@Gleyvien]"
[3]  srcDsp      source display name (or "" / "*")
[4]  srcInt      source internal ID  (or "" / "*")
[5]  tgtDsp      target display name
[6]  tgtInt      target internal ID
[7]  evtDsp      event/skill display name
[8]  evtInt      event/skill internal ID, e.g. "Pn.F1j0yx1"
[9]  type        e.g. "Physical", "HitPoints", "Power", "Shield", "AttribModExpire"
[10] flags       e.g. "Critical", "Flank", "Kill", "ShowPowerDisplayName", ""
[11] mag         damage/heal magnitude (float, en-US culture)
[12] magBase     base magnitude (float, en-US culture)
```

Timestamp format: `yy:MM:dd:HH:mm:ss.f` — the first 19 characters of every valid log line.
A valid line has `logLine[19] == ':'` and `logLine[20] == ':'`.

Entity type is determined from the internal ID prefix:
- `P[...]` → Player
- `C[... Pet_...]` → Pet
- `C[... Entity_...]` → Entity
- `C[...]` → Creature

`*` in src/tgt means "same as owner" (self-targeting).

## Architecture

The codebase has a clean two-layer split:

- **`NWCombatLogParser`** — pure parsing; zero ACT API calls; holds all mutable parsing state
- **`NW_Parser`** — thin ACT shell; applies `CombatAction` results to ACT's data model

### Log line flow

```
oFormActMain_BeforeLogLineRead()
  → new ParsedLine(logLine, time, ts)
  → combatLogParser.NormalizeFields(pl)      // field normalization, name resolution
  → combatLogParser.RouteAction(pl, inCombat, opts)  // returns ParseResult
  → logInfo.detectedType = pr.DetectedTypeColor
  → foreach CombatAction ca: ApplyCombatAction(ca)  // ACT calls happen here only
```

### `NW_Parser` (ACT shell)
- Implements `IActPluginV1` and `UserControl`
- `InitPlugin()` / `DeInitPlugin()` — register/unregister handlers, manage settings
- `oFormActMain_BeforeLogLineRead()` — validates line, drives the flow above
- `ApplyCombatAction(ca)` — the single place that calls `SetEncounter`, `AddCombatAction`, updates shield `MasterSwing` tags
- `FlushPendingShields(expired)` — submits unmatched shield entries on combat-end / log-file-change
- `pendingShieldMasterSwings` — `Dictionary<ShieldData, MasterSwing>` mapping pending shields to their already-submitted swing so tags can be updated when a damage match arrives
- `GetCurrentOptions()` — reads the three checkboxes into a `ParserOptions` snapshot

### `NWCombatLogParser` (pure parser)
- `NormalizeFields(line)` — parses raw fields, resolves pet/entity owner names
- `RouteAction(line, inCombat, opts)` — dispatches to one of six handlers, appends kill action, returns `ParseResult`
- Handlers (all return `HandlerResult`):
  - `HandleCleanse` — `AttribModExpire` lines
  - `HandleBuffsAndProcs` — `showPowerDisplayName` lines (non-damaging effects)
  - `HandlePower` — power replenishment
  - `HandleHeals` — `HitPoints` type
  - `HandleShields` — `Shield` type; enqueues `ShieldData` into `shieldQueue`
  - `HandleDamage` — everything else; calls `shieldQueue.MatchDamage()` to match/expire pending shields
- `ResetAll()` / `ResetOnCombatEnd()` — clear state, return any pending shields for flushing

### `ParsedLine`
- Parses raw log line into structured fields
- Handles edge case: >13 fields means a name has a comma → replace `", "` with `" "` and re-split
- Populates `kill`, `critical`, `flank`, `dodge`, `immune`, `showPowerDisplayName` from flags field

### Intermediate result types (no ACT dependencies)
- `ParserOptions` — snapshot of the three UI checkboxes (`MergeNPC`, `MergePets`, `FlankSkill`)
- `CombatAction` — everything needed for one `SetEncounter` + `AddCombatAction` pair; `RealDamage` and `BaseDamage` are floats, `new Dnum((int)Math.Round(ca.RealDamage))` is computed in the shell
- `ParseResult` — list of `CombatAction` + `DetectedTypeColor` + optional `SpellTimerTarget`
- `ShieldData` — plain log fields captured from a shield line (no ACT types)
- `ShieldMatchResult` — `MatchedShields` + `ExpiredShields` lists returned by `ShieldMatchingQueue.MatchDamage()`

### Shield matching
Shield lines arrive just before their corresponding damage line (≤ 500 ms). `HandleShields` enqueues a `ShieldData` and returns a `CombatAction` with `IsShield = true`; the shell submits it and stores `ShieldData → MasterSwing` in `pendingShieldMasterSwings`. `HandleDamage` calls `shieldQueue.MatchDamage()`: matched shields update the stored `MasterSwing` tags (`ShieldDmgF`, `ShieldP`); expired shields are returned as separate `CombatAction` objects (with `ExpiredShieldData` set) so the shell can remove them from the dict.

### Registeries
- `PetOwnerRegistery` — maps pet internal IDs → their player owner
- `EntityOwnerRegistery` — maps entity internal IDs → their player owner
- Held by `NWCombatLogParser`; used in name resolution to attribute pet/entity damage to the correct player

### Options (user-configurable checkboxes)
- `checkBox_mergeNPC` — strips unique IDs from NPC names to merge identical NPCs
- `checkBox_mergePets` — merges all pet data under the owner, removes pet from listing
- `checkBox_flankSkill` — splits skills into "Skill: Flank" vs "Skill" attack types

### Special constants
- `companionEntityPowers` — entity power IDs (Blue Fire Eye, Tutor) whose damage should NOT be merged into owner even with merging enabled
- `injuryTypes` — `internal static readonly`; power IDs for injuries that should not start combat or appear as damage; referenced by `NWCombatLogParser` as `NW_Parser.injuryTypes`
- `unk = "UNKNOWN"`, `unkInt = "C[0 Unknown]"` — ACT-magic strings; do not change; referenced by `NWCombatLogParser` as `NW_Parser.unk` / `NW_Parser.unkInt`

## Settings
Stored in XML at `%AppData%\...\neverwinter.config.xml` via ACT's `SettingsSerializer`.
Settings include the three checkboxes and the player name list (`listBox_players`).

## Test Suite
`TestHarness/` (net48, LangVersion 5) runs without ACT or a GUI:
- **`ActMocks.cs`** — stub implementations of ACT interfaces; defines the boundary between shell and parser
- **`ParserTests.cs`** — unit tests feeding hand-crafted log lines through `NWCombatLogParser` and asserting on `ParseResult.Actions`
- **`GoldenTests.cs`** — regression tests that parse full real encounter logs and compare `damage_out` totals per combatant against `golden_logs_parsed/encounter-N-parsed.json`
- **`GoldenFileGenerator.cs`** — one-off test (run manually) that writes the golden JSON expectations from the current code

## Branches & Git
- `main` — stable/release branch
- `develop` — active development branch (current)
- PRs go from feature branches → `develop` → `main`
