#!/usr/bin/env bash
# Build full catalogs (run from WSL). Usage:
#   tools/build_catalog.sh                     # Hipparcos + exoplanets -> data-out/
#   tools/build_catalog.sh --gaia-limit 10     # also Gaia DR3 down to G=10
# Then copy data-out/*.csv to %APPDATA%\AlienSkies\data, e.g.:
#   cp data-out/*.csv "/mnt/c/Users/$USER/AppData/Roaming/AlienSkies/data/"
set -euo pipefail
cd "$(dirname "$0")/.."

if [ ! -d .venv ]; then
  python3 -m venv .venv
  .venv/bin/pip install --quiet pyvo
fi
.venv/bin/python tools/build_catalog.py "$@"
