#!/usr/bin/env bash
# Stop, build, run. The desktop app holds its copy of MyBudget.UI.dll open, so a build while it is
# running leaves that copy stale and the app shows old markup with no error anywhere.
set -euo pipefail
cd "$(dirname "$0")"

pkill -f '[n]et10.0/MyBudget.Desktop$' 2>/dev/null || true
pkill -f 'MyBudget[.]Api' 2>/dev/null || true
sleep 2

dotnet build

ui=$(stat -c %Y src/MyBudget.UI/bin/Debug/net10.0/MyBudget.UI.dll)
desktop=$(stat -c %Y src/MyBudget.Desktop/bin/Debug/net10.0/MyBudget.UI.dll)
if [ "$ui" != "$desktop" ]; then
  echo "STALE: the desktop copy of MyBudget.UI.dll did not refresh. Something still had it open." >&2
  exit 1
fi
echo "assemblies match ($(date -d @"$ui" +%H:%M:%S))"

ASPNETCORE_ENVIRONMENT=Development nohup dotnet run --project src/MyBudget.Api --no-build --launch-profile http >/tmp/mybudget-api.log 2>&1 &
until curl -sf -H "X-Api-Key: dev" http://localhost:5210/api/accounts >/dev/null 2>&1; do sleep 1; done
echo "api up"

nohup dotnet run --project src/MyBudget.Desktop --no-build >/tmp/mybudget-desktop.log 2>&1 &
sleep 8
pgrep -f '[n]et10.0/MyBudget.Desktop$' >/dev/null && echo "desktop up" || { echo "desktop failed; see /tmp/mybudget-desktop.log" >&2; exit 1; }
