---
name: run
description: "Start, restart, or check the pos-tpv Blazor dev server on http://localhost:5209. Use whenever asked to run/start/restart the app, or to verify it's up before testing a UI change."
---

# Run pos-tpv dev server

CLAUDE.md's documented restart recipe kills the previous instance with
`pkill -f "dotnet run --project src/PosTpv.Web"`. **Do not run that via the Bash
tool as a single `bash -c "..."` script.** The wrapper shell's own process
cmdline contains the literal script text (it's passed through `eval '<script>'`),
which includes that exact `dotnet run --project src/PosTpv.Web` substring — so
`pkill -f` matches and kills the wrapper shell itself, before `dotnet run` ever
gets a chance to start. Symptom: the launch command exits with code 144
(signal 16), sometimes with no output written to the log at all.

## Safe start/restart procedure

1. Free the port instead of pattern-matching the process by name:
   ```bash
   fuser -k 5209/tcp 2>/dev/null; sleep 1
   ```
2. Launch with the env vars from CLAUDE.md, using the Bash tool's native
   `run_in_background: true` (not `nohup ... & disown`, which doesn't survive
   the tool call's sandbox teardown here):
   ```bash
   export PATH="$PATH:/root/.dotnet"
   cd /root/pos-tpv
   export Database__Provider=Sqlite
   export ConnectionStrings__Default="Data Source=postpv-dev.db"
   export ASPNETCORE_ENVIRONMENT=Development
   export ASPNETCORE_URLS="http://localhost:5209"
   export DOTNET_gcServer=0
   export DOTNET_GCHeapHardLimit=0x10000000
   dotnet run --project src/PosTpv.Web > /tmp/dotnet-run.log 2>&1
   ```
   Run this as one Bash call with `run_in_background: true`.
3. Poll for readiness instead of one fixed sleep — a fresh build can take
   10-20s:
   ```bash
   for i in $(seq 1 15); do
     sleep 2
     code=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:5209 --max-time 2)
     echo "t=$((i*2))s http=$code"
     { [ "$code" = "302" ] || [ "$code" = "200" ]; } && break
   done
   ```
   A `302` redirect to `/login` on `/` is the expected healthy response.
4. If it's still not up, read `/tmp/dotnet-run.log` for the real error
   (build failure, missing package, etc.) instead of re-guessing timing.

## Notes

- Blazor Server does not hot-reload `dotnet run`: any `.cs`/`.razor` edit
  requires repeating steps 1-3. `.razor.css`/`app.css`-only changes often
  reflect live without a restart.
- Don't kill the server when a task finishes unless the user asks — per
  CLAUDE.md, leave it running in the background.
