#!/usr/bin/env bash
set -euo pipefail

BUILD_INFO="Models/BuildInfo.cs"

if [[ ! -f "$BUILD_INFO" ]]; then
  echo "Build info file not found: $BUILD_INFO" >&2
  exit 1
fi

current="$(sed -nE 's/^[[:space:]]*public const int BuildNumber = ([0-9]+);/\1/p' "$BUILD_INFO" | head -n 1)"
if [[ -z "$current" ]]; then
  echo "Could not read BuildNumber from $BUILD_INFO" >&2
  exit 1
fi

next=$((current + 1))

python - "$BUILD_INFO" "$next" <<'PY'
from pathlib import Path
import re
import sys

path = Path(sys.argv[1])
number = int(sys.argv[2])
text = path.read_text(encoding="utf-8")
updated, count = re.subn(
    r"public const int BuildNumber = \d+;",
    f"public const int BuildNumber = {number};",
    text,
    count=1,
)
if count != 1:
    raise SystemExit(f"Could not update BuildNumber in {path}")
path.write_text(updated, encoding="utf-8", newline="\n")
PY

printf 'CosmoNote build: v%05d\n' "$next"
