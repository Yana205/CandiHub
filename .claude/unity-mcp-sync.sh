#!/usr/bin/env bash
# SessionStart hook: keep the `gamedev-mcp` MCP server pointed at Unity's
# currently-running gamedev-mcp-server. Unity picks a fresh TCP port on each
# Editor launch, so the hard-coded URL in Claude's config goes stale. This
# detects the live port and rewrites the project's local-scope MCP config only
# when it changed. If Unity isn't running, the config is left untouched.
#
# Note: MCP servers connect when Claude starts, so a port change fixed here
# takes effect on the NEXT session or after `/mcp` reconnect in this one.
set -uo pipefail

PROJECT_DIR="/Users/yanai/Desktop/unitygame/candihub"
SERVER="gamedev-mcp"

# 1. Find the running Unity gamedev-mcp-server (pgrep excludes itself).
pid="$(pgrep -f 'gamedev-mcp-server' 2>/dev/null | head -n1)"
[ -n "${pid:-}" ] || exit 0

# 2. Extract its port= launch argument.
port="$(ps -o args= -p "$pid" 2>/dev/null | grep -o 'port=[0-9][0-9]*' | head -n1 | cut -d= -f2)"
[ -n "${port:-}" ] || exit 0
new_url="http://127.0.0.1:${port}/mcp"

# 3. Read the URL currently configured for this project (local scope lives in
#    ~/.claude.json under projects[dir].mcpServers).
cur_url="$(python3 - "$PROJECT_DIR" "$SERVER" <<'PY'
import json, os, sys
proj, srv = sys.argv[1], sys.argv[2]
try:
    cfg = json.load(open(os.path.expanduser("~/.claude.json")))
    entry = cfg.get("projects", {}).get(proj, {}).get("mcpServers", {}).get(srv, {})
    print(entry.get("url", "") if isinstance(entry, dict) else "")
except Exception:
    print("")
PY
)"

# 4. Nothing to do if already correct.
[ "$cur_url" = "$new_url" ] && exit 0

# 5. Rewrite via the supported CLI (safe read-modify-write of ~/.claude.json).
cd "$PROJECT_DIR" || exit 0
claude mcp remove "$SERVER" -s local >/dev/null 2>&1
if claude mcp add --transport http "$SERVER" "$new_url" -s local >/dev/null 2>&1; then
  printf '{"systemMessage":"Unity gamedev-mcp port is now %s (was %s). MCP config updated \\u2014 run /mcp to reconnect this session."}\n' \
    "$port" "${cur_url:-unset}"
fi
exit 0
