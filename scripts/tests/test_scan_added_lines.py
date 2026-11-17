"""scripts/scan_added_lines.py - the warning-only added-lines scan, on throw-away git repositories.

Every fake secret below is ASSEMBLED AT RUN TIME from pieces, so this file itself carries no token-shaped literal (the
repository is public and the scan reads this file too). Run by scripts/run_phase1_gates.sh:

    python3 -m unittest discover -s scripts/tests -p 'test_*.py'

What is proven: each pattern family fires on an added line; the same content already on the base does not; moved
content does not; the allow-list does not; tests / fixtures and development settings are downgraded to notice; no
fake secret value ever reaches the output; the exit code is 0 in every case (git failure included); and switching a
family off (sabotage) turns its own test red.
"""

from __future__ import annotations

import io
import os
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest import mock

SCRIPTS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, SCRIPTS)
import scan_added_lines as scan  # noqa: E402

SCRIPT = os.path.join(SCRIPTS, "scan_added_lines.py")

# ------------------------------------------------------------------------------------------- fake values (run time)
MIX = "Zq8vX2mN4pL7rT1wK9sB3dF6hJ0yC5gQ"  # mixed case + digits: not identifier-shaped, not hex


def fake(n: int) -> str:
    return (MIX * (n // len(MIX) + 1))[:n]


V = "R7" + "kQ2x" + "Lp9" + "Wm4z"  # a 13-char password-like value
GH = "gh" + "p_" + fake(36)
GH_PAT = "github" + "_pat_" + fake(40)
AWS = "AK" + "IA" + "QX7RT2MZ9KLP4WBN"
SLACK = "xo" + "xb-" + "1234567890-" + fake(16)
GOOGLE = "AI" + "za" + fake(35)
STRIPE = "sk_" + "live_" + fake(24)
ANTHROPIC = "sk-" + "ant-" + fake(40)
JWT = "ey" + "J" + fake(20) + "." + "ey" + "J" + fake(30) + "." + fake(43)
PEM = "-----BEGIN " + "RSA PRIVATE" + " KEY-----"
SECRET_ASSIGN = "Q9" + fake(22)
HOME_NAME = "jd" + "oe7"
FREEMAIL = "jane.doe" + "@" + "gm" + "ail.com"
CORPMAIL = "ops" + "@" + "acme-corp" + ".io"
HEX40 = "0123456789abcdef" * 2 + "01234567"

# Every value that must never be printed.
SENTINELS = [V, GH, GH_PAT, AWS, SLACK, GOOGLE, STRIPE, ANTHROPIC, JWT, SECRET_ASSIGN, HOME_NAME, FREEMAIL, CORPMAIL]

# path -> content; each file is ADDED by the head commit. (kind, severity) expected on line 1 (0 = the file name).
POSITIVE = {
    "cases/pem.txt": (PEM, "private-key", "warning"),
    "cases/gh.txt": (f'token: {GH}', "github-token", "warning"),
    "cases/ghpat.txt": (f'x {GH_PAT}', "github-token", "warning"),
    "cases/aws.txt": (f'aws_access_key_id = {AWS}', "aws-key", "warning"),
    "cases/slack.txt": (f'hook {SLACK}', "slack-token", "warning"),
    "cases/google.txt": (f'key {GOOGLE}', "google-api-key", "warning"),
    "cases/stripe.txt": (f'stripe {STRIPE}', "stripe-live-key", "warning"),
    "cases/anthropic.txt": (f'ANTHROPIC {ANTHROPIC}', "anthropic-key", "warning"),
    "cases/jwt.txt": (f'Authorization: Bearer {JWT}', "jwt", "warning"),
    "cases/conn.json": (f'"Db": "Server=db;Database=app;User Id=svc;Password={V};"', "conn-password", "warning"),
    "cases/url.txt": (f'amqp://svc:{V}@broker:5672/', "url-credentials", "warning"),
    "cases/assign.cs": (f'var clientSecret = "{SECRET_ASSIGN}";', "secret-assignment", "warning"),
    "cases/assign.json": (f'  "ApiKey": "{SECRET_ASSIGN}",', "secret-assignment", "warning"),
    "cases/home_mac.md": (f'ran /Users/{HOME_NAME}/repo/x.sh', "home-path", "warning"),
    "cases/home_linux.md": (f'"/home/{HOME_NAME}/work"', "home-path", "warning"),
    "cases/home_win.md": (f'C:\\Users\\{HOME_NAME}\\repo', "home-path", "warning"),
    "cases/freemail.cs": (f'Email = "{FREEMAIL}",', "email-freemail", "warning"),
    "cases/corpmail.cs": (f'Email = "{CORPMAIL}",', "email", "warning"),
    # downgrades
    "services/Svc/tests/FooTests.cs": (f'var password = "{SECRET_ASSIGN}";', "secret-assignment", "notice"),
    "services/Svc/Svc.Api.Tests/BarTests.cs": (f'var token = "{SECRET_ASSIGN}";', "secret-assignment", "notice"),
    "services/Svc/fixtures/seed.json": (f'"password": "{SECRET_ASSIGN}"', "secret-assignment", "notice"),
    "services/Svc/appsettings.Development.json":
        (f'"Default": "Host=localhost;Database=app;Username=dev;Password={V}"', "conn-password", "notice"),
    # a provider token under tests stays a warning
    "services/Svc/tests/TokenTests.cs": (f'// {GH}', "github-token", "warning"),
}
NEW_FILES = {
    ".env": ("env-file", f"API_KEY={V}\n"),
    "svc/.env.production": ("env-file", "X=1\n"),
    "certs/site.pfx": ("key-file", None),
    "certs/tls.pem": ("key-file", "not a key\n"),
    "logs/run.log": ("log-file", "started\n"),
    "TestResults/a.trx": ("trx-file", "<TestRun/>\n"),
}
# Allow-listed: none of these may produce a finding.
ALLOWED = {
    "allow/hex.cs": f'apiToken = "{HEX40}";',
    "allow/ident.cs": 'passwordKey = "DefaultPassword";\nconst TOKEN_NAME = "ACCESS_TOKEN_KEY";\ntokenHeader = "X-Diten-Service-Token"',
    "allow/ref.cs": 'password = "${DB_PASSWORD}"\n"Password": "$(PASS)"\n"Secret": "<your-secret>"\nsecret = "$API_SECRET"\npassword = f"{user_password}"',
    "allow/conf.cs": 'var p = Configuration["Db:Password"];\nvar s = Environment.GetEnvironmentVariable("SECRET");',
    "allow/conn.json": '"Db": "Server=db;Database=app;User Id=svc;Password=${PG_PASS};"\n"X": "Host=h;Password=;"',
    "allow/empty.js": 'password = ""\ntoken: \'\'',
    "allow/prose.js": 'passwordHint: "En az sekiz karakter olmalı",',
    "allow/url.txt": 'mongodb://user:pass@localhost:27017\nhttps://example.com/path\namqp://svc:***@broker',
    "allow/home.md": "/Users/runner/work/x\n/home/user/x\nC:\\Users\\someone\\x\n/srv/Users/" + HOME_NAME
                     + "/x\nsee /Users/<name>/ and $HOME",
    "allow/mail.cs": "a@b.test\nx@foo.invalid\ninfo@example.com\nnoreply@github.com\nadmin@diten.com\nno-reply@diten.local\n"
                     "icon@2x.png\nCo-Authored-By: Bot <12345+bot@users.noreply.github.com>",
    "frontend/Diten.Web/wwwroot/lib/vendor.js": f"var k = '{GOOGLE}'; var e = '{FREEMAIL}';",
    "frontend/_Reference/page.html": f"<a href='mailto:{FREEMAIL}'>x</a>",
    "frontend/Diten.Web/wwwroot/assets/json/data.json": f'{{"token": "{SECRET_ASSIGN}"}}',
}
ALLOWED_NEW_FILES = {".env.example": "API_KEY=\n", "frontend/Diten.Web/wwwroot/lib/x/debug.log": "x\n"}

EXISTING = (f'var clientSecret = "{SECRET_ASSIGN}";\n{PEM}\n{GH}\n/Users/{HOME_NAME}/x/\n{FREEMAIL}\n'
            f'"Db": "Server=db;Database=app;User Id=svc;Password={V};"\n')
MOVED = f'var apiKey = "{SECRET_ASSIGN}";\n{AWS}\n'


def git(cwd: str, *args: str) -> str:
    env = dict(os.environ, GIT_CONFIG_NOSYSTEM="1", GIT_CONFIG_GLOBAL=os.devnull, GIT_AUTHOR_NAME="t",
               GIT_AUTHOR_EMAIL="t@example.test", GIT_COMMITTER_NAME="t", GIT_COMMITTER_EMAIL="t@example.test")
    return subprocess.run(["git", "-c", "commit.gpgsign=false", "-c", "core.hooksPath=" + os.devnull, *args], cwd=cwd,
                          env=env, check=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True).stdout.strip()


def write(root: str, path: str, content: str | bytes) -> None:
    full = os.path.join(root, path)
    os.makedirs(os.path.dirname(full), exist_ok=True)
    mode = "wb" if isinstance(content, bytes) else "w"
    with open(full, mode, **({} if isinstance(content, bytes) else {"encoding": "utf-8", "newline": "\n"})) as fh:
        fh.write(content)


class AddedLinesScanTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.root = tempfile.mkdtemp(prefix="scan-added-")
        r = cls.root
        git(r, "init", "-q", "-b", "main")
        # base: content that already exists must never be reported
        write(r, "existing.cs", EXISTING)
        write(r, "old/moved.cs", MOVED)
        write(r, "old/moved.log", "a moved log\n")
        git(r, "add", "-A")
        git(r, "commit", "-q", "-m", "base")
        cls.base = git(r, "rev-parse", "HEAD")
        git(r, "checkout", "-q", "-b", "pr")
        # head: one more benign line in the existing file, a move, and every case
        write(r, "existing.cs", EXISTING + "// a harmless new line\n")
        os.makedirs(os.path.join(r, "new"), exist_ok=True)
        os.replace(os.path.join(r, "old/moved.cs"), os.path.join(r, "new/moved.cs"))
        os.replace(os.path.join(r, "old/moved.log"), os.path.join(r, "new/moved.log"))
        for path, (content, _, _) in POSITIVE.items():
            write(r, path, content + "\n")
        for path, (_, content) in NEW_FILES.items():
            write(r, path, content if content is not None else bytes([0, 1, 2, 3, 255]))
        for path, content in ALLOWED.items():
            write(r, path, content + "\n")
        for path, content in ALLOWED_NEW_FILES.items():
            write(r, path, content)
        # an added line that itself starts with "++" must not be read as a file header
        write(r, "cases/plusplus.txt", "++ b/fake\n+++ b/also-fake\n" + GH + "\n")
        # a PR-added `-diff` attribute and a NUL byte must not hide added lines (--text)
        write(r, ".gitattributes", "*.dat -diff\n")
        write(r, "cases/hidden.dat", GH + "\n")
        write(r, "cases/nul.txt", b"x\x00y\n" + GH.encode() + b"\n")
        # a line longer than MAX_LINE is cut and counted
        write(r, "cases/long.txt", "a" * (scan.MAX_LINE + 10) + "\n")
        git(r, "add", "-A")
        git(r, "commit", "-q", "-m", "head")
        cls.head = git(r, "rev-parse", "HEAD")
        cls.added, cls.new_files = scan.collect(r, cls.base, cls.head)

    @classmethod
    def tearDownClass(cls) -> None:
        shutil.rmtree(cls.root, ignore_errors=True)

    def findings(self) -> list[scan.Finding]:
        return scan.scan(self.added, self.new_files)

    def run_main(self, base: str, head: str, repo: str | None = None) -> tuple[int, str]:
        out = io.StringIO()
        code = scan.main(["--base", base, "--head", head, "--repo", repo or self.root, "--summary", ""], out=out)
        return code, out.getvalue()

    # --------------------------------------------------------------------------------------------- family checks
    def check_line_case(self, path: str) -> None:
        _, kind, severity = POSITIVE[path]
        got = [(f.kind, f.severity, f.line) for f in self.findings() if f.path == path]
        self.assertIn((kind, severity, 1), got, f"{path}: expected {kind}/{severity}, got {got}")

    def check_file_case(self, path: str) -> None:
        kind, _ = NEW_FILES[path]
        got = [(f.kind, f.line) for f in self.findings() if f.path == path and f.line == 0]
        self.assertEqual([(kind, 0)], got, path)

    def check_family(self, family: str) -> None:
        paths = [p for p, (_, k, _) in POSITIVE.items() if k == family or (family == "email" and k.startswith("email"))]
        files = [p for p, (k, _) in NEW_FILES.items() if k == family]
        self.assertTrue(paths or files, f"no case for family {family}")
        for p in paths:
            self.check_line_case(p)
        for p in files:
            self.check_file_case(p)

    def test_every_family_fires_on_an_added_line_or_file(self) -> None:
        for family in scan.FAMILIES:
            with self.subTest(family=family):
                self.check_family(family)

    def test_every_family_has_a_message_and_a_case(self) -> None:
        kinds = {k for _, k, _ in POSITIVE.values()} | {k for k, _ in NEW_FILES.values()}
        self.assertEqual(set(scan.MESSAGES), kinds)

    def test_sabotage_switching_a_family_off_turns_its_test_red(self) -> None:
        for family in scan.FAMILIES:
            with self.subTest(family=family), mock.patch.dict(scan.FAMILIES, {family: []}):
                with self.assertRaises(AssertionError, msg=f"family {family} switched off, its test stayed green"):
                    self.check_family(family)

    def test_added_line_starting_with_plus_plus_is_content(self) -> None:
        got = [(f.kind, f.line) for f in self.findings() if f.path == "cases/plusplus.txt"]
        self.assertEqual([("github-token", 3)], got)
        self.assertFalse(any(f.path in ("fake", "also-fake") for f in self.findings()))

    # ---------------------------------------------------------------------------------------------- diff scope
    def test_content_already_on_the_base_does_not_fire(self) -> None:
        self.assertEqual([], [f for f in self.findings() if f.path == "existing.cs"])
        self.assertIn(("existing.cs", 7, "// a harmless new line"), self.added)

    def test_sabotage_scanning_the_whole_file_makes_existing_content_fire(self) -> None:
        whole = [("existing.cs", i + 1, t) for i, t in enumerate(EXISTING.splitlines())]
        self.assertTrue([f for f in scan.scan(whole, []) if f.path == "existing.cs"])

    def test_moved_content_and_moved_files_do_not_fire(self) -> None:
        self.assertEqual([], [f for f in self.findings() if f.path.startswith(("new/", "old/"))])

    def test_sabotage_without_the_move_cut_the_moved_content_fires(self) -> None:
        # the production cuts switched off: collect() then reports the moved file and its content
        with mock.patch.object(scan, "drop_moved_lines", lambda added, removed: added), \
                mock.patch.object(scan, "drop_moved_files", lambda added, deleted: [path for path, _ in added]):
            added, new_files = scan.collect(self.root, self.base, self.head)
        moved = scan.scan(added, new_files)
        self.assertTrue([f for f in moved if f.path == "new/moved.cs" and f.kind == "secret-assignment"])
        self.assertTrue([f for f in moved if f.path == "new/moved.cs" and f.kind == "aws-key"])
        self.assertTrue([f for f in moved if f.path == "new/moved.log" and f.kind == "log-file"])

    # ------------------------------------------------------------------------------------- hidden lines (--text)
    def test_a_diff_attribute_or_a_nul_byte_cannot_hide_added_lines(self) -> None:
        got = {(f.path, f.line, f.kind) for f in self.findings()}
        self.assertIn(("cases/hidden.dat", 1, "github-token"), got)
        self.assertIn(("cases/nul.txt", 2, "github-token"), got)

    def test_sabotage_without_text_the_lines_are_hidden(self) -> None:
        real = scan.git

        def no_text(repo, *args):
            return real(repo, *[a for a in args if a != "--text"])

        with mock.patch.object(scan, "git", no_text):
            added, _ = scan.collect(self.root, self.base, self.head)
        paths = {p for p, _, _ in added}
        self.assertNotIn("cases/hidden.dat", paths)
        self.assertNotIn("cases/nul.txt", paths)
        self.assertIn("cases/gh.txt", paths)

    # ---------------------------------------------------------------------------------------- long lines (ReDoS)
    ADVERSARIAL = ["_", "a.", "a=", "a", "eyJ", "x@", "a:", "password", "/Users/a", "Password=;", "ghp_", "sk-ant-"]

    def test_a_200_kb_adversarial_line_scans_in_under_a_second(self) -> None:
        import time
        for unit in self.ADVERSARIAL:
            line = (unit * (200_000 // len(unit) + 1))[:200_000]
            with self.subTest(unit=unit):
                stats: dict = {}
                start = time.perf_counter()
                scan.scan([("f.cs", 1, line)], [], stats)
                self.assertLess(time.perf_counter() - start, 1.0)
                self.assertEqual(1, stats["truncated"])

    def test_bounded_patterns_stay_fast_without_the_line_cap(self) -> None:
        # the cap switched off: the bounded quantifiers alone keep a 20 KB line well under a second
        # (unbounded, these took 8-15 s in the blind review)
        import time
        with mock.patch.object(scan, "MAX_LINE", 10 ** 9):
            for unit in self.ADVERSARIAL:
                line = (unit * (20_000 // len(unit) + 1))[:20_000]
                with self.subTest(unit=unit):
                    start = time.perf_counter()
                    scan.scan([("f.cs", 1, line)], [])
                    self.assertLess(time.perf_counter() - start, 1.0)

    # ---------------------------------------------------------------------------------------------- allow-list
    def test_allow_list_cases_do_not_fire(self) -> None:
        for path in list(ALLOWED) + list(ALLOWED_NEW_FILES):
            with self.subTest(path=path):
                self.assertEqual([], [(f.kind, f.line) for f in self.findings() if f.path == path])

    def test_sabotage_without_the_allow_list_values_fire(self) -> None:
        with mock.patch.object(scan, "allowed_value", lambda v: False):
            fired = {f.path for f in self.findings()}
        for path in ("allow/hex.cs", "allow/ident.cs", "allow/ref.cs", "allow/prose.js", "allow/url.txt"):
            self.assertIn(path, fired)
        with mock.patch.object(scan, "ALLOWED_HOME_NAMES", set()):
            self.assertIn("allow/home.md", {f.path for f in self.findings()})
        with mock.patch.object(scan, "SKIP_PATH", scan.re.compile(r"(?!)")):
            self.assertIn("frontend/_Reference/page.html", {f.path for f in self.findings()})

    def test_sabotage_without_the_downgrade_tests_are_warnings(self) -> None:
        with mock.patch.object(scan, "TEST_PATH", scan.re.compile(r"(?!)")):
            with self.assertRaises(AssertionError):
                self.check_line_case("services/Svc/tests/FooTests.cs")

    # --------------------------------------------------------------------------------------------------- output
    def test_output_has_annotations_and_never_a_secret_value(self) -> None:
        summary = os.path.join(self.root, "summary.md")
        out = io.StringIO()
        code = scan.main(["--base", self.base, "--head", self.head, "--repo", self.root, "--summary", summary], out=out)
        text = out.getvalue()
        with open(summary, encoding="utf-8") as fh:
            text_summary = fh.read()
        self.assertEqual(0, code)
        annotations = [ln for ln in text.splitlines() if ln.startswith(("::warning file=", "::notice file="))]
        self.assertEqual(scan.MAX_ANNOTATIONS, len(annotations))
        self.assertEqual(10, scan.MAX_ANNOTATIONS)
        self.assertIn("::warning file=.env,title=added-lines-scan::[env-file]", text)
        self.assertIn("bulgu daha var; tamamı iş adımı özetinde (step summary).", text)
        self.assertIn("added-lines-scan:", text)
        self.assertIn("1 satır kısaltıldı", text)
        self.assertIn("1 satır kısaltıldı", text_summary)
        self.assertIn("| cases/gh.txt | 1 | github-token | warning | uzunluk 40 |", text_summary)
        self.assertIn("| services/Svc/tests/FooTests.cs | 1 | secret-assignment | notice |", text_summary)
        self.assertIn("| cases/conn.json | 1 | conn-password | warning | uzunluk 13 |", text_summary)
        for value in SENTINELS:
            for chunk in {value, value[:8], value[-8:]}:
                self.assertNotIn(chunk, text)
                self.assertNotIn(chunk, text_summary)

    def test_sabotage_the_secret_assertion_catches_a_leaking_hint(self) -> None:
        real = scan.scan_line

        def leaky(path, line_no, text):
            return [scan.Finding(f.path, f.line, f.kind, f.severity, text) for f in real(path, line_no, text)]

        with mock.patch.object(scan, "scan_line", leaky):
            with self.assertRaises(AssertionError):
                self.test_output_has_annotations_and_never_a_secret_value()

    def test_exit_code_is_zero_from_the_command_line(self) -> None:
        for args in ([self.base, self.head], [self.base, "0" * 40], ["-x", self.head], ["--base=-x", "--"]):
            with self.subTest(args=args):
                result = subprocess.run([sys.executable, SCRIPT, "--base", args[0], "--head", args[1], "--repo",
                                         self.root, "--summary", ""], stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                                        text=True)
                self.assertEqual(0, result.returncode, result.stderr)
                for value in SENTINELS:
                    self.assertNotIn(value, result.stdout + result.stderr)

    def test_git_failure_is_a_warning_and_exit_zero(self) -> None:
        code, text = self.run_main(self.base, "0" * 40)
        self.assertEqual(0, code)
        self.assertIn("::warning title=added-lines-scan::Tarama çalışamadı", text)
        empty = tempfile.mkdtemp(prefix="scan-nogit-")
        try:
            code, text = self.run_main("a", "b", repo=empty)
            self.assertEqual(0, code)
            self.assertIn("Tarama çalışamadı", text)
        finally:
            shutil.rmtree(empty, ignore_errors=True)

    def test_internal_error_is_a_warning_and_exit_zero(self) -> None:
        with mock.patch.object(scan, "scan", side_effect=RuntimeError("x")):
            code, text = self.run_main(self.base, self.head)
        self.assertEqual(0, code)
        self.assertIn("iç hata: RuntimeError", text)

    def test_annotation_properties_are_escaped(self) -> None:
        line = scan.annotation(scan.Finding("a,b:c.txt", 3, "jwt", "warning", "uzunluk 9"))
        self.assertTrue(line.startswith("::warning file=a%2Cb%3Ac.txt,line=3,title=added-lines-scan::[jwt]"))

    def test_summary_escapes_backticks_and_drops_control_characters(self) -> None:
        fd, summary = tempfile.mkstemp(suffix=".md", dir=self.root)
        os.close(fd)
        out = io.StringIO()
        bad = "a`b\x01c|d\x1b[31m.txt"
        scan.report([scan.Finding(bad, 2, "jwt", "warning", "uzunluk 9")], out, summary)
        with open(summary, encoding="utf-8") as fh:
            text = fh.read()
        self.assertIn("| a\\`bc\\|d[31m.txt | 2 | jwt |", text)
        for ch in ("\x01", "\x1b"):
            self.assertNotIn(ch, text)
            self.assertNotIn(ch, out.getvalue())
        self.assertNotIn("`a", text)

    def test_sabotage_unescaped_summary_path_is_caught(self) -> None:
        with mock.patch.object(scan, "md_path", lambda p: p):
            with self.assertRaises(AssertionError):
                self.test_summary_escapes_backticks_and_drops_control_characters()

    def test_quoted_paths_are_unquoted(self) -> None:
        self.assertEqual("a\tb.txt", scan.unquote_path('"a\\tb.txt"'))
        self.assertEqual("ç.txt", scan.unquote_path('"\\303\\247.txt"'))
        self.assertEqual("plain.txt", scan.unquote_path("plain.txt"))


if __name__ == "__main__":
    unittest.main()
