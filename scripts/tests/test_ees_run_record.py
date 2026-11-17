"""scripts/ees_run_record.py - the EES run record built from session transcripts.

Run by scripts/run_phase1_gates.sh:

    python3 -m unittest discover -s scripts/tests -p 'test_*.py'

Every transcript here is SYNTHETIC and written to a temp directory. The pull-request section uses a fake `gh` function:
no test touches the network.
"""

from __future__ import annotations

import io
import json
import os
import shutil
import sys
import tempfile
import unittest
from contextlib import redirect_stderr
from datetime import datetime, timedelta, timezone

SCRIPTS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, SCRIPTS)

import ees_run_record as rr  # noqa: E402

T0 = datetime(2026, 11, 16, 10, 0, 0, tzinfo=timezone.utc)


def at(minutes=0.0, seconds=0.0):
    t = T0 + timedelta(minutes=minutes, seconds=seconds)
    return t.strftime("%Y-%m-%dT%H:%M:%S.000Z")


def human(ts, text="hello", **extra):
    e = {"type": "user", "timestamp": ts, "message": {"role": "user", "content": text}}
    e.update(extra)
    return e


def human_parts(ts, text):
    return {"type": "user", "timestamp": ts, "message": {"role": "user", "content": [{"type": "text", "text": text}]}}


def result(ts, tool_id):
    return {"type": "user", "timestamp": ts, "message": {"role": "user", "content": [
        {"type": "tool_result", "tool_use_id": tool_id, "content": "SECRETRESULT"}]}}


def assistant(ts, mid, usage=None, model="model-a", blocks=None):
    msg = {"id": mid, "model": model, "role": "assistant", "content": blocks if blocks is not None else [
        {"type": "text", "text": "SECRETREPLY"}]}
    if usage is not None:
        msg["usage"] = usage
    return {"type": "assistant", "timestamp": ts, "message": msg}


def tool_use(tid, name, **inp):
    return {"type": "tool_use", "id": tid, "name": name, "input": inp}


def usage(i=0, cw=0, cr=0, o=0):
    return {"input_tokens": i, "cache_creation_input_tokens": cw, "cache_read_input_tokens": cr, "output_tokens": o}


class Base(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.mkdtemp(prefix="ees_rr_")
        self.addCleanup(shutil.rmtree, self.dir, ignore_errors=True)

    def write(self, name, events):
        path = os.path.join(self.dir, name)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8") as fh:
            for e in events:
                fh.write(json.dumps(e) + "\n")
            fh.write("not json at all\n")  # a damaged line must be skipped, not fatal
        return path

    def data(self, path, **kw):
        return rr.build_data([path], kw.pop("prs", []), kw.pop("repo", None), kw.pop("idle", 5), kw.pop("since", None),
                             kw.pop("until", None), gh=kw.pop("gh", None))

    def run_main(self, argv, gh=None):
        buf = io.StringIO()
        code = rr.main(argv, gh=gh, stdout=buf)
        return code, buf.getvalue()


class UsageCounting(Base):
    def test_usage_counted_once_per_message_id_last_line_wins(self):
        path = self.write("s.jsonl", [
            human(at(0)),
            assistant(at(0, 5), "m1", usage(i=1, o=5), blocks=[{"type": "text", "text": "x"}]),
            assistant(at(0, 6), "m1", usage(i=1, cw=100, cr=1000, o=40), blocks=[tool_use("t1", "Read", file_path="/x/y.txt")]),
            assistant(at(0, 7), "m1", usage(i=1, cw=100, cr=1000, o=40), blocks=[tool_use("t2", "Bash", command="ls")]),
            result(at(0, 8), "t1"),
            assistant(at(0, 9), "m2", usage(i=2, cw=10, cr=20, o=7), model="model-b"),
        ])
        d = self.data(path)
        main = d["ozet"]["token_ana"]
        self.assertEqual(main["model-a"], {"mesaj": 1, "girdi": 1, "onbellek_yazma": 100, "onbellek_okuma": 1000, "cikti": 40})
        self.assertEqual(main["model-b"]["cikti"], 7)
        self.assertEqual(d["ozet"]["token_hepsi"], (1 + 100 + 1000 + 40) + (2 + 10 + 20 + 7))
        self.assertEqual(d["ozet"]["token_girdi"], 1 + 100 + 1000 + 2 + 10 + 20)
        self.assertEqual(d["ozet"]["token_cikti"], 47)
        # tool calls are counted once per tool_use id, even though the message streamed over several lines
        self.assertEqual(d["ozet"]["arac_cagrilari_ana"], {"Read": 1, "Bash": 1})
        self.assertEqual(d["oturumlar"][0]["adimlar"][0]["asistan_iletisi"], 2)


class HumanDetection(Base):
    def test_each_excluded_kind_is_not_a_human_message(self):
        events = [human(at(0), "real one")]
        excluded = [
            human(at(1), "meta", isMeta=True),
            human(at(1, 1), "side", isSidechain=True),
            human(at(1, 2), "<system-reminder>x</system-reminder>"),
            human(at(1, 3), "<task-notification>x"),
            human(at(1, 4), "<local-command-stdout>x"),
            human(at(1, 5), "Caveat: the messages below"),
            human(at(1, 6), "<side-sessions-event>x"),
            human(at(1, 7), "<ci-monitor-event>x"),
            human(at(1, 8), "relay text [Subagent hand-back] more"),
            human(at(1, 9), "Another Claude session sent a message: hi"),
            human(at(1, 10), "[SYSTEM NOTIFICATION] build done"),
            human_parts(at(1, 11), "<system-reminder>only reminder</system-reminder>"),
            result(at(1, 12), "unknown-tool"),
        ]
        events += excluded
        events += [human_parts(at(2), "second real one"), human(at(3), "  third real one ")]
        path = self.write("s.jsonl", events)
        d = self.data(path)
        self.assertEqual(d["ozet"]["insan_dokunusu"], 3)
        self.assertEqual([a["ad"] for a in d["oturumlar"][0]["adimlar"]], ["Tur 1", "Tur 2", "Tur 3"])

    def test_chapter_titles_name_steps(self):
        path = self.write("s.jsonl", [
            human(at(0)),
            assistant(at(0, 5), "m1", usage(o=1), blocks=[tool_use("c1", "mcp__ccd_session__mark_chapter", title="Paket yazimi")]),
            human(at(10)),
            assistant(at(10, 5), "m2", usage(o=1)),
            human(at(20)),
            assistant(at(20, 5), "m3", usage(o=1), blocks=[
                tool_use("c2", "mcp__x__mark_chapter", title="Ilk | baslik"),
                tool_use("c3", "mark_chapter", title="Ikinci baslik")]),
        ])
        steps = self.data(path)["oturumlar"][0]["adimlar"]
        self.assertEqual([s["ad"] for s in steps], ["Paket yazimi", "Tur 2", "Ilk / baslik"])


class ActiveAndWaiting(Base):
    def test_active_waiting_split_and_threshold(self):
        path = self.write("s.jsonl", [
            human(at(0)),
            assistant(at(1), "m1", usage(o=1)),          # +1 min active
            assistant(at(4), "m2", usage(o=1)),          # +3 min active
            human(at(34)),                                # +30 min waiting (person)
            assistant(at(36), "m3", usage(o=1)),         # +2 min active
        ])
        d = self.data(path)
        o = d["ozet"]
        self.assertEqual(o["duvar_saati_dk"], 36.0)
        self.assertEqual(o["is_dk"], 6.0)
        self.assertEqual(o["bekleme_dk"], 30.0)
        d2 = self.data(path, idle=2.5)       # 3 min gap is now waiting too; 1 and 2 min stay active
        self.assertEqual(d2["ozet"]["is_dk"], 3.0)
        self.assertEqual(d2["ozet"]["bekleme_dk"], 33.0)
        steps = d["oturumlar"][0]["adimlar"]
        self.assertEqual((steps[0]["is_dk"], steps[0]["bekleme_dk"]), (4.0, 30.0))  # the wait belongs to the step that ended
        self.assertEqual((steps[1]["is_dk"], steps[1]["bekleme_dk"]), (2.0, 0.0))
        self.assertEqual(o["rapor_satiri"], "Bu adım: 6 dk iş + 30 dk bekleme · 3 token · 2 dokunuş")

    def test_wait_classification(self):
        path = self.write("s.jsonl", [
            human(at(0)),
            assistant(at(1), "m1", usage(o=1), blocks=[tool_use("t1", "Bash", command="cd /private/SECRETDIR && git push origin SECRETBRANCH")]),
            result(at(61), "t1"),                         # 60 min between tool_use and tool_result
            assistant(at(62), "m2", usage(o=1)),
            human(at(100)),                               # 38 min waiting for the person
            assistant(at(120), "m3", usage(o=1)),         # 20 min nothing explains
        ])
        waits = self.data(path)["beklemeler"]
        self.assertEqual([(w["tur"], w["arac"], w["sure_dk"]) for w in waits], [
            (rr.WAIT_TOOL, "Bash: git push", 60.0),
            (rr.WAIT_HUMAN, None, 38.0),
            (rr.WAIT_OTHER, None, 20.0),
        ])

    def test_subagent_activity_fills_the_parent_wait(self):
        main = self.write("s.jsonl", [
            human(at(0)),
            assistant(at(1), "m1", usage(o=1), blocks=[tool_use("a1", "Agent", subagent_type="ees-isci", description="SECRETDESC")]),
            result(at(21), "a1"),
        ])
        sub = [{"type": "assistant", "isSidechain": True, "timestamp": at(m), "message": {
            "id": "s%d" % m, "model": "model-s", "usage": usage(o=3), "content": [{"type": "text", "text": "y"}]}}
            for m in (2, 4, 6, 8, 10, 12, 14, 16, 18, 20)]
        self.write("s/subagents/agent-aaa.jsonl", sub)
        o = self.data(main)["ozet"]
        self.assertEqual(o["bekleme_dk"], 0.0)
        self.assertEqual(o["is_dk"], 21.0)


class SubAgents(Base):
    def test_subagent_folder_type_model_tokens_and_reads(self):
        main = self.write("s.jsonl", [
            human(at(0)),
            assistant(at(1), "m1", usage(i=1, o=10), blocks=[
                tool_use("a1", "Agent", subagent_type="ees-denetci", description="SECRETDESC"),
                tool_use("a2", "Task", description="no type")]),
        ])
        s1 = [
            {"type": "user", "isSidechain": True, "timestamp": at(2), "message": {"role": "user", "content": "SECRETPROMPT"}},
            {"type": "assistant", "isSidechain": True, "timestamp": at(3), "message": {
                "id": "x1", "model": "model-s", "usage": usage(i=5, cw=6, cr=7, o=8), "content": [
                    tool_use("r1", "Read", file_path="C:\\work\\repo\\.ees\\kartlar\\hat.md"),
                    tool_use("r2", "Read", file_path="/work/other/secret.txt"),
                    tool_use("r3", "Bash", command="cat .antigravity/rules/multi-tenancy.md AGENTS.md")]}},
            {"type": "assistant", "isSidechain": True, "timestamp": at(5), "message": {
                "id": "x1", "model": "model-s", "usage": usage(i=5, cw=6, cr=7, o=9), "content": [{"type": "text", "text": "z"}]}},
        ]
        self.write("s/subagents/agent-one.jsonl", s1)
        with open(os.path.join(self.dir, "s/subagents/agent-one.meta.json"), "w", encoding="utf-8") as fh:
            json.dump({"agentType": "meta-type", "toolUseId": "a1"}, fh)
        s2 = [{"type": "assistant", "isSidechain": True, "timestamp": at(6), "message": {
            "id": "y1", "model": "model-t", "usage": usage(o=1), "content": [{"type": "text", "text": "q"}]}}]
        self.write("s/subagents/agent-two.jsonl", s2)  # no meta file: the file name is the type
        d = self.data(main)
        subs = {a["tur"]: a for a in d["oturumlar"][0]["alt_ajanlar"]}
        self.assertEqual(set(subs), {"ees-denetci", "agent-two"})
        one = subs["ees-denetci"]
        self.assertEqual(one["model"], "model-s")
        self.assertEqual(one["sure_dk"], 3.0)
        self.assertEqual(one["arac_cagrisi"], 3)
        self.assertEqual(one["token"]["model-s"], {"mesaj": 1, "girdi": 5, "onbellek_yazma": 6, "onbellek_okuma": 7, "cikti": 9})
        self.assertEqual(d["ozet"]["token_alt_ajan"]["model-t"]["cikti"], 1)
        self.assertEqual(d["oturumlar"][0]["adimlar"][0]["alt_ajan_baslatildi"], 2)
        reads = d["oturumlar"][0]["okunan_kural_dosyalari"]["alt_ajanlar"]
        key = [k for k in reads if k.startswith("ees-denetci")][0]
        self.assertEqual(reads[key], {".antigravity/rules/multi-tenancy.md": 1, ".ees/kartlar/hat.md": 1, "AGENTS.md": 1})

    def test_rule_files_main_chat_read_and_bash(self):
        path = self.write("s.jsonl", [
            human(at(0)),
            assistant(at(1), "m1", usage(o=1), blocks=[
                tool_use("r1", "Read", file_path="/Users/someone/repo/.ees/kartlar/cekirdek.md"),
                tool_use("r2", "Read", file_path="/Users/someone/repo/AGENTS.md"),
                tool_use("r3", "Read", file_path="/Users/someone/repo/README.md"),
                tool_use("r4", "Bash", command="sed -n 1,5p .ees/kartlar/cekirdek.md; grep x .ees/kartlar/hat.md")]),
        ])
        reads = self.data(path)["oturumlar"][0]["okunan_kural_dosyalari"]["ana"]
        self.assertEqual(reads, {".ees/kartlar/cekirdek.md": 2, ".ees/kartlar/hat.md": 1, "AGENTS.md": 1})


class Privacy(Base):
    def test_no_message_text_or_command_arguments_in_any_format(self):
        path = self.write("s.jsonl", [
            human(at(0), "SECRETTEXT-human"),
            assistant(at(1), "m1", usage(o=1), blocks=[
                {"type": "text", "text": "SECRETTEXT-assistant"},
                tool_use("t1", "Bash", command="cat /etc/SECRETARG.txt"),
                tool_use("t2", "Bash", command="git push origin SECRETBRANCH"),
                tool_use("t3", "Bash", command="FOO=SECRETENV npm run SECRETSCRIPT"),
                tool_use("t4", "Grep", pattern="SECRETPATTERN"),
                tool_use("t5", "Write", file_path="/home/SECRETPATH/a.txt", content="SECRETCONTENT"),
                tool_use("t6", "Agent", subagent_type="ees-isci", description="SECRETDESC", prompt="SECRETPROMPT")]),
            result(at(40), "t2"),
            human(at(41), "SECRETTEXT-second"),
        ])
        code_md, md = self.run_main(["--transcript", path])
        code_js, js = self.run_main(["--transcript", path, "--format", "json"])
        self.assertEqual((code_md, code_js), (0, 0))
        for text in (md, js):
            self.assertNotIn("SECRET", text)
            self.assertNotIn(self.dir, text)
        self.assertIn("Bash: git push", md)
        self.assertEqual(rr.bash_label("cat /etc/passwd"), "Bash: cat")
        self.assertEqual(rr.bash_label("cd /x && git -C /y/z push --force origin main"), "Bash: git push")
        self.assertEqual(rr.bash_label("FOO=1 ./run.sh --flag"), "Bash: run.sh")

    def test_pr_section_has_no_logins(self):
        path = self.write("s.jsonl", [human(at(0))])
        raw = fake_pr()
        raw["reviews"][0]["author"] = {"login": "SECRETLOGIN"}
        raw["commits"][0]["authors"] = [{"login": "SECRETLOGIN2", "name": "SECRETNAME"}]
        _, md = self.run_main(["--transcript", path, "--pr", "7"], gh=lambda repo, n: raw)
        self.assertNotIn("SECRET", md)
        self.assertIn("onaylayıcı", md)


class OutputFormats(Base):
    def test_json_is_one_object_with_the_same_numbers(self):
        path = self.write("s.jsonl", [
            human(at(0)), assistant(at(2), "m1", usage(i=1, cw=2, cr=3, o=4)),
        ])
        code, text = self.run_main(["--transcript", path, "--format", "json"])
        self.assertEqual(code, 0)
        obj = json.loads(text)
        self.assertEqual(set(obj), {"ozet", "oturumlar", "beklemeler", "prler"})
        self.assertIsNone(obj["prler"])
        self.assertEqual(obj["ozet"]["insan_dokunusu"], 1)
        self.assertEqual(obj["ozet"]["token_hepsi"], 10)
        self.assertEqual(obj["ozet"]["token_cikti"], 4)

    def test_markdown_has_the_turkish_headings(self):
        path = self.write("s.jsonl", [human(at(0)), assistant(at(1), "m1", usage(o=1))])
        _, md = self.run_main(["--transcript", path])
        for heading in ("## Özet", "## Adımlar", "## Beklemeler", "## Alt ajanlar", "## Okunan kural dosyaları"):
            self.assertIn(heading, md)
        self.assertNotIn("## PR'lar", md)

    def test_since_until_window_and_errors(self):
        path = self.write("s.jsonl", [
            human(at(0)), assistant(at(1), "m1", usage(o=1)),
            human(at(30)), assistant(at(31), "m2", usage(o=2)),
        ])
        code, text = self.run_main(["--transcript", path, "--format", "json", "--since", at(20).replace(".000Z", "Z")])
        self.assertEqual(code, 0)
        o = json.loads(text)["ozet"]
        self.assertEqual((o["insan_dokunusu"], o["token_cikti"], o["is_dk"]), (1, 2, 1.0))
        err = io.StringIO()
        with redirect_stderr(err):
            self.assertEqual(rr.main(["--transcript", os.path.join(self.dir, "missing.jsonl")], stdout=io.StringIO()), 2)
            self.assertEqual(rr.main(["--transcript", path, "--since", "yesterday"], stdout=io.StringIO()), 2)

    def test_parse_ts_variants(self):
        self.assertEqual(rr.parse_ts("2026-11-16T10:00:00.123Z"), T0.replace(microsecond=123000))
        self.assertEqual(rr.parse_ts("2026-11-16T13:00:00+03:00"), T0)
        self.assertEqual(rr.parse_ts("2026-11-16T10:00:00"), T0)
        self.assertEqual(rr.parse_ts("2026-11-16"), T0.replace(hour=0))
        self.assertIsNone(rr.parse_ts("nonsense"))


def fake_pr():
    return {
        "createdAt": "2026-11-16T10:00:00Z", "mergedAt": "2026-11-16T12:00:00Z", "closedAt": "2026-11-16T12:00:00Z",
        "isDraft": False,
        "reviews": [
            {"state": "APPROVED", "submittedAt": "2026-11-16T10:30:00Z"},
            {"state": "COMMENTED", "submittedAt": "2026-11-16T10:10:00Z"},
        ],
        "commits": [
            {"committedDate": "2026-11-16T09:00:00Z"},
            {"committedDate": "2026-11-16T10:20:00Z"},
        ],
        "statusCheckRollup": [
            {"status": "COMPLETED", "conclusion": "SUCCESS", "completedAt": "2026-11-16T10:35:00Z"},
            {"status": "COMPLETED", "conclusion": "SKIPPED", "completedAt": "2026-11-16T10:25:00Z"},
            {"status": "COMPLETED", "conclusion": "FAILURE", "completedAt": "2026-11-16T10:40:00Z"},
        ],
    }


class PullRequests(Base):
    def test_pr_section_with_fake_gh(self):
        path = self.write("s.jsonl", [human(at(0))])
        calls = []

        def gh(repo, number):
            calls.append((repo, number))
            if number == 9:
                raise RuntimeError("SECRETERROR token")
            return fake_pr()

        d = self.data(path, prs=[7, 9], repo="o/r", gh=gh)
        self.assertEqual(calls, [("o/r", 7), ("o/r", 9)])
        p7, p9 = d["prler"]
        self.assertEqual(p7["ilk_inceleme"]["durum"], "COMMENTED")      # earliest review, not the strongest
        self.assertEqual(p7["ilk_inceleme"]["acilistan_sonra_dk"], 10.0)
        self.assertEqual(p7["inceleme_sayisi"], 2)
        self.assertEqual(p7["durum"], "birleşti")
        self.assertEqual(p7["kontrol"], {"toplam": 3, "gecen": 2, "kalan": 1, "bekleyen": 0})
        self.assertEqual(p7["son_commit_kontrol_bitis_dk"], 20.0)       # 10:20 -> 10:40
        self.assertEqual(p7["acilistan_kontrol_bitis_dk"], 40.0)
        self.assertEqual(p9, {"no": 9, "hata": "okunamadı (RuntimeError)"})
        _, md = self.run_main(["--transcript", path, "--pr", "7", "--repo", "o/r"], gh=lambda r, n: fake_pr())
        self.assertIn("### PR #7", md)
        self.assertIn("2 geçti · 1 kaldı", md)

    def test_unfinished_checks_leave_the_commit_to_checks_figure_empty(self):
        raw = fake_pr()
        raw["statusCheckRollup"][2] = {"status": "IN_PROGRESS", "conclusion": "", "completedAt": ""}
        raw["mergedAt"] = None
        raw["closedAt"] = None
        out = rr.pr_summary(1, None, lambda r, n: raw)
        self.assertIsNone(out["son_commit_kontrol_bitis_dk"])
        self.assertEqual(out["kontrol"]["bekleyen"], 1)
        self.assertEqual(out["durum"], "açık")

    def test_without_pr_no_gh_call_is_made(self):
        path = self.write("s.jsonl", [human(at(0))])

        def boom(repo, number):
            raise AssertionError("gh must not be called without --pr")

        code, _ = self.run_main(["--transcript", path], gh=boom)
        self.assertEqual(code, 0)


class MultipleTranscripts(Base):
    def test_two_sessions_merge_into_one_summary(self):
        a = self.write("a.jsonl", [human(at(0)), assistant(at(1), "m1", usage(o=1))])
        b = self.write("b.jsonl", [human(at(0, 30)), assistant(at(1, 30), "m2", usage(o=2))])
        d = rr.build_data([a, b], [], None, 5, None, None)
        self.assertEqual(d["ozet"]["oturum_sayisi"], 2)
        self.assertEqual(d["ozet"]["insan_dokunusu"], 2)
        self.assertEqual(d["ozet"]["token_cikti"], 3)
        self.assertEqual(d["ozet"]["duvar_saati_dk"], 1.5)   # overlapping sessions are not added up
        md = rr.render_md(d)
        self.assertIn("### Oturum 1", md)
        self.assertIn("### Oturum 2", md)


if __name__ == "__main__":
    unittest.main()
