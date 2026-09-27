#!/usr/bin/env bash
# Stop, build, run the desktop app. It hosts the API in-process against a local SQLite file, so
# there is no separate server to start.
#
# Two traps this guards against:
#  1. The desktop app holds its copy of MyBudget.UI.dll open. Building while it runs leaves that copy
#     stale and the app shows old markup with no error anywhere. So: stop first, then verify.
#  2. "dotnet run" is a wrapper process that launches the compiled binary. Killing only the binary
#     leaves the wrapper, and the next launch stacks a second instance on top of the first.
set -uo pipefail
cd "$(dirname "$0")"

# $2 is the signal. The app hosts Kestrel now, so a polite TERM can take a moment; the last
# attempt uses KILL rather than refusing to start over a process that is already going away.
stop() {
  local pattern=$1 signal=${2:-TERM} pid
  for pid in $(pgrep -f -- "$pattern" 2>/dev/null); do
    case " $$ $PPID " in *" $pid "*) continue ;; esac
    kill "-$signal" "$pid" 2>/dev/null
  done
}

for attempt in 1 2 3; do
  sig=TERM
  [ "$attempt" = "3" ] && sig=KILL
  stop 'dotnet run --project src/MyBudget.Desktop' "$sig"
  stop 'net10.0/MyBudget.Desktop$' "$sig"
  stop 'dotnet run --project src/MyBudget.Api' "$sig"
  stop 'net10.0/MyBudget.Api$' "$sig"
  sleep 2
  pgrep -f 'net10.0/MyBudget.(Desktop|Api)$' >/dev/null 2>&1 || break
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

nohup dotnet run --project src/MyBudget.Desktop --no-build >/tmp/mybudget-desktop.log 2>&1 &
for _ in $(seq 1 30); do
  grep -q "MyBudget: " /tmp/mybudget-desktop.log 2>/dev/null && break
  sleep 1
done

n=$(pgrep -fc 'net10.0/MyBudget.Desktop$' 2>/dev/null || echo 0)
if [ "$n" != "1" ]; then
  echo "expected 1 desktop process, found $n" >&2
  tail -20 /tmp/mybudget-desktop.log >&2
  exit 1
fi
grep -m1 "MyBudget: " /tmp/mybudget-desktop.log || echo "(database path not logged yet)"
echo "desktop up, api in-process"
