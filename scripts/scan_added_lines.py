#!/usr/bin/env python3
"""Added-lines scan - a WARNING-ONLY check of what a pull request adds (the repository is public).

What it checks: secret shapes (tokens, keys, passwords in connection strings / URLs / assignments), risky new file names
(.env, key / certificate, .log, .trx), personal folder paths (/Users/<name>/, /home/<name>/, C:\\Users\\<name>), e-mail
addresses outside an allow-list and - ONLY when a list of names is supplied from outside the repository - personal
names. It never blocks: the exit code is always 0.

    python3 scripts/scan_added_lines.py --base <sha> --head <sha> [--repo .] [--summary file.md] [--names-file <path>]

Names (optional): the environment variable EES_SCAN_NAMES (entries separated by newlines or commas) and / or
--names-file <path> (one or more entries per line, same separators). A names file inside the repository is refused
(the list must never be committed). Entries shorter than 3 letters / digits are ignored. Matching is on whole tokens,
case-insensitive, Turkish letters folded on both sides (ı/İ->i, ş->s, ğ->g, ü->u, ö->o, ç->c); a line is split into
tokens at every non-letter / non-digit character (., _, -, /, @, spaces ...), at camelCase boundaries and between
letters and digits, so `ad.soyad`, `ad_soyad`, `adSoyad`, an e-mail local part or a path segment are found, but a name
inside a longer word is not. A multi-word entry matches the same tokens in a row. A name finding shows only path, line
and a masked length - never the matched text, the list or its size - and is a warning in test files too. Without a list
the scan prints one line saying the name check did NOT run.

Scope: only what the pull request ADDS. `git diff --no-renames -U0 base...head` is read; the added lines and the names
of newly added files are scanned, never the content already on the base. Two noise cuts keep moved content quiet
(--no-renames shows a move as delete + add): an added line whose exact text is also removed somewhere in the same diff,
and an added file whose blob equals a deleted file's blob, are not reported.

Output: GitHub annotations `::warning file=<path>,line=<n>::<Turkish message>` (or `::notice`) and a summary. A finding
carries ONLY the path, the line, the kind and a masked hint ("uzunluk 32"): the matched value is never printed, logged
or written, not even partly. Exit code is ALWAYS 0: a git failure (unreachable base, shallow clone) or an internal error
prints a warning that the scan could not run - this check never blocks a pull request.

Kinds:
  secrets   private-key (PEM header), github-token, aws-key, slack-token, google-api-key, stripe-live-key,
            anthropic-key, jwt, conn-password (`Password=` / `Pwd=` in a connection string), url-credentials
            (`scheme://user:pass@`), secret-assignment (a quoted value of >= 8 chars assigned to a name containing
            secret / token / password / pwd / apikey)
  files     env-file (.env, not .env.example/.sample/.template), key-file (*.pfx, *.p12, *.pem, *.key), log-file (*.log),
            trx-file (*.trx) - newly added files only
  names     home-path (/Users/<name>/, /home/<name>/, C:\\Users\\<name> starting a token; user / runner / someone
            allowed), email-freemail, email (any domain outside the allow-list: *.test, *.invalid, *.example,
            *.localhost, *.local, example.*, noreply addresses, the product dev domains diten.com / diten.local)
Allow-list (noise): hex digests (40 / 64), identifier-shaped values (CamelCase, UPPER_SNAKE, dotted / kebab keys),
references (${, $(, {{, {name}, <...>, $VAR, %VAR%, Configuration[, Environment., process.env), prose (a value with spaces),
paths / plain URLs, masked values (***), placeholder words (pass, password, secret, changeme).
Vendored paths are skipped (wwwroot/lib/**, frontend/_Reference/**, wwwroot/assets/json/*).
  person-name  a token sequence equal to an entry of the supplied names list (see "Names" above)
Downgraded to notice: password / secret literals in appsettings*.Development.json on a line with a development / local
marker; password / secret literals, home paths and e-mail addresses in a test location (a tests / test / fixtures
directory, a path segment ending in Tests or .Tests, a file named test_*.py, *Tests.cs, *.test.* or *.spec.*) - test
fixtures use fake ones on purpose. Provider-shaped secrets and person-name findings keep the warning level everywhere.
Stdlib only. Tests: scripts/tests/test_scan_added_lines.py.
"""

from __future__ import annotations

import argparse
import os
import re
import subprocess
import sys
import unicodedata
from collections import Counter
from dataclasses import dataclass

WARNING = "warning"
NOTICE = "notice"
MAX_ANNOTATIONS = 10
MAX_SUMMARY_ROWS = 300

MESSAGES = {
    "private-key": "Özel anahtar başlığı (PEM)",
    "github-token": "GitHub erişim belirteci biçimi",
    "aws-key": "AWS erişim anahtarı biçimi",
    "slack-token": "Slack belirteci biçimi",
    "google-api-key": "Google API anahtarı biçimi",
    "stripe-live-key": "Stripe canlı anahtar biçimi",
    "anthropic-key": "Anthropic API anahtarı biçimi",
    "jwt": "JWT biçiminde belirteç",
    "conn-password": "Bağlantı dizesinde parola",
    "url-credentials": "URL içinde kullanıcı adı ve parola",
    "secret-assignment": "Gizli değer adına tırnaklı atama",
    "env-file": "Yeni .env dosyası",
    "key-file": "Yeni anahtar / sertifika dosyası",
    "log-file": "Yeni .log dosyası",
    "trx-file": "Yeni .trx test sonuç dosyası",
    "home-path": "Mutlak ev dizini yolu (kişisel kullanıcı adı)",
    "email-freemail": "Ücretsiz posta sağlayıcısında e-posta adresi",
    "email": "İzin listesinde olmayan alan adında e-posta adresi",
    "person-name": "Kişi adı (depo dışından verilen listeden)",
}
NAME_KIND = "person-name"  # not a FAMILIES entry: its "pattern" is the supplied list (NameMatcher)

# ------------------------------------------------------------------------------------------------ pattern families
# Each family maps a kind to its regexes; group "v" (when present) is the value the allow-list and the length hint read.
# Tests switch a family off (empty list) to prove its test turns red (sabotage).
FAMILIES: dict[str, list[re.Pattern[str]]] = {
    "private-key": [re.compile(r"-----BEGIN (?:[A-Z0-9]+ )*PRIVATE KEY(?: BLOCK)?-----")],
    "github-token": [
        re.compile(r"(?<![A-Za-z0-9_])(?P<v>gh[pousr]_[A-Za-z0-9]{36,255})"),
        re.compile(r"(?<![A-Za-z0-9_])(?P<v>github_pat_[A-Za-z0-9_]{22,255})"),
    ],
    "aws-key": [re.compile(r"(?<![A-Za-z0-9])(?P<v>(?:AKIA|ASIA)[A-Z0-9]{16})(?![A-Za-z0-9])")],
    "slack-token": [re.compile(r"(?<![A-Za-z0-9])(?P<v>xox[abprs]-[A-Za-z0-9-]{10,255})")],
    "google-api-key": [re.compile(r"(?<![A-Za-z0-9])(?P<v>AIza[0-9A-Za-z_-]{35})")],
    "stripe-live-key": [re.compile(r"(?<![A-Za-z0-9])(?P<v>sk_live_[0-9A-Za-z]{10,255})")],
    "anthropic-key": [re.compile(r"(?<![A-Za-z0-9])(?P<v>sk-ant-[A-Za-z0-9_-]{20,255})")],
    "jwt": [re.compile(r"(?<![A-Za-z0-9_-])(?P<v>eyJ[A-Za-z0-9_-]{10,1024}\.[A-Za-z0-9_-]{10,2048}\.[A-Za-z0-9_-]{10,1024})")],
    # A connection string: `Password=` / `Pwd=` (no spaces) after a separator, on a line that has another `key=...;`.
    "conn-password": [re.compile(r"(?i)(?:^|[;\"'\s])(?:password|pwd)=(?P<v>[^;\"'\s]{1,256})")],
    "url-credentials": [
        re.compile(r"(?i)(?<![A-Za-z0-9+.-])[a-z][a-z0-9+.-]{1,20}://[^\s:/@\"'<>]{1,128}:(?P<v>[^\s/@\"'<>]{1,256})@")
    ],
    "secret-assignment": [
        re.compile(
            r"(?i)(?<![A-Za-z0-9])[\w.-]{0,64}(?:secret|token|passw(?:or)?d|pwd|api[_-]?key)[\w.-]{0,64}[\"']?"
            r"[ \t]{0,8}(?::=|=>|:|=)[ \t]{0,8}[@$]?(?P<q>[\"'])(?P<v>[^\"'\r\n]{8,512}?)(?P=q)"
        )
    ],
    "home-path": [
        re.compile(r"(?<![\w./-])/Users/(?P<v>[A-Za-z0-9_][A-Za-z0-9._-]{0,63})/"),
        re.compile(r"(?<![\w./-])/home/(?P<v>[A-Za-z0-9_][A-Za-z0-9._-]{0,63})/"),
        re.compile(r"(?<![\w./\\-])[A-Za-z]:(?:\\{1,2}|/)Users(?:\\{1,2}|/)(?P<v>[A-Za-z0-9_][A-Za-z0-9._-]{0,63})"),
    ],
    "email": [re.compile(r"(?<![\w.%+-])(?P<local>[A-Za-z0-9._%+-]{1,64})@(?P<v>(?:[A-Za-z0-9-]{1,63}\.){1,8}[A-Za-z]{2,24})(?![\w-])")],
    # Newly added files, by name.
    "env-file": [re.compile(r"(?i)(?:^|/)\.env(?:\.(?!(?:example|sample|template)$)[^/]+)?$")],
    "key-file": [re.compile(r"(?i)\.(?:pfx|p12|pem|key)$")],
    "log-file": [re.compile(r"(?i)\.log$")],
    "trx-file": [re.compile(r"(?i)\.trx$")],
}
FILE_KINDS = ("env-file", "key-file", "log-file", "trx-file")
# A connection-string line: some `key=value;` (bounded, so a long line without ";" stays linear).
CONN_GUARD = re.compile(r"\w=[^;]{0,256};")
MAX_LINE = 4096  # characters scanned per added line; the rest is cut and counted in the summary

SECRET_KINDS = ("private-key", "github-token", "aws-key", "slack-token", "google-api-key", "stripe-live-key",
                "anthropic-key", "jwt", "conn-password", "url-credentials", "secret-assignment")
PROVIDER_KINDS = ("private-key", "github-token", "aws-key", "slack-token", "google-api-key", "stripe-live-key",
                  "anthropic-key", "jwt")
LITERAL_KINDS = ("conn-password", "url-credentials", "secret-assignment")  # downgradable password / secret literals
FIXTURE_KINDS = ("home-path", "email", "email-freemail")  # fake ones are deliberate test data: notice in test locations

ALLOWED_HOME_NAMES = {"user", "runner", "someone"}
FREEMAIL_LABELS = {"gmail", "googlemail", "outlook", "hotmail", "yahoo", "ymail", "icloud", "proton", "protonmail",
                   "aol", "gmx", "yandex", "zoho", "tutanota"}
FREEMAIL_DOMAINS = {"live.com", "msn.com", "me.com", "mac.com", "pm.me", "mail.ru", "mail.com"}
ALLOWED_EMAIL_TLDS = {"test", "invalid", "example", "localhost", "local"}
ALLOWED_EMAIL_DOMAINS = {"diten.com", "diten.local"}  # the product's dev domains (seed files)
FILE_EXTENSION_TLDS = {"png", "jpg", "jpeg", "gif", "svg", "webp", "ico", "js", "mjs", "css", "map", "json", "ts",
                       "cs", "md", "html", "txt", "xml", "woff", "woff2", "ttf", "min"}

HEX_DIGEST = re.compile(r"^(?:[0-9a-fA-F]{40}|[0-9a-fA-F]{64})$")
IDENTIFIER = re.compile(
    r"^(?:[A-Z][a-z]+(?:[A-Z][a-z]+)+"          # CamelCase
    r"|[a-z]+(?:[A-Z][a-z]+)+"                   # lowerCamel
    r"|[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)+"            # UPPER_SNAKE
    r"|[A-Za-z]+(?:[.:][A-Za-z]+)+"              # dotted / colon keys (Jwt:Secret, auth.password.required)
    r"|[A-Za-z]+(?:[-_][A-Za-z]+)+)$"            # kebab / snake words, header names (X-Request-Id)
)
REFERENCE = re.compile(r"\$\{|\$\(|\{\{|\{[A-Za-z_][\w.]*\}|^<[^<>]+>$|^\$[A-Za-z_]\w*$|^%\w+%$|Configuration\[|Environment\.|process\.env"
                       r"|os\.environ|getenv")
PLACEHOLDER = re.compile(r"(?i)^(?:pass|password|passwd|pwd|secret|changeme)$")
MASKED = re.compile(r"^[*xX•.]+$")
PATH_OR_URL = re.compile(r"^(?:/|\./|\.\./|~/|https?://)")
DEV_SETTINGS = re.compile(r"(?i)(?:^|/)appsettings(?:\.[\w-]+)*\.development\.json$")
DEV_MARKER = re.compile(r"(?i)dev|local|test|sample|example|changeme|placeholder")
SKIP_PATH = re.compile(r"(?:^|/)wwwroot/lib/|^frontend/_Reference/|(?:^|/)wwwroot/assets/json/[^/]+$")
# A test location: a tests / test / fixtures directory (scripts/tests/ included), a *.Tests / *.Test project, a segment
# ending in "Tests" (UnitTests/), a file test_*.py, *Tests.cs, *.test.* or *.spec.*. "Tests" is case-sensitive so that
# words like "contests/" are not test folders.
TEST_PATH = re.compile(
    r"(?i:(?:^|/)(?:tests?|fixtures)/|(?:^|/)[^/]+\.tests?/|(?:^|/)test_[^/]*\.py$|(?:^|/)[^/]+\.(?:test|spec)\.[^/]+$)"
    r"|(?:^|/)[^/]*Tests/|(?:^|/)[^/]*Tests\.cs$"
)
NOREPLY = re.compile(r"(?i)^(?:no-?reply|do-?not-?reply)(?:[+.].*)?$|noreply")


@dataclass(frozen=True)
class Finding:
    path: str
    line: int  # 0 = the file itself (a newly added file's name)
    kind: str
    severity: str
    hint: str  # masked: a length or a fixed word, never a character of the value


class GitError(Exception):
    pass


# ------------------------------------------------------------------------------------------------------- git input
def git(repo: str, *args: str) -> bytes:
    result = subprocess.run(["git", "-C", repo, "-c", "core.quotePath=false", "-c", "core.attributesFile=" + os.devnull,
                             *args],
                            stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=False)
    if result.returncode != 0:
        raise GitError(f"git {args[0]} çıkış kodu {result.returncode}")
    return result.stdout


def unquote_path(raw: str) -> str:
    """git's C-style quoted path ("a\\tb") -> text; an unquoted path is returned as is."""
    if not (len(raw) >= 2 and raw[0] == '"' and raw[-1] == '"'):
        return raw
    out = bytearray()
    body = raw[1:-1].encode("utf-8", "surrogateescape")
    i = 0
    simple = {ord("n"): 10, ord("t"): 9, ord("r"): 13, ord('"'): 34, ord("\\"): 92, ord("a"): 7, ord("b"): 8,
              ord("f"): 12, ord("v"): 11}
    while i < len(body):
        c = body[i]
        if c == 92 and i + 1 < len(body):
            n = body[i + 1]
            if i + 4 <= len(body) and all(48 <= x <= 55 for x in body[i + 1:i + 4]):
                out.append(int(body[i + 1:i + 4].decode(), 8))
                i += 4
                continue
            out.append(simple.get(n, n))
            i += 2
            continue
        out.append(c)
        i += 1
    return out.decode("utf-8", "replace")


HUNK = re.compile(r"^@@ -\d+(?:,(\d+))? \+(\d+)(?:,(\d+))? @@")


def parse_patch(text: str) -> tuple[list[tuple[str, int, str]], set[str]]:
    """-U0 patch -> (added lines [(path, line, text)], removed line texts). Hunk line counts decide where a hunk ends,
    so an added line that itself starts with '++' is never read as a file header."""
    added: list[tuple[str, int, str]] = []
    removed: set[str] = set()
    path = None
    old_left = new_left = 0
    line_no = 0
    for raw in text.split("\n"):
        if old_left > 0 or new_left > 0:
            if raw.startswith("+"):
                if path is not None:
                    added.append((path, line_no, raw[1:]))
                line_no += 1
                new_left -= 1
            elif raw.startswith("-"):
                removed.add(raw[1:].strip())
                old_left -= 1
            elif raw.startswith("\\"):
                pass
            else:  # context line (not expected with -U0) - keep counts honest
                old_left -= 1
                new_left -= 1
                line_no += 1
            continue
        if raw.startswith("+++ "):
            target = raw[4:].rstrip("\r")
            if target == "/dev/null":
                path = None
            else:
                target = unquote_path(target)
                path = target[2:] if target.startswith("b/") else target
            continue
        m = HUNK.match(raw)
        if m:
            old_left = int(m.group(1)) if m.group(1) is not None else 1
            line_no = int(m.group(2))
            new_left = int(m.group(3)) if m.group(3) is not None else 1
    return added, removed


def parse_raw(data: bytes) -> list[str]:
    """`git diff --raw -z --no-abbrev` -> newly added paths whose blob is not also a deleted path's blob (moves)."""
    parts = data.split(b"\0")
    added: list[tuple[str, str]] = []
    deleted_blobs: set[str] = set()
    i = 0
    while i + 1 < len(parts):
        meta = parts[i].decode("utf-8", "replace")
        if not meta.startswith(":"):
            i += 1
            continue
        fields = meta[1:].split()
        path = parts[i + 1].decode("utf-8", "replace")
        i += 2
        if len(fields) < 5:
            continue
        status = fields[4][:1]
        if status == "A":
            added.append((path, fields[3]))
        elif status == "D":
            deleted_blobs.add(fields[2])
    return drop_moved_files(added, deleted_blobs)


def collect(repo: str, base: str, head: str) -> tuple[list[tuple[str, int, str]], list[str]]:
    rng = f"{base}...{head}"
    # --text: a PR-added `.gitattributes` with -diff, or a NUL byte, must not turn added lines into "Binary files differ".
    patch = git(repo, "diff", "--no-renames", "--text", "--no-color", "--no-ext-diff", "--no-textconv", "-U0", rng, "--")
    raw = git(repo, "diff", "--no-renames", "--raw", "-z", "--no-abbrev", rng, "--")
    added, removed = parse_patch(patch.decode("utf-8", "replace"))
    return drop_moved_lines(added, removed), parse_raw(raw)


def drop_moved_lines(added: list[tuple[str, int, str]], removed: set[str]) -> list[tuple[str, int, str]]:
    """Move cut (lines): an added line whose exact text is also removed in the same diff already existed."""
    return [(p, n, t) for p, n, t in added if t.strip() == "" or t.strip() not in removed]


def drop_moved_files(added: list[tuple[str, str]], deleted_blobs: set[str]) -> list[str]:
    """Move cut (files): an added file whose blob equals a deleted file's blob already existed."""
    return [path for path, blob in added if blob not in deleted_blobs]


# ------------------------------------------------------------------------------------------------- names (optional)
NAMES_ENV = "EES_SCAN_NAMES"
MIN_NAME_CHARS = 3
TURKISH_FOLD = str.maketrans({"ı": "i", "İ": "i", "I": "i", "ş": "s", "Ş": "s", "ğ": "g", "Ğ": "g", "ü": "u", "Ü": "u",
                              "ö": "o", "Ö": "o", "ç": "c", "Ç": "c"})
ALNUM_RUN = re.compile(r"[^\W_]+")  # letters / digits (Unicode); every other character splits


def fold(piece: str) -> str:
    """Case-insensitive, Turkish letters (and other combining accents) folded: 'Şükrü' and 'SUKRU' -> 'sukru'."""
    decomposed = unicodedata.normalize("NFKD", piece.translate(TURKISH_FOLD))
    return "".join(ch for ch in decomposed if not unicodedata.combining(ch)).casefold()


def split_camel(run: str) -> list[str]:
    """One letter / digit run -> its pieces at camelCase (adSoyad, XMLParser) and letter / digit boundaries."""
    pieces: list[str] = []
    start = 0
    for i in range(1, len(run)):
        prev, cur = run[i - 1], run[i]
        nxt = run[i + 1] if i + 1 < len(run) else ""
        if ((prev.islower() and cur.isupper()) or (prev.isdigit() != cur.isdigit())
                or (prev.isupper() and cur.isupper() and nxt.islower())):
            pieces.append(run[start:i])
            start = i
    pieces.append(run[start:])
    return pieces


def name_tokens(text: str) -> list[tuple[str, int, int]]:
    """Text -> [(folded token, original length, run number)] in order; pieces of one letter / digit run share a number."""
    out: list[tuple[str, int, int]] = []
    for run_no, run in enumerate(ALNUM_RUN.findall(unicodedata.normalize("NFC", text))):
        for piece in split_camel(run):
            out.append((fold(piece), len(piece), run_no))
    return out


MAX_JOIN = 6  # pieces of one run joined when comparing with an entry written as one word


class NameMatcher:
    """Whole-token matching of a names list. A multi-token entry ('Ad Soyad', 'ad.soyad') matches those tokens in a row;
    an entry also matches whole pieces of ONE identifier joined ('adSoyad', 'AdSoyad85' for 'Ad Soyad' or 'adsoyad').
    A match always starts and ends on a token boundary, so a name inside a longer word never matches."""

    def __init__(self, entries: list[tuple[str, ...]]) -> None:
        self._by_first: dict[str, list[tuple[str, ...]]] = {}
        self._joined: set[str] = set()
        for entry in entries:
            self._by_first.setdefault(entry[0], []).append(entry)
            self._joined.add("".join(entry))

    def find(self, text: str) -> int | None:
        """The original length of the first matched name in text, or None. Never returns the text itself."""
        tokens = name_tokens(text)
        words = [t for t, _, _ in tokens]
        for i, word in enumerate(words):
            for entry in self._by_first.get(word, ()):
                if tuple(words[i:i + len(entry)]) == entry:
                    return sum(n for _, n, _ in tokens[i:i + len(entry)])
            joined, length = "", 0
            for j in range(i, min(i + MAX_JOIN, len(tokens))):
                if tokens[j][2] != tokens[i][2]:
                    break
                joined += tokens[j][0]
                length += tokens[j][1]
                if joined in self._joined:
                    return length
        return None


def parse_names(raw: str) -> tuple[list[tuple[str, ...]], bool]:
    """Raw list text -> (entries as folded token tuples, whether any entry was ignored as too short)."""
    entries: list[tuple[str, ...]] = []
    short = False
    for item in re.split(r"[\n,]", raw):
        item = item.strip()
        if not item:
            continue
        tokens = tuple(t for t, _, _ in name_tokens(item))
        if sum(len(t) for t in tokens) < MIN_NAME_CHARS:
            short = True
            continue
        entries.append(tokens)
    return entries, short


def repo_roots(repo: str) -> list[str]:
    """The working tree, its top level and the main working tree (a linked worktree's shared clone)."""
    roots = {os.path.realpath(repo)}
    for args in (("rev-parse", "--show-toplevel"), ("rev-parse", "--path-format=absolute", "--git-common-dir")):
        try:
            value = git(repo, *args).decode("utf-8", "replace").strip()
        except (GitError, OSError):
            continue
        if value:
            path = os.path.realpath(value)
            roots.add(os.path.dirname(path) if args[-1] == "--git-common-dir" else path)
    return sorted(roots)


def inside(path: str, root: str) -> bool:
    try:
        return os.path.commonpath([path, root]) == root
    except ValueError:  # different drives
        return False


def load_names(env_value: str | None, names_file: str | None, repo: str, out) -> NameMatcher | None:
    """The supplied list (environment and / or file outside the repository) -> a matcher, or None when no usable list.
    Messages never carry an entry, the list size or the file's path."""
    raw = env_value or ""
    if names_file:
        real = os.path.realpath(names_file)
        if any(inside(real, root) for root in repo_roots(repo)):
            print("::warning title=added-lines-scan::Ad listesi dosyası deponun içinde: reddedildi ve okunmadı. "
                  "Listeyi depo dışında tutun (--names-file depo dışı bir yol olmalı).", file=out)
        else:
            try:
                with open(real, encoding="utf-8-sig") as fh:
                    raw += "\n" + fh.read()
            except (OSError, UnicodeError):
                print("::warning title=added-lines-scan::Ad listesi dosyası okunamadı.", file=out)
    entries, short = parse_names(raw)
    if short:
        print(f"::notice title=added-lines-scan::{MIN_NAME_CHARS} karakterden kısa ad girişleri yok sayıldı.", file=out)
    return NameMatcher(entries) if entries else None


NAMES_NOT_RUN = ("Kişi adı denetimi ÇALIŞMADI: kullanılabilir ad listesi verilmedi (EES_SCAN_NAMES ya da --names-file). "
                 "Yeşil sonuç 'kişi adı yok' demek değildir.")
NAMES_RAN = "Kişi adı denetimi çalıştı (liste depo dışından)."


# ----------------------------------------------------------------------------------------------------- the checks
def allowed_value(value: str) -> bool:
    v = value.strip()
    return (v == "" or bool(HEX_DIGEST.match(v)) or bool(IDENTIFIER.match(v)) or bool(REFERENCE.search(v))
            or bool(MASKED.match(v)) or bool(PLACEHOLDER.match(v)) or bool(PATH_OR_URL.match(v))
            or any(ch.isspace() for ch in v))


def email_kind(local: str, domain: str) -> str | None:
    d = domain.lower().rstrip(".")
    labels = d.split(".")
    if labels[-1] in FILE_EXTENSION_TLDS:
        return None
    if NOREPLY.search(local) or any(NOREPLY.search(label) for label in labels) or labels[-1] in ALLOWED_EMAIL_TLDS or "example" in labels[:-1] \
            or d in ALLOWED_EMAIL_DOMAINS or any(d.endswith("." + a) for a in ALLOWED_EMAIL_DOMAINS):
        return None
    if d in FREEMAIL_DOMAINS or any(label in FREEMAIL_LABELS for label in labels[:-1]):
        return "email-freemail"
    return "email"


def is_test_path(path: str) -> bool:
    return bool(TEST_PATH.search(path))


def scan_line(path: str, line_no: int, text: str, names: NameMatcher | None = None) -> list[Finding]:
    found: dict[str, Finding] = {}
    dev_line = bool(DEV_SETTINGS.search(path)) and bool(DEV_MARKER.search(text))
    test_path = is_test_path(path)

    def add(kind: str, hint: str) -> None:
        if kind in found:
            return
        severity = WARNING
        if kind in LITERAL_KINDS and (dev_line or test_path):
            severity = NOTICE
        elif kind in FIXTURE_KINDS and test_path:
            severity = NOTICE
        found[kind] = Finding(path, line_no, kind, severity, hint)

    for kind in PROVIDER_KINDS:
        for rx in FAMILIES[kind]:
            m = rx.search(text)
            if m:
                value = m.group("v") if "v" in rx.groupindex else m.group(0)
                add(kind, f"uzunluk {len(value)}")
                break
    specific = bool(found)

    if not specific:
        if ";" in text and CONN_GUARD.search(text):
            for rx in FAMILIES["conn-password"]:
                for m in rx.finditer(text):
                    if not allowed_value(m.group("v")):
                        add("conn-password", f"uzunluk {len(m.group('v'))}")
                        break
        for rx in FAMILIES["url-credentials"]:
            for m in rx.finditer(text):
                if not allowed_value(m.group("v")):
                    add("url-credentials", f"uzunluk {len(m.group('v'))}")
                    break
        for rx in FAMILIES["secret-assignment"]:
            for m in rx.finditer(text):
                if not allowed_value(m.group("v")):
                    add("secret-assignment", f"uzunluk {len(m.group('v'))}")
                    break

    for rx in FAMILIES["home-path"]:
        for m in rx.finditer(text):
            if m.group("v").lower() not in ALLOWED_HOME_NAMES:
                add("home-path", f"kullanıcı adı uzunluğu {len(m.group('v'))}")
                break

    for rx in FAMILIES["email"]:
        for m in rx.finditer(text):
            kind = email_kind(m.group("local"), m.group("v"))
            if kind:
                add(kind, f"uzunluk {len(m.group(0))}")

    if names is not None:
        length = names.find(text)
        if length is not None:
            add(NAME_KIND, f"uzunluk {length}")  # a warning everywhere: a real name in a fixture is the case to catch
    return list(found.values())


def file_kind(path: str) -> str | None:
    for kind in FILE_KINDS:
        if any(rx.search(path) for rx in FAMILIES[kind]):
            return kind
    return None


def scan(added: list[tuple[str, int, str]], new_files: list[str], stats: dict | None = None,
         names: NameMatcher | None = None) -> list[Finding]:
    """stats (optional) receives "truncated": the added lines longer than MAX_LINE (only their start is scanned).
    names (optional): the supplied names list; also read against the names of newly added files."""
    findings: list[Finding] = []
    truncated = 0
    for path in new_files:
        if SKIP_PATH.search(path):
            continue
        kind = file_kind(path)
        if kind:
            findings.append(Finding(path, 0, kind, WARNING, "dosya adı"))
        if names is not None and names.find(path) is not None:
            findings.append(Finding(path, 0, NAME_KIND, WARNING, "dosya adı"))
    for path, line_no, text in added:
        if SKIP_PATH.search(path):
            continue
        if len(text) > MAX_LINE:
            truncated += 1
            text = text[:MAX_LINE]
        findings.extend(scan_line(path, line_no, text, names))
    if stats is not None:
        stats["truncated"] = truncated
    return findings


# ---------------------------------------------------------------------------------------------------------- output
CONTROL = re.compile(r"[\x00-\x1f\x7f]")


def clean_path(path: str) -> str:
    return CONTROL.sub("", path)


def md_path(path: str) -> str:
    """A path as Markdown table text: control characters dropped, backslashes / backticks / pipes escaped."""
    return clean_path(path).replace("\\", "\\\\").replace("`", "\\`").replace("|", "\\|")


def esc_data(s: str) -> str:
    return s.replace("%", "%25").replace("\r", "%0D").replace("\n", "%0A")


def esc_prop(s: str) -> str:
    return esc_data(s).replace(":", "%3A").replace(",", "%2C")


def annotation(f: Finding) -> str:
    where = f"file={esc_prop(clean_path(f.path))}" + (f",line={f.line}" if f.line > 0 else "")
    msg = f"[{f.kind}] {MESSAGES[f.kind]} ({f.hint}). Değer gösterilmez; gerçekse silin, anahtarsa iptal edip yenileyin."
    return f"::{f.severity} {where},title=added-lines-scan::{esc_data(msg)}"


def report(findings: list[Finding], out, summary_path: str | None, truncated: int = 0, names_ran: bool = False) -> None:
    names_line = NAMES_RAN if names_ran else NAMES_NOT_RUN
    print(f"::notice title=added-lines-scan::{names_line}", file=out)
    for f in findings[:MAX_ANNOTATIONS]:
        print(annotation(f), file=out)
    if len(findings) > MAX_ANNOTATIONS:
        print(f"::notice title=added-lines-scan::{len(findings) - MAX_ANNOTATIONS} bulgu daha var; tamamı iş adımı "
              "özetinde (step summary).", file=out)
    warnings = sum(1 for f in findings if f.severity == WARNING)
    notices = len(findings) - warnings
    kinds = Counter(f.kind for f in findings)
    by_kind = ", ".join(f"{k}={n}" for k, n in sorted(kinds.items())) or "-"
    cut = f" {truncated} satır kısaltıldı (ilk {MAX_LINE} karakter tarandı)." if truncated else ""
    print(f"added-lines-scan: {warnings} uyarı, {notices} not (yalnız eklenen satırlar; engellemez). Türler: {by_kind}."
          + cut, file=out)
    if summary_path:
        try:
            with open(summary_path, "a", encoding="utf-8") as fh:
                fh.write(f"### added-lines-scan\n\n{warnings} uyarı, {notices} not — yalnız eklenen satırlar, "
                         "uyarı amaçlı; değerler gösterilmez." + cut + "\n\n" + names_line + "\n\n")
                if findings:
                    fh.write("| Tür | Sayı |\n|---|---|\n")
                    for k, n in sorted(kinds.items()):
                        fh.write(f"| {k} | {n} |\n")
                    fh.write("\n| Dosya | Satır | Tür | Düzey | İpucu |\n|---|---|---|---|---|\n")
                    for f in findings[:MAX_SUMMARY_ROWS]:
                        fh.write(f"| {md_path(f.path)} | {f.line or '-'} | {f.kind} | {f.severity} | {f.hint} |\n")
                    if len(findings) > MAX_SUMMARY_ROWS:
                        fh.write(f"\n… {len(findings) - MAX_SUMMARY_ROWS} satır daha.\n")
        except OSError:
            print("::warning title=added-lines-scan::Özet dosyası yazılamadı.", file=out)


class QuietParser(argparse.ArgumentParser):
    """A usage error must not echo the arguments (a path may carry a name): exit 2 without printing them."""

    def error(self, message: str):  # noqa: ARG002 - the message quotes argv
        raise SystemExit(2)


def main(argv: list[str] | None = None, out=None, env=None) -> int:
    out = out or sys.stdout
    env = os.environ if env is None else env
    parser = QuietParser(
        description="WARNING-ONLY scan of the lines (and new file names) a pull request adds: secret shapes, risky file "
                    "names, personal folder paths, e-mail addresses and - only when a names list is supplied from "
                    f"outside the repository ({NAMES_ENV} or --names-file) - personal names. Always exits 0.")
    parser.add_argument("--base", required=True, help="base commit (the diff is base...head)")
    parser.add_argument("--head", required=True, help="head commit")
    parser.add_argument("--repo", default=".", help="repository path (default: .)")
    parser.add_argument("--summary", default=env.get("GITHUB_STEP_SUMMARY"), help="Markdown summary file to append to")
    parser.add_argument("--names-file", default=None,
                        help=f"names list OUTSIDE the repository (newline / comma separated); adds to ${NAMES_ENV}")
    try:
        args = parser.parse_args(argv)
    except SystemExit as exc:  # a usage error (or --help) must not block either
        if exc.code not in (0, None):
            print("::warning title=added-lines-scan::Tarama çalışamadı (geçersiz argüman); engellenmez.", file=out)
        return 0
    try:
        if not args.base or not args.head or args.base.startswith("-") or args.head.startswith("-"):
            raise GitError("geçersiz --base / --head")
        names = load_names(env.get(NAMES_ENV), args.names_file, args.repo, out)
        added, new_files = collect(args.repo, args.base, args.head)
        stats: dict = {}
        findings = scan(added, new_files, stats, names)
        report(findings, out, args.summary, stats.get("truncated", 0), names_ran=names is not None)
    except GitError as exc:
        print(f"::warning title=added-lines-scan::Tarama çalışamadı ({exc}); bu PR denetlenmedi, engellenmez.", file=out)
    except Exception as exc:  # noqa: BLE001 - never block a pull request
        print(f"::warning title=added-lines-scan::Tarama çalışamadı (iç hata: {type(exc).__name__}); engellenmez.",
              file=out)
    return 0


if __name__ == "__main__":
    sys.exit(main())
