#!/usr/bin/env bash
# Stop, build, run.
#
# Two traps this guards against:
#  1. The desktop app holds its copy of MyBudget.UI.dll open. Building while it runs leaves that copy
#     stale and the app shows old markup with no error anywhere. So: stop first, then verify.
#  2. "dotnet run" is a wrapper process that launches the compiled binary. Killing only the binary
#     leaves the wrapper, and the next launch stacks a second instance on top of the first.
set -uo pipefail
cd "$(dirname "$0")"

# Kill by pattern, skipping this script and its ancestors so it can never kill itself.
stop() {
  local pattern=$1 pid
  for pid in $(pgrep -f -- "$pattern" 2>/dev/null); do
    case " $$ $PPID " in *" $pid "*) continue ;; esac
    kill "$pid" 2>/dev/null
  done
}

for attempt in 1 2 3; do
  stop 'dotnet run --project src/MyBudget.Desktop'
  stop 'net10.0/MyBudget.Desktop$'
  stop 'dotnet run --project src/MyBudget.Api'
  stop 'net10.0/MyBudget.Api$'
  sleep 1
  remaining=$(pgrep -fc 'net10.0/MyBudget.(Desktop|Api)$' 2>/dev/null || echo 0)
  [ "$remaining" = "0" ] && break
done
if pgrep -f 'net10.0/MyBudget.(Desktop|Api)$' >/dev/null 2>&1; then
  echo "could not stop existing processes:" >&2
  pgrep -af 'net10.0/MyBudget.(Desktop|Api)$' >&2
  exit 1
fi

dotnet build || exit 1

ui=$(stat -c %Y src/MyBudget.UI/bin/Debug/net10.0/MyBudget.UI.dll)
desktop=$(stat -c %Y src/MyBudget.Desktop/bin/Debug/net10.0/MyBudget.UI.dll)
if [ "$ui" != "$desktop" ]; then
  echo "STALE: the desktop copy of MyBudget.UI.dll did not refresh." >&2
  exit 1
fi
echo "assemblies match ($(date -d @"$ui" +%H:%M:%S))"

ASPNETCORE_ENVIRONMENT=Development nohup dotnet run --project src/MyBudget.Api --no-build --launch-profile http >/tmp/mybudget-api.log 2>&1 &
for _ in $(seq 1 60); do
  curl -sf -H "X-Api-Key: dev" http://localhost:5210/api/accounts >/dev/null 2>&1 && break
  sleep 1
done
curl -sf -H "X-Api-Key: dev" http://localhost:5210/api/accounts >/dev/null 2>&1 || { echo "api did not start; see /tmp/mybudget-api.log" >&2; exit 1; }
echo "api up"

nohup dotnet run --project src/MyBudget.Desktop --no-build >/tmp/mybudget-desktop.log 2>&1 &
sleep 8

# Exactly one of each, or say so rather than leaving duplicates behind.
for name in Desktop Api; do
  n=$(pgrep -fc "net10.0/MyBudget.$name\$" 2>/dev/null || echo 0)
  [ "$n" = "1" ] || { echo "expected 1 $name process, found $n" >&2; pgrep -af "net10.0/MyBudget.$name\$" >&2; exit 1; }
done
echo "one desktop, one api"
