#!/usr/bin/env python3
"""EES 2.4 command brake -- a Claude Code PreToolUse hook (2026-11-09).

Why: the agents on a machine run under the same GitHub account as the person. Until the agents get their own
identity (EES 2.11, deferred), nothing on GitHub can tell an agent's approval or merge from the person's. This hook
stops irreversible and approval-like actions BEFORE they run (GOAL red lines R1, R3; AGENTS.md core rule 8).

Decisions (Claude Code hook protocol: JSON on stdout, exit 0):
  deny -- a person does this, not an agent: merge or approve a PR (also via `gh api`, GraphQL or auto-merge),
          push to main/master (also `HEAD`/`@` while on main), force push (`-f`, `+ref`, `--force*`), delete a
          remote branch, push tags / --all / --mirror, create a release, change repository settings or branch
          protection, kill processes by name.
  ask  -- destructive local git commands; the person decides each time.
  (no output) -- everything else.

The command is tokenised with shlex, so text inside a commit message or a grep pattern is never mistaken for a
command. `bash -c "..."`, `env`, `nohup`, `sudo`, `xargs`, `time` wrappers and `git -C <dir>` are followed.
Input that cannot be parsed passes (fail-open) so that the brake never stops ordinary work.
Enabled for everyone who opens the repository by .claude/settings.json (PreToolUse for SHELL_TOOLS and DENY_MCP;
the first python3/python that is 3.7+ is chosen before it runs, never re-run; a checkout without this file passes; output is ASCII).
Known limit: a person can switch hooks off locally -- this is a net against agent habit, not against intent.
"""
from __future__ import annotations

import json
import os
import re
import shlex
import subprocess
import sys

SEPARATORS = {";", "&&", "||", "|", "&", "(", ")", "|&", ";;"}
WRAPPERS = {"env", "nohup", "sudo", "time", "command", "exec", "nice", "stdbuf", "timeout"}
VALUE_OPTS = {"sudo": {"-u", "-g", "-h", "-p", "-C", "-D", "-r", "-t", "-U"}, "env": {"-u", "-C", "-S"},
              "xargs": {"-I", "-n", "-P", "-L", "-s", "-d", "-E", "-a"}, "nice": {"-n"}}
SHELLS = {"bash", "sh", "zsh", "dash"}
MAIN = {"main", "master"}
GH_MUTATIONS = re.compile(r"\b(mergePullRequest|addPullRequestReview|submitPullRequestReview|enablePullRequestAutoMerge|"
                          r"updateBranchProtectionRule|deleteBranchProtectionRule|createBranchProtectionRule|"
                          r"updateRepository|deleteRepository|archiveRepository)\b")
DENY_MCP = {"mcp__ccd_pr__set_auto_merge"}
# Every tool that runs a shell command from tool_input.command: the Bash tool, the desktop app's Terminal panel,
# the background Monitor and the Windows PowerShell tool. .claude/settings.json registers the hook for each of them
# (scripts/tests/test_command_brake.py keeps the two in step).
SHELL_TOOLS = {"Bash", "Monitor", "PowerShell", "mcp__terminal__run_in_terminal"}


def current_branch(cwd: str | None) -> str:
    try:
        out = subprocess.run(["git", "rev-parse", "--abbrev-ref", "HEAD"], cwd=cwd or None,
                             capture_output=True, text=True, timeout=5)
        return out.stdout.strip()
    except Exception:
        return ""


def segments(command: str) -> list[list[str]]:
    text = command.replace("\\\n", " ").replace("\n", " ; ")
    lex = shlex.shlex(text, posix=True, punctuation_chars=True)
    lex.whitespace_split = True
    out, cur, skip_next = [], [], False
    for tok in lex:
        if skip_next:
            skip_next = False
            continue
        if set(tok) <= set("<>&") and ("<" in tok or ">" in tok):   # redirection: drop it, its target, its fd
            if cur and cur[-1].isdigit():
                cur.pop()
            skip_next = not tok.endswith("&") or tok in (">&", "<&")
            if tok in (">&", "<&", "&>", "&>>", ">", ">>", "<", "<<", "<<<", ">|"):
                skip_next = True
            continue
        if tok in SEPARATORS:
            if cur:
                out.append(cur)
            cur = []
        else:
            cur.append(tok)
    if cur:
        out.append(cur)
    return out


def strip_wrappers(argv: list[str]) -> list[str]:
    while argv:
        head = os.path.basename(argv[0])
        if re.match(r"^[A-Za-z_][A-Za-z0-9_]*=", argv[0]):      # VAR=value prefix
            argv = argv[1:]
        elif head in WRAPPERS or head == "xargs":
            argv = argv[1:]
            while argv and argv[0].startswith("-"):                # wrapper options (and their values)
                takes_value = argv[0] in VALUE_OPTS.get(head, set())
                argv = argv[2:] if takes_value else argv[1:]
            if head == "timeout" and argv and re.match(r"^\d", argv[0]):
                argv = argv[1:]
        else:
            break
    return argv


class Brake:
    def __init__(self, cwd: str | None, branch: str | None):
        self.cwd, self.branch_override = cwd, branch

    def branch(self, cwd: str | None) -> str:
        if self.branch_override is not None:
            return self.branch_override
        return current_branch(cwd)

    # ---- git -------------------------------------------------------------------------------------------------
    def git(self, args: list[str], cwd: str | None):
        i = 0
        while i < len(args) and args[i].startswith("-"):
            if args[i] in ("-C", "-c", "--git-dir", "--work-tree", "--namespace") and i + 1 < len(args):
                if args[i] == "-c" and args[i + 1].startswith("alias."):
                    return "ask", "git takma adı (alias) tanımlayan komut ne yapacağını gizler."
                if args[i] == "-C":
                    cwd = os.path.join(cwd or os.getcwd(), args[i + 1])
                i += 2
            else:
                i += 1
        if i >= len(args):
            return None
        sub, rest = args[i], args[i + 1:]
        if sub == "push":
            return self.push(rest, cwd)
        if sub == "reset" and ("--hard" in rest or "--merge" in rest):
            return "ask", "git reset --hard kaydedilmemiş işi siler."
        if sub == "clean" and any(a == "--force" or (re.match(r"^-[a-zA-Z]+$", a) and "f" in a) for a in rest):
            return "ask", "git clean izlenmeyen dosyaları siler."
        if sub == "branch" and ("-D" in rest or ("--delete" in rest and "--force" in rest)):
            return "ask", "git branch -D birleşmemiş dalı siler."
        if sub in ("checkout", "switch") and any(a in ("-f", "--force", "--discard-changes") for a in rest):
            return "ask", "git " + sub + " zorlamalı geçiş kaydedilmemiş değişiklikleri siler."
        if sub in ("checkout", "restore") and "." in rest and "--staged" not in rest:
            return "ask", "git " + sub + " . kaydedilmemiş değişiklikleri siler."
        if sub == "stash" and rest[:1] and rest[0] in ("drop", "clear"):
            return "ask", "Saklanan değişiklikleri siler; başka oturumların da olabilir."
        return None

    def push(self, rest: list[str], cwd: str | None):
        positional, i = [], 0
        while i < len(rest):
            a = rest[i]
            if a in ("--force", "--force-if-includes") or a.startswith("--force-with-lease"):
                return "deny", "Zorla gönderme (force push) geri alınamaz (R1)."
            if a in ("--delete", "-d"):
                return "deny", "Uzak dalı silmek geri alınamaz (R1)."
            if a in ("--tags", "--follow-tags"):
                return "deny", "Etiket göndermek sürüm sayılır (R1)."
            if a in ("--all", "--mirror", "--branches"):
                return "deny", "Bütün dalları göndermek ana dalı da kapsar (R1)."
            if a in ("-o", "--push-option", "--repo", "--receive-pack", "--exec"):
                i += 2
                continue
            if re.match(r"^-[a-zA-Z]+$", a):
                if "f" in a:
                    return "deny", "Zorla gönderme (force push) geri alınamaz (R1)."
                if "d" in a:
                    return "deny", "Uzak dalı silmek geri alınamaz (R1)."
            elif not a.startswith("-"):
                positional.append(a)
            i += 1
        refspecs = positional[1:]
        if not refspecs:
            if self.branch(cwd) in MAIN:
                return "deny", "Ana daldayken git push ana dala gönderir; yasak (AGENTS.md çekirdek 8)."
            return None
        for spec in refspecs:
            if spec.startswith("+"):
                return "deny", "'+' önekli gönderme zorla göndermedir (R1)."
            if "{}" in spec or "$" in spec or "`" in spec:
                return "ask", "Gönderilecek dal komut çalışınca belli oluyor; ana dal olabilir."
            src, _, dst = spec.partition(":")
            if spec.startswith(":"):
                return "deny", "Uzak dalı silmek geri alınamaz (R1)."
            target = dst if dst else src
            if target in ("HEAD", "@"):
                target = self.branch(cwd)
            target = re.sub(r"^refs/heads/", "", target)
            if target in MAIN:
                return "deny", "Ana dala doğrudan gönderme yasak (AGENTS.md çekirdek 8)."
        return None

    # ---- gh --------------------------------------------------------------------------------------------------
    def gh(self, args: list[str]):
        i = 0
        while i < len(args) and args[i].startswith("-"):
            i += 2 if args[i] in ("-R", "--repo", "--hostname") else 1
        words = args[i:]
        if "--help" in words or "-h" in words:
            return None
        if words[:1] == ["secret"] and words[1:2] and words[1] in ("set", "delete", "remove"):
            return "deny", "Depo sırlarını değiştirmek bir insan kararıdır."
        if words[:2] == ["pr", "merge"]:
            return "deny", "PR birleştirmek: birleştirmeyi bir insan yapar (R1)."
        if words[:2] == ["pr", "review"] and any(
                w.startswith("--approve") or (re.match(r"^-[a-zA-Z]+$", w) and "a" in w) for w in words[2:]):
            return "deny", "PR onaylamak: onayı insan kendi hesabıyla verir (R3)."
        if words[:2] == ["release", "create"]:
            return "deny", "Sürüm oluşturmak bir insan kararıdır (R1, R2)."
        if words[:1] == ["repo"] and words[1:2] and words[1] in ("edit", "delete", "archive", "rename"):
            return "deny", "Depo ayarını değiştirmek bir insan kararıdır."
        if words[:1] == ["api"]:
            return self.gh_api(words[1:])
        return None

    def gh_api(self, args: list[str]):
        method, paths, payload = "GET", [], " ".join(args)
        i = 0
        while i < len(args):
            a = args[i]
            if a in ("-X", "--method") and i + 1 < len(args):
                method = args[i + 1].upper(); i += 2; continue
            if a.startswith("-X") and len(a) > 2:
                method = a[2:].upper(); i += 1; continue
            if a.startswith("--method="):
                method = a.split("=", 1)[1].upper(); i += 1; continue
            if not a.startswith("-"):
                paths.append(a)
            i += 1
        if re.search(r"query=@", payload) and any(p == "graphql" for p in paths):
            return "ask", "GraphQL isteği dosyadan geliyor; ne yaptığı görünmüyor."
        if GH_MUTATIONS.search(payload):
            return "deny", "API ile birleştirme, onay ya da koruma değişikliği bir insan işidir (R1, R3)."
        if re.search(r"event\s*=\s*approve", payload, re.I):
            return "deny", "API ile PR onaylamak: onayı insan verir (R3)."
        for p in paths:
            if re.search(r"/pulls/\d+/merge\b", p):
                return "deny", "API ile PR birleştirmek: birleştirmeyi bir insan yapar (R1)."
            if method != "GET" and re.search(r"/(branches/[^/]+/protection|rulesets)", p):
                return "deny", "Dal korumasını değiştirmek bir insan kararıdır."
            if method in ("PATCH", "DELETE", "PUT") and re.fullmatch(r"/?repos/[^/]+/[^/]+/?", p):
                return "deny", "Depo ayarını değiştirmek bir insan kararıdır."
            if re.search(r"/pulls/\d+/reviews/\d+/events", p) and method != "GET":
                return "deny", "API ile incelemeyi onaya çevirmek: onayı insan verir (R3)."
            if re.search(r"/pulls/\d+/reviews/?$", p) and method != "GET" and "--input" in args:
                return "deny", "Dosyadan gönderilen inceleme onay olabilir: onayı insan verir (R3)."
            if method != "GET" and re.search(r"/(collaborators|actions/permissions|actions/secrets|secrets|hooks|keys)\b", p):
                return "deny", "Depo erişimini ya da ayarlarını değiştirmek bir insan kararıdır."
            if method == "POST" and re.search(r"repos/[^/]+/[^/]+/merges/?$", p):
                return "deny", "API ile dalları birleştirmek bir insan işidir (R1)."
            if method != "GET" and re.search(r"/git/refs/", p):
                if method == "DELETE" or re.search(r"/git/refs/heads/(main|master)\b", p):
                    return "deny", "API ile dalı silmek ya da ana dalı değiştirmek geri alınamaz (R1)."
        return None

    # ---- one argv --------------------------------------------------------------------------------------------
    def argv(self, argv: list[str], depth: int = 0):
        argv = strip_wrappers(argv)
        if not argv:
            return None
        prog = os.path.basename(argv[0])
        if prog == "eval" and depth < 3:
            return self.command(" ".join(argv[1:]), depth + 1)
        if prog in SHELLS and depth < 3:
            for j, a in enumerate(argv[1:], 1):
                if a == "-c" or (re.match(r"^-[a-zA-Z]+$", a) and "c" in a):
                    if j + 1 < len(argv):
                        return self.command(argv[j + 1], depth + 1)
        if prog in ("killall", "pkill"):
            return "deny", "Adla süreç öldürmek başka işleri de durdurur; PID ile ve yalnız kendi sürecini durdur."
        if prog == "kill" and any("pgrep" in a for a in argv[1:]):
            return "deny", "Adla bulunan süreçleri öldürmek başka işleri de durdurur."
        if prog == "git":
            return self.git(argv[1:], self.cwd)
        if prog == "gh":
            return self.gh(argv[1:])
        return None

    def command(self, command: str, depth: int = 0):
        if re.search(r"\|\s*(?:sudo\s+)?(?:bash|sh|zsh|dash)\b(?!\s+\S+\.sh)|\b(?:bash|sh|zsh|dash)\s*<<<", command) and depth == 0:
            if re.search(r"\b(git|gh)\b", command):
                return "ask", "Kabuğa borudan verilen komut görünmüyor; ne yaptığını siz onaylayın."
        if re.search(r"\bkill\b[^;&|\n]*(\$\(|`)\s*pgrep\b", command):
            return "deny", "Adla bulunan süreçleri öldürmek başka işleri de durdurur."
        try:
            segs = segments(command)
        except ValueError:
            return None  # unbalanced quotes: let it through, the shell would fail anyway
        found = None
        for seg in segs:
            if seg and seg[0] == "cd" and len(seg) > 1:
                self.cwd = os.path.join(self.cwd or os.getcwd(), seg[1])
                continue
            v = self.argv(seg, depth)
            if v and v[0] == "deny":
                return v
            if v and found is None:
                found = v
        return found


def decide(command: str, cwd: str | None = None, branch: str | None = None):
    return Brake(cwd, branch).command(command)


def main() -> int:
    try:   # bytes in, decoded as UTF-8: a Windows ANSI code page (cp1252) must not change what is read
        data = json.loads(sys.stdin.buffer.read().decode("utf-8", "replace"))
    except Exception:
        return 0
    if not isinstance(data, dict):
        return 0
    tool = data.get("tool_name")
    if tool in DENY_MCP:
        verdict = ("deny", "Otomatik birleştirmeyi bir insan açar (R1).")
    elif tool in SHELL_TOOLS:
        tool_input = data.get("tool_input")
        tool_input = tool_input if isinstance(tool_input, dict) else {}
        verdict = decide(str(tool_input.get("command") or ""), tool_input.get("cwd") or data.get("cwd"))
    else:
        return 0
    if verdict is None:
        return 0
    decision, reason = verdict
    print(json.dumps({"hookSpecificOutput": {
        "hookEventName": "PreToolUse",
        "permissionDecision": decision,
        "permissionDecisionReason": "EES komut freni: " + reason,
    }}, ensure_ascii=True))   # ASCII only: the decision survives any stdout encoding (Codex R1-1, cp1252)
    return 0


if __name__ == "__main__":
    sys.exit(main())
