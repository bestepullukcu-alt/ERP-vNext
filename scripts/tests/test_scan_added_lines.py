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

import contextlib
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
    "scripts/tests/test_keys.py": (f'KEY = "{AWS}"', "aws-key", "warning"),
    "web/src/session.spec.ts": (f'const t = "{JWT}";', "jwt", "warning"),
    # fake home paths / e-mail addresses in a test location are deliberate fixtures: notice (the same strings outside a
    # test location - cases/home_*.md, cases/*mail.cs above - stay warnings)
    "scripts/tests/test_fixture_paths.py": (f'ROOT = "/Users/{HOME_NAME}/repo"', "home-path", "notice"),
    "src/Svc.UnitTests/PathsTests.cs": (f'var p = @"C:\\Users\\{HOME_NAME}\\repo";', "home-path", "notice"),
    "services/Svc/Svc.Api.Tests/Mail.cs": (f'Email = "{FREEMAIL}",', "email-freemail", "notice"),
    "web/src/login.test.js": (f'const mail = "{CORPMAIL}";', "email", "notice"),
    "web/src/IntegrationTests/home.md": (f'"/home/{HOME_NAME}/work"', "home-path", "notice"),
    "web/src/MailerTests.cs": (f'To = "{CORPMAIL}";', "email", "notice"),
    # not test locations: "contests" is not "tests", "test_x.md" is not test_*.py
    "docs/contests/home.md": (f'ran /Users/{HOME_NAME}/repo/x.sh', "home-path", "warning"),
    "docs/test_notes.md": (f'Email = "{FREEMAIL}",', "email-freemail", "warning"),
}
FIXTURE_DOWNGRADES = [p for p, (_, k, sev) in POSITIVE.items() if k in ("home-path", "email", "email-freemail")
                      and sev == "notice"]
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
        self.assertEqual(set(scan.MESSAGES), kinds | {scan.NAME_KIND})  # person-name: NameCheckTests

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

    def test_moved_content_does_not_fire_but_a_moved_file_name_is_checked(self) -> None:
        # content of a moved file: move cut; its NAME: checked like any added path (no move cut on names)
        self.assertEqual([], [f for f in self.findings() if f.path == "new/moved.cs"])
        self.assertEqual([("log-file", 0)], [(f.kind, f.line) for f in self.findings() if f.path == "new/moved.log"])
        self.assertEqual([], [f for f in self.findings() if f.path.startswith("old/")])

    def test_sabotage_without_the_move_cut_the_moved_content_fires(self) -> None:
        # the production content cut switched off: collect() then reports the moved file's content
        with mock.patch.object(scan, "drop_moved_lines", lambda added, removed: added):
            added, new_files = scan.collect(self.root, self.base, self.head)
        moved = scan.scan(added, new_files)
        self.assertTrue([f for f in moved if f.path == "new/moved.cs" and f.kind == "secret-assignment"])
        self.assertTrue([f for f in moved if f.path == "new/moved.cs" and f.kind == "aws-key"])

    # ------------------------------------------------------------------------------------- hidden lines (--text)
    def test_a_diff_attribute_or_a_nul_byte_cannot_hide_added_lines(self) -> None:
        got = {(f.path, f.line, f.kind) for f in self.findings()}
        self.assertIn(("cases/hidden.dat", 1, "github-token"), got)
        self.assertIn(("cases/nul.txt", 2, "github-token"), got)

    def test_sabotage_without_text_the_lines_are_hidden(self) -> None:
        with mock.patch.object(scan, "PATCH_ARGS", tuple(a for a in scan.PATCH_ARGS if a != "--text")):
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

    def check_fixture_downgrades(self) -> None:
        self.assertGreaterEqual(len(FIXTURE_DOWNGRADES), 6)
        for path in FIXTURE_DOWNGRADES:  # no subTest: a sabotage must see the AssertionError
            self.check_line_case(path)
            self.assertFalse([f for f in self.findings() if f.path == path and f.severity == "warning"], path)

    def test_fixture_home_paths_and_emails_in_test_locations_are_notices(self) -> None:
        self.check_fixture_downgrades()

    def test_the_same_strings_outside_tests_stay_warnings(self) -> None:
        for path in ("cases/home_mac.md", "cases/home_linux.md", "cases/home_win.md", "cases/freemail.cs",
                     "cases/corpmail.cs", "docs/contests/home.md", "docs/test_notes.md"):
            with self.subTest(path=path):
                self.check_line_case(path)

    def test_secret_shapes_in_test_locations_keep_the_warning(self) -> None:
        for path in ("services/Svc/tests/TokenTests.cs", "scripts/tests/test_keys.py", "web/src/session.spec.ts"):
            with self.subTest(path=path):
                self.check_line_case(path)

    def test_sabotage_without_the_fixture_downgrade_the_fixtures_are_warnings(self) -> None:
        with mock.patch.object(scan, "FIXTURE_KINDS", ()):
            with self.assertRaises(AssertionError):
                self.check_fixture_downgrades()

    def test_test_location_names(self) -> None:
        yes = ["tests/a.cs", "scripts/tests/x.py", "a/Svc.Tests/b.cs", "a/UnitTests/b.cs", "a/test_x.py", "a/FooTests.cs",
               "a/b.test.ts", "a/b.spec.js", "a/fixtures/s.json", "test/x.md"]
        no = ["docs/contests/a.md", "a/latest/b.cs", "a/test_x.md", "src/attest.py", "a/b.cs", "a/Tests.md"]
        for path in yes:
            self.assertTrue(scan.is_test_path(path), path)
        for path in no:
            self.assertFalse(scan.is_test_path(path), path)

    # --------------------------------------------------------------------------------------------------- output
    def test_output_has_annotations_and_never_a_secret_value(self) -> None:
        summary = os.path.join(self.root, "summary.md")
        out = io.StringIO()
        code = scan.main(["--base", self.base, "--head", self.head, "--repo", self.root, "--summary", summary], out=out)
        text = out.getvalue()
        with open(summary, encoding="utf-8") as fh:
            text_summary = fh.read()
        self.assertEqual(0, code)
        for value in SENTINELS:  # first: a leak must be what turns this test red
            for chunk in {value, value[:8], value[-8:]}:
                self.assertNotIn(chunk, text)
                self.assertNotIn(chunk, text_summary)
        self.assertNotIn("iç hata", text)
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
        calls = []

        def leaky(path, line_no, text, names=None):  # same signature as scan_line: fails on the leak, not a TypeError
            calls.append(path)
            return [scan.Finding(f.path, f.line, f.kind, f.severity, text) for f in real(path, line_no, text, names)]

        with mock.patch.object(scan, "scan_line", leaky):
            with self.assertRaises(AssertionError) as caught:
                self.test_output_has_annotations_and_never_a_secret_value()
        self.assertTrue(calls, "the leaky scan_line was never called")
        self.assertIn("unexpectedly found", str(caught.exception))

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

    # ------------------------------------------------------------------------- decision: literals vs provider keys
    def test_decision_literals_are_notices_and_provider_keys_warnings_in_tests(self) -> None:
        by_kind = {k: content for p, (content, k, _) in POSITIVE.items() if p.startswith("cases/")}
        self.assertTrue(set(scan.PROVIDER_KINDS) <= set(by_kind) and set(scan.LITERAL_KINDS) <= set(by_kind))
        for kind in scan.LITERAL_KINDS:
            got = {(f.kind, f.severity) for f in scan.scan_line("tests/pinned/CaseTests.cs", 1, by_kind[kind])}
            self.assertIn((kind, "notice"), got, kind)
        for kind in scan.PROVIDER_KINDS:
            got = {(f.kind, f.severity) for f in scan.scan_line("tests/pinned/CaseTests.cs", 1, by_kind[kind])}
            self.assertIn((kind, "warning"), got, kind)
        help_out = io.StringIO()
        with contextlib.redirect_stdout(help_out):
            self.assertEqual(0, scan.main(["--help"], out=io.StringIO(), env={}))
        help_text = scan.re.sub(r"(\w)- (\w)", r"\1-\2", " ".join(help_out.getvalue().split()))  # undo hyphen wraps
        self.assertIn("password-like literals", help_text)
        self.assertIn("provider-shaped keys (PEM, GitHub, AWS, Slack, Google, Stripe, Anthropic, JWT) stay warnings "
                      "everywhere", help_text)
        self.assertIn("full names, surnames and logins", help_text)
        doc = " ".join(scan.__doc__.split())
        self.assertIn("Decision: in test locations password-like literals", doc)
        self.assertIn("provider-shaped keys (PEM, GitHub, AWS, Slack, Google, Stripe, Anthropic, JWT)", doc)

    def test_development_marker_is_a_whole_word(self) -> None:
        path = "services/Svc/appsettings.Development.json"
        device = f'"Default": "Host=device-db;Database=app;Username=svc;Password={V}"'
        self.assertEqual([("conn-password", "warning")], [(f.kind, f.severity) for f in scan.scan_line(path, 1, device)])
        dev1 = f'"Default": "Host=db;Database=app;Username=dev1;Password={V}"'
        self.assertEqual([("conn-password", "notice")], [(f.kind, f.severity) for f in scan.scan_line(path, 1, dev1)])

    # ------------------------------------------------------------------------------------- output and memory limits
    def test_a_local_run_prints_every_finding_and_no_step_summary_pointer(self) -> None:
        out = io.StringIO()
        scan.main(["--base", self.base, "--head", self.head, "--repo", self.root, "--summary", ""], out=out, env={})
        text = out.getvalue()
        annotations = [ln for ln in text.splitlines() if ln.startswith(("::warning file=", "::notice file="))]
        self.assertEqual(len(self.findings()), len(annotations))
        self.assertGreater(len(annotations), scan.MAX_ANNOTATIONS)
        self.assertNotIn("step summary", text)
        for value in SENTINELS:
            self.assertNotIn(value, text)

    def test_a_console_without_utf8_never_gives_a_non_zero_exit(self) -> None:
        for env_extra in ({"PYTHONIOENCODING": "ascii"}, {"PYTHONIOENCODING": "ascii", "EES_SCAN_NAMES": "Qaz,Ad Soyad"}):
            with self.subTest(env=sorted(env_extra)):
                env = dict(os.environ, **env_extra)
                env.pop("GITHUB_STEP_SUMMARY", None)
                result = subprocess.run([sys.executable, SCRIPT, "--base", self.base, "--head", self.head, "--repo",
                                         self.root], stdout=subprocess.PIPE, stderr=subprocess.PIPE, env=env)
                self.assertEqual(0, result.returncode)
                self.assertEqual(b"", result.stderr)
                self.assertIn(b"added-lines-scan: ", result.stdout)
                self.assertNotIn(b"RuntimeError", result.stdout)
                self.assertNotIn(b"UnicodeEncodeError", result.stdout)

    def test_sabotage_without_safe_streams_an_ascii_console_breaks_the_output(self) -> None:
        # the guard the test above relies on: without it the output raises and nothing useful is printed
        env = dict(os.environ, PYTHONIOENCODING="ascii")
        env.pop("GITHUB_STEP_SUMMARY", None)
        code = ("import sys; sys.path.insert(0, %r); import scan_added_lines as s; s.safe_streams = lambda: None; "
                "sys.exit(s.run_cli())" % SCRIPTS)
        result = subprocess.run([sys.executable, "-c", code, "--base", self.base, "--head", self.head, "--repo",
                                 self.root], stdout=subprocess.PIPE, stderr=subprocess.PIPE, env=env)
        self.assertEqual(0, result.returncode)  # still exit 0: the handler does not re-raise
        self.assertNotIn(b"added-lines-scan: ", result.stdout)

    def test_the_patch_read_is_capped_with_one_notice(self) -> None:
        stats: dict = {}
        with mock.patch.object(scan, "MAX_PATCH_BYTES", 2000):
            added, _ = scan.collect(self.root, self.base, self.head, stats)
            out = io.StringIO()
            scan.main(["--base", self.base, "--head", self.head, "--repo", self.root, "--summary", ""], out=out, env={})
        self.assertTrue(stats.get("patch_cut"))
        self.assertLess(sum(len(t) for _, _, t in added), 2000)
        self.assertEqual(1, out.getvalue().count("Tarama yarıda kesildi"))
        self.assertNotIn("Tarama çalışamadı", out.getvalue())
        full: dict = {}
        scan.collect(self.root, self.base, self.head, full)
        self.assertFalse(full.get("patch_cut"))

    def test_large_binary_files_are_not_read_but_their_names_are_checked(self) -> None:
        # at the real 1 MB threshold the small -diff / NUL files are read (test above); with 0 they count as large
        stats: dict = {}
        with mock.patch.object(scan, "BINARY_SKIP_BYTES", 0):
            added, new_files = scan.collect(self.root, self.base, self.head, stats)
        paths = {p for p, _, _ in added}
        self.assertNotIn("cases/hidden.dat", paths)
        self.assertNotIn("cases/nul.txt", paths)
        self.assertIn("cases/gh.txt", paths)
        self.assertGreaterEqual(stats["binary_skipped"], 3)  # hidden.dat, nul.txt, site.pfx
        self.assertIn("certs/site.pfx", new_files)
        self.assertIn(("certs/site.pfx", "key-file"), {(f.path, f.kind) for f in scan.scan(added, new_files)})


# ------------------------------------------------------------------------------------------- names (synthetic only)
# Invented placeholder "names": none of them is a real person. "Ad Soyad" (two tokens), a 3-letter one, one with Turkish
# letters, one with dotted / dotless i, one written as a single word.
N_PAIR = "Ad Soyad"
N_SHORT3 = "Qaz"
N_TR = "Şablonöğe"
N_I = "İpsizıl"
N_ONE = "ornekkisi"
NAMES = [N_PAIR, N_SHORT3, N_TR, N_I, N_ONE]
NAMES_ENV_VALUE = "\n".join([N_PAIR, N_SHORT3]) + "," + ",".join([N_TR, N_I, N_ONE])
PADDING = ["Dolguadbir", "Dolguadiki", "Dolguaduc", "Dolguaddort", "Dolguadbes"]  # never in the diff
# Every string about the list that must never be printed: entries, their folded / upper / lower forms, the raw value.
NAME_SENTINELS = sorted({v for n in NAMES + PADDING for v in (n, n.lower(), n.upper(), scan.fold(n))
                         if len(v) >= 3} | {NAMES_ENV_VALUE})

NAME_CASES = {  # path -> added line that must give a person-name finding on line 1
    "names/dot.cs": 'var u = "ad.soyad";',
    "names/snake.py": "ad_soyad = 1",
    "names/kebab.md": "see ad-soyad here",
    "names/camel.ts": "const adSoyad = load();",
    "names/pascal.cs": "class AdSoyadReport {}",
    "names/space.md": "Hazırlayan: AD SOYAD",
    "names/joined.md": "user adsoyad85 logged in",
    "names/mail.md": "write to ad.soyad@corp.example.org",
    "names/path.md": "/data/ad/soyad/report.txt",
    "names/short3_dot.md": "qaz.soyad",
    "names/short3_digit.md": "user qaz1",
    "names/short3_upper.md": "QAZ",
    "names/tr_upper_ascii.md": "SABLONOGE",
    "names/tr_lower.md": "şablonöğe",
    "names/tr_mixed.md": "x_ŞABLONÖĞE_y",
    "names/i_upper.md": "IPSIZIL",
    "names/i_dotless.md": "ipsızil",
    "names/i_dotted.md": "İPSİZİL",
    "names/one_camel.ts": "let ornekKisi = 1;",
    "names/one_plain.md": "ORNEKKISI",
    "services/Svc/tests/PeopleTests.cs": 'var user = "ad.soyad";',  # a name in a test file: a warning
}
NAME_NEGATIVE = {  # a listed name only INSIDE a longer word: no finding
    "neg/longer3.md": "qazan qazanlar Qazma aqazb",
    "neg/longer_pair.md": "adsoyadi soyadlar badsoyad",
    "neg/longer_tr.md": "şablonöğeler sablonogex",
    "neg/longer_one.md": "ornekkisiler xornekkisi",
    "neg/short.md": "ab x yz",
}
NAME_NEW_FILE = "people/ad_soyad.txt"


class NameCheckTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.root = tempfile.mkdtemp(prefix="scan-names-")
        cls.outside = tempfile.mkdtemp(prefix="scan-names-list-")
        r = cls.root
        git(r, "init", "-q", "-b", "main")
        write(r, "base.md", "qaz.soyad on the base must not fire\n")
        git(r, "add", "-A")
        git(r, "commit", "-q", "-m", "base")
        cls.base = git(r, "rev-parse", "HEAD")
        for path, line in {**NAME_CASES, **NAME_NEGATIVE}.items():
            write(r, path, line + "\n")
        write(r, NAME_NEW_FILE, "nothing personal\n")
        git(r, "add", "-A")
        git(r, "commit", "-q", "-m", "head")
        cls.head = git(r, "rev-parse", "HEAD")
        cls.added, cls.new_files = scan.collect(r, cls.base, cls.head)
        cls.names_file = os.path.join(cls.outside, "names.txt")
        write(cls.outside, "names.txt", "\n".join(NAMES) + "\n")
        # a sibling linked worktree of the same repository, in a directory of its own (not under root)
        cls.sibling_parent = tempfile.mkdtemp(prefix="scan-names-wt-")
        cls.sibling = os.path.join(cls.sibling_parent, "side")
        git(r, "worktree", "add", "-q", "-b", "side", cls.sibling, cls.head)

    @classmethod
    def tearDownClass(cls) -> None:
        shutil.rmtree(cls.root, ignore_errors=True)
        shutil.rmtree(cls.outside, ignore_errors=True)
        shutil.rmtree(cls.sibling_parent, ignore_errors=True)

    def matcher(self, names: list[str] | None = None) -> scan.NameMatcher:
        entries, _ = scan.parse_names("\n".join(NAMES if names is None else names))
        return scan.NameMatcher(entries)

    def name_findings(self, names: list[str] | None = None) -> list[scan.Finding]:
        return [f for f in scan.scan(self.added, self.new_files, names=self.matcher(names)) if f.kind == scan.NAME_KIND]

    def run_main(self, *extra: str, env: dict | None = None, head: str | None = None,
                 repo: str | None = None) -> tuple[int, str, str]:
        fd, summary = tempfile.mkstemp(suffix=".md", dir=self.outside)
        os.close(fd)
        out = io.StringIO()
        code = scan.main(["--base", self.base, "--head", head or self.head, "--repo", repo or self.root, "--summary",
                          summary, *extra], out=out, env={} if env is None else env)
        with open(summary, encoding="utf-8") as fh:
            return code, out.getvalue(), fh.read()

    def assert_no_list_leak(self, *texts: str) -> None:
        for text in texts:
            for value in NAME_SENTINELS:
                self.assertNotIn(value, text)

    # ---------------------------------------------------------------------------------------------- matching forms
    def test_every_form_is_found(self) -> None:
        got = {(f.path, f.line, f.severity) for f in self.name_findings()}
        for path in NAME_CASES:
            with self.subTest(path=path):
                self.assertIn((path, 1, "warning"), got)

    def test_no_match_inside_a_longer_word(self) -> None:
        self.assertEqual([], [f.path for f in self.name_findings() if f.path.startswith("neg/")])

    def test_a_name_in_a_test_file_is_a_warning(self) -> None:
        got = [f.severity for f in self.name_findings() if f.path == "services/Svc/tests/PeopleTests.cs"]
        self.assertEqual(["warning"], got)

    def test_a_new_file_name_is_checked(self) -> None:
        self.assertIn((NAME_NEW_FILE, 0), {(f.path, f.line) for f in self.name_findings()})

    def test_content_on_the_base_does_not_fire(self) -> None:
        self.assertFalse([f for f in self.name_findings() if f.path == "base.md"])

    def test_turkish_letters_fold_both_ways(self) -> None:
        # list written with Turkish letters, text without - and the reverse
        self.assertIsNotNone(scan.NameMatcher(scan.parse_names(N_TR)[0]).find("x SABLONOGE y"))
        self.assertIsNotNone(scan.NameMatcher(scan.parse_names("sablonoge")[0]).find("x ŞABLONÖĞE y"))
        self.assertIsNotNone(scan.NameMatcher(scan.parse_names("ipsizil")[0]).find("İPSİZİL"))
        self.assertIsNotNone(scan.NameMatcher(scan.parse_names("IPSIZIL")[0]).find("ipsızil"))
        self.assertIsNotNone(scan.NameMatcher(scan.parse_names("çağ.öğün")[0]).find("CAG_OGUN"))
        self.assertEqual(scan.fold("İıIiŞşĞğÜüÖöÇç"), "iiiissgguuoocc")

    def test_short_entries_are_ignored_and_said_once(self) -> None:
        entries, short = scan.parse_names("ab\nx, yz ,a.b\n" + N_SHORT3)
        self.assertTrue(short)
        self.assertEqual([("qaz",)], entries)
        self.assertFalse([f for f in self.name_findings(["ab", "x", "yz"]) if f.path == "neg/short.md"])
        code, text, _ = self.run_main(env={"EES_SCAN_NAMES": "ab,x,yz," + N_SHORT3})
        self.assertEqual(0, code)
        self.assertEqual(1, text.count("karakterden kısa ad girişleri yok sayıldı"))
        self.assertIn(scan.NAMES_RAN, text)

    def test_only_short_entries_means_the_check_did_not_run(self) -> None:
        _, text, summary = self.run_main(env={"EES_SCAN_NAMES": "ab, x"})
        self.assertIn(scan.NAMES_NOT_RUN, text)
        self.assertIn(scan.NAMES_NOT_RUN, summary)
        self.assertNotIn("[person-name]", text)

    # ---------------------------------------------------------------------------------------------- list sources
    def test_without_a_list_one_line_says_the_name_check_did_not_run(self) -> None:
        code, text, summary = self.run_main()
        self.assertEqual(0, code)
        self.assertEqual(1, text.count(scan.NAMES_NOT_RUN))
        self.assertIn("ÇALIŞMADI", text)
        self.assertIn(scan.NAMES_NOT_RUN, summary)
        self.assertNotIn("[person-name]", text)
        _, empty_env, _ = self.run_main(env={"EES_SCAN_NAMES": ""})  # an unset Actions secret arrives empty
        self.assertIn(scan.NAMES_NOT_RUN, empty_env)

    def test_environment_list_runs_the_check(self) -> None:
        code, text, summary = self.run_main(env={"EES_SCAN_NAMES": NAMES_ENV_VALUE})
        self.assertEqual(0, code)
        self.assertIn(scan.NAMES_RAN, text)
        self.assertIn("[person-name]", text)
        self.assertIn("| names/dot.cs | 1 | person-name | warning | uzunluk 7 |", summary)
        self.assertIn(f"| {NAME_NEW_FILE} | - | person-name | warning | dosya adı |", summary)

    def test_names_file_outside_the_repository_runs_the_check(self) -> None:
        code, text, summary = self.run_main("--names-file", self.names_file)
        self.assertEqual(0, code)
        self.assertIn(scan.NAMES_RAN, text)
        self.assertIn("| names/tr_lower.md | 1 | person-name | warning |", summary)
        self.assert_no_list_leak(text, summary)

    REFUSED = "Ad listesi dosyası deponun içinde: reddedildi ve okunmadı"

    def assert_refused(self, names_file: str, repo: str | None = None) -> None:
        code, text, summary = self.run_main("--names-file", names_file, repo=repo)
        self.assertEqual(0, code)
        self.assertIn(self.REFUSED, text)
        self.assertIn(scan.NAMES_NOT_RUN, text)
        self.assertNotIn("[person-name]", text)
        self.assertNotIn("person-name", summary)
        self.assert_no_list_leak(text, summary)

    def in_tree_list(self, root: str, rel: str = "names/list.txt") -> str:
        write(root, rel, "\n".join(NAMES) + "\n")
        self.addCleanup(os.remove, os.path.join(root, rel))
        return os.path.join(root, rel)

    def test_names_file_inside_the_repository_is_refused(self) -> None:
        self.assert_refused(self.in_tree_list(self.root))

    def test_names_file_through_a_symlink_is_refused(self) -> None:
        link = os.path.join(self.outside, "link.txt")
        os.symlink(self.in_tree_list(self.root), link)
        self.addCleanup(os.remove, link)
        self.assert_refused(link)

    def test_names_file_given_as_a_relative_path_is_refused(self) -> None:
        self.assert_refused(os.path.relpath(self.in_tree_list(self.root), os.getcwd()))

    def test_names_file_with_a_different_letter_case_is_refused(self) -> None:
        inside = self.in_tree_list(self.root)
        parent, leaf = os.path.split(self.root)
        variant = os.path.join(parent, leaf.swapcase(), "names", "list.txt")
        if not os.path.exists(variant) or not os.path.samefile(variant, inside):
            self.skipTest("case-sensitive file system: a different letter case is another path")
        self.assert_refused(variant)

    def test_names_file_in_a_sibling_linked_worktree_is_refused(self) -> None:
        self.assert_refused(self.in_tree_list(self.sibling))

    def test_names_file_in_the_main_worktree_is_refused_from_a_linked_one(self) -> None:
        self.assert_refused(self.in_tree_list(self.root, "list-main.txt"), repo=self.sibling)

    def test_names_file_at_the_top_level_is_refused_from_a_subdirectory_repo_path(self) -> None:
        self.assert_refused(self.in_tree_list(self.root, "list-top.txt"), repo=os.path.join(self.root, "names"))

    def test_sabotage_without_the_inside_check_the_repository_file_is_read(self) -> None:
        with mock.patch.object(scan, "inside_any", lambda path, roots: False):
            with self.assertRaises(AssertionError):
                self.test_names_file_inside_the_repository_is_refused()

    def test_sabotage_without_the_worktree_list_a_sibling_worktree_file_is_read(self) -> None:
        with mock.patch.object(scan, "worktree_paths", lambda repo: []):
            with self.assertRaises(AssertionError):
                self.test_names_file_in_a_sibling_linked_worktree_is_refused()

    def test_sabotage_with_the_repo_path_as_the_only_root_the_top_level_file_is_read(self) -> None:
        with mock.patch.object(scan, "repo_roots", lambda repo: [repo]):
            with self.assertRaises(AssertionError):
                self.test_names_file_at_the_top_level_is_refused_from_a_subdirectory_repo_path()

    def test_sabotage_comparing_spelling_instead_of_identity_lets_a_case_variant_through(self) -> None:
        def by_spelling(path, roots):
            real = os.path.realpath(path)
            return any(os.path.commonpath([real, os.path.realpath(r)]) == os.path.realpath(r) for r in roots)

        parent, leaf = os.path.split(self.root)
        if not os.path.exists(os.path.join(parent, leaf.swapcase())):
            self.skipTest("case-sensitive file system")
        with mock.patch.object(scan, "inside_any", by_spelling):
            with self.assertRaises(AssertionError):
                self.test_names_file_with_a_different_letter_case_is_refused()

    # ------------------------------------------------------------------------------------------- matching details
    def test_a_lone_first_word_does_not_match_a_two_word_entry(self) -> None:
        pair = scan.NameMatcher(scan.parse_names(N_PAIR)[0])
        self.assertIsNone(pair.find("ad bey"))
        self.assertIsNone(pair.find("soyad"))
        self.assertIsNone(pair.find("ad, bey soyad"))
        self.assertIsNotNone(pair.find("ad soyad"))
        self.assertIsNotNone(pair.find("x.ad.soyad.y"))

    def test_other_accents_are_stripped_too(self) -> None:
        self.assertIsNotNone(scan.NameMatcher(scan.parse_names("Éxémplàr")[0]).find("EXEMPLAR"))
        self.assertIsNotNone(scan.NameMatcher(scan.parse_names("exemplar")[0]).find("x_Éxémplàr_y"))
        self.assertIsNone(scan.NameMatcher(scan.parse_names("exemplar")[0]).find("Éxémplàrs"))

    def test_a_stray_combining_dot_does_not_split_the_word(self) -> None:
        lowered_outside_turkish = "i̇psi̇zi̇l"  # what 'İPSİZİL'.lower() gives outside the tr locale
        self.assertEqual(lowered_outside_turkish, "İPSİZİL".lower())
        self.assertIsNotNone(self.matcher([N_I]).find("x " + lowered_outside_turkish + " y"))
        self.assertIsNotNone(self.matcher([N_TR]).find(scan.unicodedata.normalize("NFD", N_TR)))

    # ------------------------------------------------------------------------------------------- argument errors
    def test_an_unknown_argument_is_reached_and_never_echoed(self) -> None:
        args = ["--base", self.base, "--head", self.head, "--repo", self.root, "--summary", "", "--bogus=" + N_ONE]
        out, err = io.StringIO(), io.StringIO()
        with contextlib.redirect_stderr(err), contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(0, scan.main(args, out=out, env={}))
        self.assertIn("geçersiz argüman", out.getvalue())  # the unknown argument WAS the error
        self.assertNotIn(N_ONE, out.getvalue() + err.getvalue())
        self.assertNotIn("--bogus", out.getvalue() + err.getvalue())
        result = subprocess.run([sys.executable, SCRIPT, *args[:-1], "--unknown=" + N_ONE], stdout=subprocess.PIPE,
                                stderr=subprocess.PIPE, text=True)
        self.assertEqual(0, result.returncode)
        self.assertIn("geçersiz argüman", result.stdout)
        self.assertNotIn(N_ONE, result.stdout + result.stderr)

    def test_sabotage_argparse_default_error_echoes_the_argument(self) -> None:
        with mock.patch.object(scan.QuietParser, "error", scan.argparse.ArgumentParser.error):
            with self.assertRaises(AssertionError):
                self.test_an_unknown_argument_is_reached_and_never_echoed()

    # ---------------------------------------------------------------------------------------------- masked output
    def test_name_output_is_masked_and_independent_of_the_list_size(self) -> None:
        _, text, summary = self.run_main(env={"EES_SCAN_NAMES": NAMES_ENV_VALUE})
        self.assert_no_list_leak(text, summary)
        padded = NAMES_ENV_VALUE + "\n" + "\n".join(PADDING)
        _, text2, summary2 = self.run_main(env={"EES_SCAN_NAMES": padded})
        self.assertEqual(text, text2)  # a longer list changes nothing printed: its size is not shown
        self.assertEqual(summary, summary2)

    def test_sabotage_a_leaking_name_hint_is_caught(self) -> None:
        real = scan.scan_line

        def leaky(path, line_no, text, names=None):
            return [scan.Finding(f.path, f.line, f.kind, f.severity, text if f.kind == scan.NAME_KIND else f.hint)
                    for f in real(path, line_no, text, names)]

        with mock.patch.object(scan, "scan_line", leaky):
            with self.assertRaises(AssertionError):
                self.test_name_output_is_masked_and_independent_of_the_list_size()

    def test_error_paths_never_show_the_list(self) -> None:
        env = {"EES_SCAN_NAMES": NAMES_ENV_VALUE}
        _, git_fail, _ = self.run_main(env=env, head="0" * 40)
        self.assertIn("Tarama çalışamadı", git_fail)
        with mock.patch.object(scan, "scan", side_effect=RuntimeError(NAMES_ENV_VALUE)):
            _, internal, _ = self.run_main(env=env)
        self.assertIn("iç hata: RuntimeError", internal)
        missing = os.path.join(self.outside, "no-such-" + N_ONE + ".txt")
        _, unreadable, _ = self.run_main("--names-file", missing, env=env)
        self.assertIn("Ad listesi dosyası okunamadı", unreadable)
        out = io.StringIO()
        self.assertEqual(0, scan.main(["--base", self.base, "--names-file", self.names_file, "--bogus", N_ONE],
                                      out=out, env=env))
        self.assertIn("geçersiz argüman", out.getvalue())
        self.assert_no_list_leak(git_fail, internal, unreadable, out.getvalue())
        # the command line: stdout and stderr, list in the environment and in a file, a usage error included
        for args in (["--base", self.base, "--head", self.head], ["--base", self.base, "--head", "0" * 40],
                     ["--base", self.base, "--head", self.head, "--names-file", self.names_file],
                     ["--base", self.base, "--names-file", self.names_file, "--unknown", N_ONE]):
            with self.subTest(args=args):
                result = subprocess.run([sys.executable, SCRIPT, *args, "--repo", self.root, "--summary", ""],
                                        stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True,
                                        env=dict(os.environ, EES_SCAN_NAMES=NAMES_ENV_VALUE))
                self.assertEqual(0, result.returncode)
                self.assert_no_list_leak(result.stdout, result.stderr)


class MovedFileNameTests(unittest.TestCase):
    """A file whose content equals a deleted file's (a move, or two empty files) still has its NAME checked."""

    @classmethod
    def setUpClass(cls) -> None:
        cls.root = tempfile.mkdtemp(prefix="scan-moved-")
        r = cls.root
        git(r, "init", "-q", "-b", "main")
        write(r, "keep/config.sample", "same content\n")
        write(r, "keep/cert.txt", "a certificate placeholder\n")
        write(r, "other/notes.txt", "shared text\n")
        write(r, "empty_old.txt", "")
        git(r, "add", "-A")
        git(r, "commit", "-q", "-m", "base")
        cls.base = git(r, "rev-parse", "HEAD")
        os.makedirs(os.path.join(r, "certs"))
        os.makedirs(os.path.join(r, "people"))
        git(r, "mv", "keep/config.sample", ".env")                       # rename into a risky name
        git(r, "mv", "keep/cert.txt", "certs/site.pem")                  # rename into a key-file name
        git(r, "mv", "other/notes.txt", "people/qaz-notes.md")           # same content, a listed name in the path
        git(r, "rm", "-q", "empty_old.txt")
        write(r, "people/ornekkisi.txt", "")                             # empty file, while another empty one goes
        git(r, "add", "-A")
        git(r, "commit", "-q", "-m", "head")
        cls.head = git(r, "rev-parse", "HEAD")
        cls.added, cls.new_files = scan.collect(r, cls.base, cls.head)
        cls.names = scan.NameMatcher(scan.parse_names("\n".join(NAMES))[0])

    @classmethod
    def tearDownClass(cls) -> None:
        shutil.rmtree(cls.root, ignore_errors=True)

    def check(self) -> None:
        got = {(f.path, f.line, f.kind) for f in scan.scan(self.added, self.new_files, names=self.names)}
        self.assertIn((".env", 0, "env-file"), got)
        self.assertIn(("certs/site.pem", 0, "key-file"), got)
        self.assertIn(("people/qaz-notes.md", 0, "person-name"), got)
        self.assertIn(("people/ornekkisi.txt", 0, "person-name"), got)

    def test_moved_and_empty_files_get_the_file_name_checks(self) -> None:
        self.check()

    def test_the_move_cut_still_applies_to_content(self) -> None:
        self.assertEqual([], self.added)  # every added line already existed on the base

    def test_sabotage_the_old_move_cut_on_file_names_hides_them(self) -> None:
        def old_cut(data: bytes) -> list[str]:  # the removed behaviour: drop an added path whose blob was deleted
            parts, added, deleted, i = data.split(b"\0"), [], set(), 0
            while i + 1 < len(parts):
                meta = parts[i].decode()
                if not meta.startswith(":"):
                    i += 1
                    continue
                fields, path = meta[1:].split(), parts[i + 1].decode()
                i += 2
                if fields[4][:1] == "A":
                    added.append((path, fields[3]))
                elif fields[4][:1] == "D":
                    deleted.add(fields[2])
            return [p for p, b in added if b not in deleted]

        with mock.patch.object(scan, "parse_raw", old_cut):
            added, new_files = scan.collect(self.root, self.base, self.head)
        with mock.patch.object(self, "new_files", new_files), mock.patch.object(self, "added", added):
            with self.assertRaises(AssertionError):
                self.check()


if __name__ == "__main__":
    unittest.main()
