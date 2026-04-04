"""
golden-log-generator.py
Full pipeline: splits a raw Neverwinter combat log into per-encounter files,
redacts player names and account handles, then runs the .NET GoldenFileGenerator
to produce the matching parsed-JSON files.

  1. Splits <input.log> on gaps >= 20 s into golden_logs/encounter-N.log
  2. Drops encounters with <= 10 non-blank lines (noise events)
  3. Redacts player identifiers:
       - Display names  -> "Player N"  (in all display fields and pet names)
       - Internal IDs   -> P[... Player N@playerN]  (name + handle both replaced)
  4. Runs: dotnet test <TestHarness.csproj> --filter GoldenFileGenerator
     which writes golden_logs_parsed/encounter-N-parsed.json

Usage:
    python golden-log-generator.py [input.log]

Default input: combatlog.log in the same directory as this script.
"""

import os
import re
import subprocess
import sys
from datetime import datetime

# ---------------------------------------------------------------------------
# Config
# ---------------------------------------------------------------------------

TS_RE = re.compile(r'^(\d{2}):(\d{2}):(\d{2}):(\d{2}):(\d{2}):(\d{2})\.(\d)')
# Captures (display_name, handle) from a player internal ID
PLAYER_ID_RE = re.compile(r'(P\[\d+@\d+ )(.+?)@([^\]]+)(\])')

GAP_SECONDS = 20
MIN_LINES = 10

SCRIPT_DIR   = os.path.dirname(os.path.abspath(__file__))
FIXTURES_DIR = SCRIPT_DIR
LOGS_DIR     = os.path.join(FIXTURES_DIR, 'golden_logs')
CSPROJ       = os.path.normpath(os.path.join(SCRIPT_DIR, '..', 'TestHarness.csproj'))

# ---------------------------------------------------------------------------
# Step 1: split
# ---------------------------------------------------------------------------

def parse_ts(line):
    m = TS_RE.match(line)
    if not m:
        return None
    yy, mo, dd, hh, mi, ss, f = m.groups()
    return datetime(2000 + int(yy), int(mo), int(dd),
                    int(hh), int(mi), int(ss), int(f) * 100000)


def split_log(src):
    print('Splitting {} ...'.format(src))
    encounters = []
    current = []
    last_ts = None

    with open(src, encoding='utf-8') as f:
        for line in f:
            line = line.rstrip('\r\n')
            ts = parse_ts(line)
            if ts is not None:
                if last_ts is not None and (ts - last_ts).total_seconds() >= GAP_SECONDS:
                    encounters.append(current)
                    current = []
                last_ts = ts
            current.append(line)

    if current:
        encounters.append(current)

    keepers = [e for e in encounters if len([l for l in e if l.strip()]) > MIN_LINES]
    print('Split into {} encounters.\n'.format(len(keepers)))
    return keepers

# ---------------------------------------------------------------------------
# Step 2: redact player names and handles
# ---------------------------------------------------------------------------

def collect_players(encounters):
    """Return {display_name: handle} for every unique player seen across all encounters."""
    players = {}
    for enc in encounters:
        for line in enc:
            for m in PLAYER_ID_RE.finditer(line):
                name, handle = m.group(2), m.group(3)
                players[name] = handle
    return players


def build_maps(players):
    """
    Returns (name_map, handle_map) where:
      name_map   : {original_name -> 'Player N'}  sorted longest-first for safe replacement
      handle_map : {original_handle -> 'playerN'}
    N is assigned by alphabetical sort of the original names for stability.
    """
    sorted_names = sorted(players.keys())
    name_map   = {}
    handle_map = {}
    for i, name in enumerate(sorted_names, 1):
        name_map[name]             = 'Player {}'.format(i)
        handle_map[players[name]]  = 'player{}'.format(i)

    # Sort name_map by length descending so longer names are replaced before
    # shorter ones that might be substrings (e.g. "Carlos o Bruxo" before "Carlos")
    name_items = sorted(name_map.items(), key=lambda kv: len(kv[0]), reverse=True)
    return name_items, handle_map


def make_redact_fn(name_items, handle_map):
    def replace_player_id(m):
        prefix = m.group(1)          # 'P[digits@digits '
        name   = m.group(2)          # display name
        handle = m.group(3)          # account handle (may contain #number)
        suffix = m.group(4)          # ']'
        new_name   = name_map_dict.get(name, name)
        new_handle = handle_map.get(handle, handle)
        return prefix + new_name + '@' + new_handle + suffix

    name_map_dict = dict(name_items)

    def redact_line(line):
        # 1. Replace inside P[...] internal IDs (name + handle)
        line = PLAYER_ID_RE.sub(replace_player_id, line)
        # 2. Replace bare display name occurrences (display fields, pet names, etc.)
        for original, redacted in name_items:
            line = line.replace(original, redacted)
        return line

    return redact_line


def redact_encounters(encounters):
    players   = collect_players(encounters)
    name_items, handle_map = build_maps(players)
    redact    = make_redact_fn(name_items, handle_map)

    print('Redacting {} player names and handles ...'.format(len(players)))
    return [[redact(line) for line in enc] for enc in encounters]

# ---------------------------------------------------------------------------
# Step 3: write encounter files
# ---------------------------------------------------------------------------

def write_encounters(encounters):
    os.makedirs(LOGS_DIR, exist_ok=True)
    os.makedirs(os.path.join(FIXTURES_DIR, 'golden_logs_parsed'), exist_ok=True)

    for old in os.listdir(LOGS_DIR):
        if re.match(r'encounter-\d+\.log', old):
            os.remove(os.path.join(LOGS_DIR, old))

    for i, enc in enumerate(encounters, 1):
        while enc and enc[0].strip() == '':
            enc.pop(0)
        path = os.path.join(LOGS_DIR, 'encounter-{}.log'.format(i))
        with open(path, 'w', encoding='utf-8', newline='\n') as out:
            out.write('\n'.join(enc) + '\n')
        n = len([l for l in enc if l.strip()])
        print('  encounter-{}.log: {} lines'.format(i, n))

    print()

# ---------------------------------------------------------------------------
# Step 4: generate parsed JSON via the .NET test
# ---------------------------------------------------------------------------

def run_generator():
    print('Running GoldenFileGenerator ...')
    cmd = ['dotnet', 'test', CSPROJ,
           '--filter', 'FullyQualifiedName~GoldenFileGenerator',
           '--no-build']
    result = subprocess.run(cmd, cwd=SCRIPT_DIR)
    if result.returncode != 0:
        print('\nERROR: dotnet test failed (exit {}).'.format(result.returncode))
        sys.exit(result.returncode)
    print('Done.')

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

if __name__ == '__main__':
    src = sys.argv[1] if len(sys.argv) > 1 else os.path.join(SCRIPT_DIR, 'combatlog.log')
    if not os.path.exists(src):
        print('ERROR: input log not found: ' + src)
        sys.exit(1)

    encounters = split_log(src)
    encounters = redact_encounters(encounters)
    write_encounters(encounters)
    run_generator()
