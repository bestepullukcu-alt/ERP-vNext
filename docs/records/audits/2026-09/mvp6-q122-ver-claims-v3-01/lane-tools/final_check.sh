#!/usr/bin/env bash
# Q122 end-of-lane read-only repo check (no git write; GIT_OPTIONAL_LOCKS=0). Output: stdout.
export GIT_OPTIONAL_LOCKS=0
cd /Users/natig/Projects/ERP-vNext-recovery || exit 2
echo "Q122 FINAL-CHECK $(date '+%Y-%m-%dT%H:%M:%S%z')"
echo "uname: $(uname -s)"
echo "branch: $(git branch --show-current)"
echo "HEAD: $(git rev-parse HEAD)"
if [ -e .git/index.lock ]; then echo "index.lock: PRESENT"; else echo "index.lock: absent"; fi
echo "diff --name-only count: $(git diff --name-only | wc -l | tr -d ' ')"
echo "diff --cached --name-only count: $(git diff --cached --name-only | wc -l | tr -d ' ')"
if git diff --check >/dev/null 2>&1; then echo "diff --check: clean"; else echo "diff --check: ISSUES"; fi
echo "status --porcelain entries: $(git status --porcelain | wc -l | tr -d ' ') (start 492; +1 this folder; +1 mvp6-q121b-build-test-01/ from the parallel Q121b lane)"
echo "new untracked folders since start:"
git status --porcelain docs/records/audits/2026-09 | grep -E 'q122|q121b' | sed 's/^/  /'
echo "diff paths:"
git diff --name-only | sed 's/^/  /'
echo "lane ports still listening (57192 57222 39994 5900 5901 5956 5957 5959 5961 5999):"
for p in 57192 57222 39994 5900 5901 5956 5957 5959 5961 5999; do lsof -nP -iTCP:$p -sTCP:LISTEN >/dev/null 2>&1 && echo "  $p LISTEN"; done
echo "  (end of list)"
echo "lane processes (dotnet / k00_supervisor / playwright chromium): $(pgrep -f 'dotnet|k00_supervisor|headless_shell|ms-playwright' | wc -l | tr -d ' ')"
echo "BASE folder mode: $(stat -f '%Sp' /Users/natig/mvp6-env/base/src)"
