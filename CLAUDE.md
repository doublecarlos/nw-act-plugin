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
├── Neverwinter.cs          ← the plugin (the only deliverable)
├── README.md
├── CLAUDE.md               ← this file
└── TestHarness/            ← planned; does not exist yet
    ├── TestHarness.csproj  (net48, LangVersion 5)
    ├── ActMocks.cs         (stub implementations of ACT interfaces)
    ├── Tests.cs            (test cases)
    └── fixtures/
        ├── sample.log      (known combat log snippets)
        └── expected.json   (expected parse output snapshots)
```

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

## Key Classes & Flow

### `NW_Parser` (main plugin class)
- Implements `IActPluginV1` and `UserControl`
- `InitPlugin()` — registers event handlers, sets up ACT globals
- `DeInitPlugin()` — unregisters handlers, saves settings
- `oFormActMain_BeforeLogLineRead()` — entry point for each log line
  1. Basic validation (length, `::` at pos 19-20)
  2. Constructs `ParsedLine`
  3. Calls `ProcessBasic()` to normalize fields
  4. Calls `ProcessAction()` to route to specific handler

### `ParsedLine`
- Parses raw log line into structured fields
- Handles edge case: >13 fields means a name has a comma → replace `", "` with `" "` and re-split
- Populates `kill`, `critical`, `flank`, `dodge`, `immune`, `showPowerDisplayName` from flags field

### `ProcessAction()` — routes by `type` and flags:
- `"AttribModExpire"` → `ProcessActionCleanse()`
- `showPowerDisplayName == true` → `ProcessActionSPDN()` (non-damaging effects)
- `"Power"` → `ProcessActionPower()` (power replenishment)
- `"HitPoints"` → `ProcessActionHeals()`
- `"Shield"` → `ProcessActionShields()`
- everything else → `ProcessActionDamage()`

### Registeries
- `PetOwnerRegistery` — maps pet internal IDs → their player owner
- `EntityOwnerRegistery` — maps entity internal IDs → their player owner
- Used to attribute pet/entity damage to the correct player when merge options are enabled

### Options (user-configurable checkboxes)
- `checkBox_mergeNPC` — strips unique IDs from NPC names to merge identical NPCs
- `checkBox_mergePets` — merges all pet data under the owner, removes pet from listing
- `checkBox_flankSkill` — splits skills into "Skill: Flank" vs "Skill" attack types

### Special constants
- `companionEntityPowers` — entity power IDs (Blue Fire Eye, Tutor) whose damage should NOT be merged into owner even with merging enabled
- `injuryTypes` — internal power IDs for injuries that should not start combat or appear as damage
- `unk = "UNKNOWN"`, `unkInt = "C[0 Unknown]"` — these exact strings are recognized by ACT internals; do not change

## Settings
Stored in XML at `%AppData%\...\neverwinter.config.xml` via ACT's `SettingsSerializer`.
Settings include the three checkboxes and the player name list (`listBox_players`).

## Testing Goal
We want to add a `TestHarness/` project (net48, LangVersion 5) that:
- Mocks the ACT interfaces (`ActGlobals`, `LogLineEventArgs`, `MasterSwing`, etc.)
- Feeds known log lines through the parsing logic
- Asserts the resulting `MasterSwing` objects match expected snapshots
- Runs without ACT or a GUI — pure console or test runner

The plugin file itself should remain unchanged (or minimally changed) to keep it deployable as-is into ACT.

## Branches & Git
- `main` — stable/release branch
- `develop` — active development branch (current)
- PRs go from feature branches → `develop` → `main`
