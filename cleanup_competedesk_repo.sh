#!/usr/bin/env bash
set -euo pipefail
ROOT="${1:-.}"
cd "$ROOT"
rm -rf "CompeteDesk.Web/CompeteDesk.Data" "CompeteDesk.Web/CompeteDesk.Domain"
rm -f "CompeteDesk.Web/app.db"
echo "Cleanup complete. Removed placeholder folders and duplicate root database file."
