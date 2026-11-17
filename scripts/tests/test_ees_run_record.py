"""scripts/ees_run_record.py - the EES run record built from session transcripts.

Run by scripts/run_phase1_gates.sh:

    python3 -m unittest discover -s scripts/tests -p 'test_*.py'

Every transcript here is SYNTHETIC and written to a temp directory. The pull-request section uses a fake `gh` function:
no test touches the network. The privacy tests plant HOSTILE free text (secrets, e-mail, newlines, pipes, path tricks)
in every field the tool reads and check that none of it reaches the Markdown or the JSON output.
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


def queued(ts, uuid=None, **att):
    a = {"type": "queued_command", "commandMode": "prompt", "origin": {"kind": "human"}, "prompt": "SECRETQUEUED"}
    a.update(att)
    e = {"type": "attachment", "timestamp": ts, "attachment": a}
    if uuid:
        e["uuid"] = uuid
    return e


def result(ts, tool_id, **extra):
    e = {"type": "user", "timestamp": ts, "message": {"role": "user", "content": [
        {"type": "tool_result", "tool_use_id": tool_id, "content": "SECRETRESULT"}]}}
    e.update(extra)
    return e


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


def sub_assistant(ts, mid, blocks=None, model="model-s", u=None):
    return {"type": "assistant", "isSidechain": True, "timestamp": ts, "message": {
        "id": mid, "model": model, "usage": u or usage(o=1), "content": blocks or [{"type": "text", "text": "y"}]}}


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

    def meta(self, name, **kw):
        with open(os.path.join(self.dir, name), "w", encoding="utf-8") as fh:
            json.dump(kw, fh)

    def data(self, path, **kw):
        paths = path if isinstance(path, list) else [path]
        return rr.build_data(paths, kw.pop("prs", []), kw.pop("repo", None), kw.pop("idle", 5), kw.pop("since", None),
                             kw.pop("until", None), gh=kw.pop("gh", None), tur_adlari=kw.pop("tur_adlari", False),
                             adim=kw.pop("adim", None), limits=kw.pop("limits", None), now=kw.pop("now", None))

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
        # a tool_use that streams again with the same id (t2 appears twice) is still one call
        self.assertEqual(d["ozet"]["arac_cagrilari_ana"], {"Read": 1, "Bash": 1})
        self.assertEqual(d["turlar"][0]["asistan_iletisi"], 2)

    def test_non_string_ids_and_models_do_not_crash(self):
        events = [
            human(at(0)),
            {"type": "assistant", "timestamp": at(1), "message": {"id": 12345, "model": ["x"], "usage": usage(o=3), "content": [
                {"type": "tool_use", "id": {"a": 1}, "name": ["Bash"], "input": {"command": ["git", "push"]}},
                {"type": "tool_use", "id": 77, "name": "Read", "input": {"file_path": 5}}]}},
            {"type": "assistant", "timestamp": at(2), "message": {"id": None, "model": 7, "usage": "bad", "content": "text"}},
            {"type": "user", "timestamp": at(3), "uuid": 99, "message": {"content": [{"type": "tool_result", "tool_use_id": 5}]}},
            {"type": "user", "timestamp": at(4), "message": None},
        ]
        d = self.data(self.write("s.jsonl", events))
        self.assertEqual(d["ozet"]["token_cikti"], 3)


class HumanDetection(Base):
    def test_each_excluded_kind_is_not_a_human_message(self):
        events = [human(at(0), "real one")]
        excluded = [
            human(at(1), "meta", isMeta=True),
            human(at(1, 1), "side", isSidechain=True),
            human(at(1, 2), "<system-reminder>x</system-reminder>"),
            human(at(1, 3), "<task-notification>x"),
            human(at(1, 4), "<local-command-stdout>x"),
            human(at(1, 5), "Caveat: The messages below were generated by the user while running local commands."),
            human(at(1, 6), "<side-sessions-event>x"),
            human(at(1, 7), "<ci-monitor-event>x"),
            human(at(1, 8), "[Subagent hand-back] more"),
            human(at(1, 9), "Another Claude session sent a message: hi"),
            human(at(1, 10), "[SYSTEM NOTIFICATION] build done"),
            human_parts(at(1, 11), "<system-reminder>only reminder</system-reminder>"),
            human(at(1, 12), "<bash-stdout>x</bash-stdout><bash-stderr></bash-stderr>"),
            human(at(1, 13), "<user-prompt-submit-hook>x</user-prompt-submit-hook>"),
            human(at(1, 14), "This session is being continued from a previous conversation", isCompactSummary=True),
            result(at(1, 15), "unknown-tool"),
        ]
        events += excluded
        events += [human_parts(at(2), "second real one"), human(at(3), "  third real one ")]
        d = self.data(self.write("s.jsonl", events))
        self.assertEqual(d["ozet"]["insan_dokunusu"], 3)
        self.assertEqual([a["ad"] for a in d["turlar"]], ["Tur 1", "Tur 2", "Tur 3"])

    def test_quoted_markers_and_plain_caveat_are_human(self):
        events = [
            human(at(0), "Caveat: do not push yet"),
            human(at(1), "please read the [Subagent hand-back] text above"),
            human(at(2), "he wrote: Another Claude session sent a message"),
        ]
        self.assertEqual(self.data(self.write("s.jsonl", events))["ozet"]["insan_dokunusu"], 3)

    def test_origin_kind_decides_when_present(self):
        events = [
            human(at(0), "<system-reminder>looks automated</system-reminder>", origin={"kind": "human"}),   # human
            human(at(1), "plain words typed by nobody", origin={"kind": "peer"}),                           # not human
            human(at(2), "x", origin={"kind": "task-notification"}),                                        # not human
            human(at(3), "meta but origin says human", isMeta=True, origin={"kind": "human"}),              # human
            human(at(4), "no origin, plain text"),                                                          # heuristic: human
            human(at(5), "<task-notification>no origin"),                                                   # heuristic: not human
        ]
        d = self.data(self.write("s.jsonl", events))
        self.assertEqual(d["ozet"]["insan_dokunusu"], 3)

    def test_messages_typed_while_the_assistant_works_are_counted(self):
        events = [
            human(at(0), origin={"kind": "human"}),
            assistant(at(1), "m1", usage(o=1)),
            queued(at(2), uuid="q1"),                                              # human, queued
            queued(at(2, 5), uuid="q2", origin={"kind": "peer"}, isMeta=True),     # another session: not human
            queued(at(3), uuid="q3", commandMode="task-notification", origin={"kind": "task-notification"}),
            {"type": "attachment", "timestamp": at(4), "uuid": "q4",
             "attachment": {"type": "queued_command", "commandMode": "prompt", "prompt": "no origin field"}},  # fallback
            {"type": "attachment", "timestamp": at(5), "uuid": "q5", "attachment": {"type": "date"}},
            queued(at(6), uuid="q1"),                                              # same uuid again: once
        ]
        d = self.data(self.write("s.jsonl", events))
        self.assertEqual(d["ozet"]["insan_dokunusu"], 3)
        self.assertEqual(d["ozet"]["calisma_sirasinda_yazilan"], 2)
        self.assertEqual(len(d["turlar"]), 3)


class TurnNames(Base):
    def _events(self):
        return [
            human(at(0)),
            assistant(at(0, 5), "m1", usage(o=1), blocks=[tool_use("c1", "mcp__ccd_session__mark_chapter", title="Paket yazimi")]),
            human(at(10)),
            assistant(at(10, 5), "m2", usage(o=1)),
            human(at(20)),
            assistant(at(20, 5), "m3", usage(o=1), blocks=[
                tool_use("c2", "mcp__x__mark_chapter",
                         title="Ilk | baslik mail ali@example.com /home/u/x a=b C:\\x ABCDEFGHIJKLMNOPQRSTUVWXYZ0123 son"),
                tool_use("c3", "mark_chapter", title="Ikinci baslik")]),
        ]

    def test_default_rows_are_numbered_not_named(self):
        path = self.write("s.jsonl", self._events())
        d = self.data(path)
        self.assertEqual([t["ad"] for t in d["turlar"]], ["Tur 1", "Tur 2", "Tur 3"])
        for text in (rr.render_md(d), json.dumps(d, ensure_ascii=False)):
            self.assertNotIn("Paket yazimi", text)
            self.assertNotIn("example.com", text)

    def test_tur_adlari_names_turns_and_sanitises(self):
        path = self.write("s.jsonl", self._events())
        d = self.data(path, tur_adlari=True)
        names = [t["ad"] for t in d["turlar"]]
        self.assertEqual(names, ["Paket yazimi", "Tur 2", "Ilk / baslik mail son"])
        text = rr.render_md(d) + json.dumps(d, ensure_ascii=False)
        for bad in ("example.com", "/home", "a=b", "C:", "ABCDEFGHIJ"):
            self.assertNotIn(bad, text)
        self.assertLessEqual(len(rr.sanitize_title("word " * 40)), 60)
        code, md = self.run_main(["--transcript", path, "--tur-adlari"])
        self.assertIn("Paket yazimi", md)
        code, md = self.run_main(["--transcript", path])
        self.assertNotIn("Paket yazimi", md)


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
        turns = d["turlar"]
        self.assertEqual((turns[0]["is_dk"], turns[0]["bekleme_dk"]), (4.0, 30.0))  # the wait belongs to the turn that ended
        self.assertEqual((turns[1]["is_dk"], turns[1]["bekleme_dk"]), (2.0, 0.0))
        self.assertEqual(o["rapor_satiri"], "Bu adım: 6 dk iş + 30 dk bekleme · 3 token · 2 dokunuş")

    def test_a_gap_exactly_at_the_threshold_is_waiting(self):
        path = self.write("s.jsonl", [human(at(0)), assistant(at(5), "m1", usage(o=1)), assistant(at(9, 59), "m2", usage(o=1))])
        o = self.data(path, idle=5)["ozet"]
        self.assertEqual((o["is_dk"], o["bekleme_dk"]), (5.0, 5.0))    # 5:00 gap = waiting, 4:59 gap = active
        waits = self.data(path, idle=5)["beklemeler"]
        self.assertEqual([w["sure_dk"] for w in waits], [5.0])

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

    def test_open_tool_call_rule_names_the_tool_open_longest(self):
        path = self.write("s.jsonl", [
            human(at(0)),
            assistant(at(1), "m1", usage(o=1), blocks=[tool_use("a", "Bash", command="sleep 9"), tool_use("b", "Grep", pattern="x")]),
            result(at(2), "b"),
            result(at(40), "a"),
        ])
        waits = self.data(path)["beklemeler"]
        self.assertEqual([(w["tur"], w["arac"]) for w in waits], [(rr.WAIT_TOOL, "Bash: sleep")])

    def test_open_call_wins_even_when_a_person_message_ends_the_gap(self):
        path = self.write("s.jsonl", [
            human(at(0)),
            assistant(at(1), "m1", usage(o=1), blocks=[tool_use("a", "Edit", file_path="/x")]),   # permission prompt, never answered
            human(at(50)),
        ])
        self.assertEqual([(w["tur"], w["arac"]) for w in self.data(path)["beklemeler"]], [(rr.WAIT_TOOL, "Edit")])

    def test_a_call_that_never_got_a_result_does_not_stay_open_after_the_next_message(self):
        path = self.write("s.jsonl", [
            human(at(0)),
            assistant(at(1), "m1", usage(o=1), blocks=[tool_use("a", "Bash", command="ls")]),   # interrupted: no result
            assistant(at(2), "m2", usage(o=1)),
            human(at(60)),
        ])
        self.assertEqual([(w["tur"], w["arac"]) for w in self.data(path)["beklemeler"]], [(rr.WAIT_HUMAN, None)])

    def test_subagent_bash_waiting_33_minutes_is_a_tool_wait(self):
        main = self.write("s.jsonl", [
            human(at(0)),
            assistant(at(1), "m1", usage(o=1), blocks=[tool_use("ag", "Agent", subagent_type="ees-isci")]),
            result(at(40), "ag"),
        ])
        sub = [
            sub_assistant(at(2), "s1", blocks=[tool_use("sb", "Bash", command="git push origin x")]),
            {"type": "user", "isSidechain": True, "timestamp": at(35), "message": {"content": [{"type": "tool_result", "tool_use_id": "sb"}]}},
            sub_assistant(at(36), "s2"),
        ]
        self.write("s/subagents/agent-z.jsonl", sub)
        waits = self.data(main)["beklemeler"]
        self.assertEqual(waits[0]["arac"], "Bash: git push")
        self.assertEqual(waits[0]["tur"], rr.WAIT_TOOL)
        self.assertEqual(waits[0]["sure_dk"], 33.0)

    def test_main_long_command_while_a_background_subagent_works(self):
        main = self.write("s.jsonl", [
            human(at(0)),
            assistant(at(1), "m1", usage(o=1), blocks=[tool_use("ag", "Agent", subagent_type="ees-isci", run_in_background=True)]),
            result(at(1, 5), "ag"),
            assistant(at(1.5), "m2", usage(o=1), blocks=[tool_use("bb", "Bash", command="dotnet test --filter SECRETFILTER")]),
            result(at(40), "bb"),
        ])
        self.write("s/subagents/agent-q.jsonl", [sub_assistant(at(2), "q1"), sub_assistant(at(20), "q2")])
        waits = self.data(main)["beklemeler"]
        self.assertEqual([(w["tur"], w["arac"], w["sure_dk"]) for w in waits], [(rr.WAIT_TOOL, "Bash: dotnet test", 20.0),
                                                                          (rr.WAIT_TOOL, "Bash: dotnet test", 18.0)])

    def test_a_subagent_event_alone_is_not_a_main_tool_wait(self):
        main = self.write("s.jsonl", [human(at(0)), assistant(at(1), "m1", usage(o=1))])
        self.write("s/subagents/agent-q.jsonl", [sub_assistant(at(2), "q1"), sub_assistant(at(40), "q2")])
        waits = self.data(main)["beklemeler"]
        self.assertEqual([(w["tur"], w["arac"]) for w in waits], [(rr.WAIT_OTHER, None)])

    def test_subagent_activity_fills_the_parent_wait(self):
        main = self.write("s.jsonl", [
            human(at(0)),
            assistant(at(1), "m1", usage(o=1), blocks=[tool_use("a1", "Agent", subagent_type="ees-isci", description="SECRETDESC")]),
            result(at(21), "a1"),
        ])
        self.write("s/subagents/agent-aaa.jsonl", [sub_assistant(at(m), "s%d" % m) for m in range(2, 21, 2)])
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
            sub_assistant(at(3), "x1", u=usage(i=5, cw=6, cr=7, o=8), blocks=[
                tool_use("r1", "Read", file_path="C:\\work\\repo\\.ees\\kartlar\\hat.md"),
                tool_use("r2", "Read", file_path="/work/other/secret.txt"),
                tool_use("r3", "Bash", command="cat .antigravity/rules/multi-tenancy.md AGENTS.md")]),
            sub_assistant(at(5), "x1", u=usage(i=5, cw=6, cr=7, o=9)),
        ]
        self.write("s/subagents/agent-one.jsonl", s1)
        self.meta("s/subagents/agent-one.meta.json", agentType="meta-type", toolUseId="a1")
        self.write("s/subagents/agent-two.jsonl", [sub_assistant(at(6), "y1", model="model-t", u=usage(o=1))])
        d = self.data(main)
        subs = {a["tur"]: a for a in d["oturumlar"][0]["alt_ajanlar"]}
        self.assertEqual(set(subs), {"ees-denetci", "alt-ajan-2"})   # no meta, no parent type: numbered, never the file name
        one = subs["ees-denetci"]
        self.assertEqual(one["model"], "model-s")
        self.assertEqual(one["sure_dk"], 3.0)
        self.assertEqual(one["arac_cagrisi"], 3)
        self.assertEqual(one["token"]["model-s"], {"mesaj": 1, "girdi": 5, "onbellek_yazma": 6, "onbellek_okuma": 7, "cikti": 9})
        self.assertEqual(d["ozet"]["token_alt_ajan"]["model-t"]["cikti"], 1)
        self.assertEqual(d["turlar"][0]["alt_ajan_baslatildi"], 2)
        reads = d["oturumlar"][0]["okunan_kural_dosyalari"]["alt_ajanlar"]
        key = [k for k in reads if k.startswith("ees-denetci")][0]
        self.assertEqual(reads[key], {".antigravity/rules/multi-tenancy.md": 1, ".ees/kartlar/hat.md": 1, "AGENTS.md": 1})

    def test_meta_type_is_the_fallback_when_the_parent_call_cannot_be_matched(self):
        main = self.write("s.jsonl", [human(at(0)), assistant(at(1), "m1", usage(o=1))])
        self.write("s/subagents/agent-one.jsonl", [sub_assistant(at(2), "x1")])
        self.meta("s/subagents/agent-one.meta.json", agentType="ees-isci", toolUseId="not-in-this-window")
        self.assertEqual(self.data(main)["oturumlar"][0]["alt_ajanlar"][0]["tur"], "ees-isci")


class RuleFiles(Base):
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

    def test_only_clean_paths_at_a_boundary_are_kept(self):
        ok = [
            (".ees/kartlar/hat.md", True), ("/Users/a/repo/.ees/x.md", True), (".antigravity/rules/a-b_c.md", True),
            ("C:\\w\\.ees\\k\\h.md", True), ("/r/AGENTS.md", True),
            ("secrets.ees/key.md", False), ("/home/u/secrets.ees/LEAK", False), (".ees/../../etc/passwd", False),
            (".ees/x,ghp_TOKEN", False), (".ees/kartlar/$f.md", False), (".ees/ajanlar/*.md", False),
            (".antigravity/rules/x{\"pw\":1}", False), (".ees/a b.md", False), ("xAGENTS.md", False), ("AGENTS.md.bak", False),
        ]
        for path, expected in ok:
            self.assertEqual(bool(rr.rule_paths(path, whole=True)), expected, path)
        self.assertEqual(rr.rule_paths("cat .ees/x,ghp_LEAK .ees/ok.md; ls secrets.ees/y"), [".ees/ok.md"])
        self.assertEqual(rr.rule_paths("echo 'x' > .antigravity/rules/x{\"pw\":\"LEAK\"}"), [])


class BashLabels(Base):
    def test_allow_list_of_command_and_subcommand_pairs(self):
        cases = {
            "git push origin SECRET": "Bash: git push", "git pull": "Bash: git pull", "git fetch --all": "Bash: git fetch",
            "git commit -m SECRET": "Bash: git commit", "git merge x": "Bash: git merge", "git status": "Bash: git status",
            "git diff HEAD": "Bash: git diff", "git log -3": "Bash: git log", "git worktree add y": "Bash: git worktree",
            "git -C /x/y push": "Bash: git push", "git checkout SECRETBRANCH": "Bash: git",
            "git rebase main": "Bash: git", "gh pr view 5": "Bash: gh pr", "gh api repos/x": "Bash: gh api",
            "gh run list": "Bash: gh run", "gh secret set SECRET": "Bash: gh",
            "dotnet test x": "Bash: dotnet test", "dotnet build": "Bash: dotnet build", "dotnet ef migrations": "Bash: dotnet",
            "npm test": "Bash: npm test", "npm run SECRETSCRIPT": "Bash: npm run", "npm install SECRET": "Bash: npm",
            "python3 secret_script.py": "Bash: python3", "python3 -m unittest": "Bash: python3",
            "make deploy-SECRET": "Bash: make", "npx SECRETTOOL": "Bash: npx", "cat /etc/SECRET": "Bash: cat",
            "FOO=SECRET npm run x": "Bash: npm run", "cd /p && git push": "Bash: git push",
            "./ad.soyad --x": "Bash", "ad.soyad --x": "Bash", "sshpass -p SECRET ssh h": "Bash: sshpass",
            "": "Bash", "   ": "Bash",
        }
        for command, expected in cases.items():
            self.assertEqual(rr.bash_label(command), expected, command)
        self.assertEqual(rr.bash_label(None), "Bash")


class Privacy(Base):
    HOSTILE = "LEAK alice@example.com\nline2 | pipe"

    def _hostile_run(self, fmt):
        main = self.write("SECRETSESSION-alice.jsonl", [
            human(at(0), "SECRETTEXT-human"),
            assistant(at(1), "m1", usage(o=1), model="model-LEAKM alice@example.com\nx", blocks=[
                {"type": "text", "text": "SECRETTEXT-assistant"},
                tool_use("t2", "Bash", command="git push origin SECRETBRANCH"),
                tool_use("t1", "Bash", command="cat /etc/SECRETARG.txt"),
                tool_use("t3", "Bash", command="FOO=SECRETENV npm run SECRETSCRIPT"),
                tool_use("t4", "mcp__evil__ghp_LEAK4 tool\nname", pattern="SECRETPATTERN"),
                tool_use("t5", "Write", file_path="/home/SECRETPATH/a.txt", content="SECRETCONTENT"),
                tool_use("t6", "Agent", subagent_type=self.HOSTILE, description="SECRETDESC", prompt="SECRETPROMPT"),
                tool_use("t7", "Read", file_path="/Users/alice/.ees/../../etc/LEAKpasswd"),
                tool_use("t8", "Read", file_path="/home/u/secrets.ees/LEAKkey"),
                tool_use("t9", "Bash", command="cat .ees/x,ghp_LEAKTOKEN"),
                tool_use("t10", "mcp__ccd_session__mark_chapter", title="Deploy with key sk-LEAK5 for ali@x.com")]),
            result(at(40), "t2"),
            human(at(41), "SECRETTEXT-second"),
        ])
        self.write("SECRETSESSION-alice/subagents/agent-LEAKSTEM-bob.jsonl", [
            sub_assistant(at(2), "s1", model="model-LEAKSUB\n|x", blocks=[tool_use("u1", "Read", file_path=".ees/ok.md")])])
        self.meta("SECRETSESSION-alice/subagents/agent-LEAKSTEM-bob.meta.json", agentType="LEAK13 Bob Smith\nx|y")
        raw = fake_pr()
        raw["reviews"][0]["state"] = "LEAK14 approved by alice"
        raw["reviews"][0]["author"] = {"login": "SECRETLOGIN"}
        raw["reviews"][0]["body"] = "LEAKBODY"
        raw["commits"][0]["authors"] = [{"login": "SECRETLOGIN2", "name": "SECRETNAME"}]
        raw["statusCheckRollup"][0]["name"] = "LEAKCHECK"
        code, text = self.run_main(["--transcript", main, "--format", fmt, "--pr", "5"], gh=lambda r, n: raw)
        self.assertEqual(code, 0)
        return text

    def test_no_free_text_reaches_md_or_json(self):
        for fmt in ("md", "json"):
            text = self._hostile_run(fmt)
            for bad in ("SECRET", "LEAK", "alice", "example.com", "Bob Smith", "etc/", self.dir):
                self.assertNotIn(bad, text, "%s output leaked %r" % (fmt, bad))
            self.assertIn("onaylayıcı" if fmt == "md" else "onaylayici", text)
        md = self._hostile_run("md")
        self.assertIn("Bash: git push", md)
        self.assertIn("alt-ajan-1", md)
        self.assertIn("oturum", self._hostile_run("json"))
        for line in md.splitlines():
            if line.startswith("|"):
                self.assertEqual(line.count("|"), line.count(" | ") + 2 if line.count(" | ") else line.count("|"))

    def test_names_are_whitelisted(self):
        self.assertEqual(rr.ident("claude-opus-5-5"), "claude-opus-5-5")
        self.assertEqual(rr.ident("mcp__ccd_session__mark_chapter"), "mcp__ccd_session__mark_chapter")
        self.assertEqual(rr.ident("ees-denetci"), "ees-denetci")
        for bad in ("a b", "a\nb", "a|b", "x" * 61, "", "ali@x.com", "a/b", None, 5, ["x"]):
            self.assertEqual(rr.ident(bad), "?", repr(bad))
        self.assertEqual(rr.ident("a\n"), "?")      # a trailing newline must not slip through

    def test_one_cleaning_function_for_every_cell(self):
        self.assertEqual(rr.cell("a|b\nc\r\td\x00e"), "a/b c d e")
        rows = rr.table(["H|1", "H\n2"], [["x|y", "z\nw"], [5, None]])
        for line in rows:
            self.assertEqual(len(line.splitlines()), 1)
        self.assertEqual(rr.count_line({"A|B": 1, "C\nD": 2}), "A/B 1 · C D 2")

    def test_review_state_only_through_the_fixed_map(self):
        for state, expected in (("APPROVED", "onaylandı"), ("CHANGES_REQUESTED", "değişiklik istendi"),
                                ("approved by SECRET", "diğer"), (None, "diğer"), (["x"], "diğer"), ("", "diğer")):
            raw = fake_pr()
            raw["reviews"] = [{"state": state, "submittedAt": "2026-11-16T10:05:00Z"}]
            out = rr.pr_summary(1, None, lambda r, n: raw)
            self.assertEqual(out["ilk_inceleme"]["durum_tr"], expected)
            self.assertIn(out["ilk_inceleme"]["durum"], list(rr.REVIEW_TR) + ["diger"])

    def test_pr_section_has_no_logins_in_md_and_json(self):
        path = self.write("s.jsonl", [human(at(0))])
        raw = fake_pr()
        raw["reviews"][0]["author"] = {"login": "SECRETLOGIN"}
        raw["commits"][0]["authors"] = [{"login": "SECRETLOGIN2", "name": "SECRETNAME"}]
        for fmt in ("md", "json"):
            _, text = self.run_main(["--transcript", path, "--pr", "7", "--format", fmt], gh=lambda repo, n: raw)
            self.assertNotIn("SECRET", text)
            self.assertIn("onaylayıcı" if fmt == "md" else "onaylayici", text)


class OutputFormats(Base):
    def test_json_is_one_object_with_the_same_numbers(self):
        path = self.write("s.jsonl", [human(at(0)), assistant(at(2), "m1", usage(i=1, cw=2, cr=3, o=4))])
        code, text = self.run_main(["--transcript", path, "--format", "json"])
        self.assertEqual(code, 0)
        obj = json.loads(text)
        self.assertEqual(set(obj), {"ozet", "turlar", "beklemeler", "oturumlar", "prler", "uyarilar"})
        self.assertIsNone(obj["prler"])
        self.assertEqual(obj["ozet"]["insan_dokunusu"], 1)
        self.assertEqual(obj["ozet"]["token_hepsi"], 10)
        self.assertEqual(obj["ozet"]["token_cikti"], 4)
        self.assertEqual(obj["oturumlar"][0]["ad"], "oturum-1")

    def test_markdown_has_the_turkish_headings(self):
        path = self.write("s.jsonl", [human(at(0)), assistant(at(1), "m1", usage(o=1))])
        _, md = self.run_main(["--transcript", path])
        for heading in ("## Özet", "## Uyarılar", "## Turlar", "## Beklemeler", "## Alt ajanlar", "## Okunan kural dosyaları"):
            self.assertIn(heading, md)
        self.assertNotIn("## PR'lar", md)
        self.assertNotIn("## Adımlar", md)

    def test_since_and_until_window_and_errors(self):
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
        self.assertNotIn(self.dir, err.getvalue())

    def test_date_only_until_means_the_end_of_that_day(self):
        path = self.write("s.jsonl", [
            human(at(0)), assistant(at(1), "m1", usage(o=1)),                       # 2026-11-16 10:00
            human(at(14 * 60 + 30)), assistant(at(14 * 60 + 31), "m2", usage(o=2)),  # 2026-11-17 00:30
            human(at(36 * 60)),                                                      # 2026-11-17 22:00
        ])
        _, text = self.run_main(["--transcript", path, "--format", "json", "--until", "2026-11-16"])
        self.assertEqual(json.loads(text)["ozet"]["insan_dokunusu"], 1)
        _, text = self.run_main(["--transcript", path, "--format", "json", "--until", "2026-11-17"])
        self.assertEqual(json.loads(text)["ozet"]["insan_dokunusu"], 3)
        _, text = self.run_main(["--transcript", path, "--format", "json", "--until", "2026-11-16T23:59:59Z"])
        self.assertEqual(json.loads(text)["ozet"]["insan_dokunusu"], 1)
        _, text = self.run_main(["--transcript", path, "--format", "json", "--since", "2026-11-17"])
        self.assertEqual(json.loads(text)["ozet"]["insan_dokunusu"], 2)
        self.assertIn("UTC", rr.build_parser().format_help())

    def test_parse_ts_variants(self):
        self.assertEqual(rr.parse_ts("2026-11-16T10:00:00.123Z"), T0.replace(microsecond=123000))
        self.assertEqual(rr.parse_ts("2026-11-16T13:00:00+03:00"), T0)
        self.assertEqual(rr.parse_ts("2026-11-16T10:00:00"), T0)
        self.assertEqual(rr.parse_ts("2026-11-16"), T0.replace(hour=0))
        self.assertIsNone(rr.parse_ts("nonsense"))


class ProgressBar(Base):
    def test_adim_prints_the_bar_line_as_the_template_wants_it(self):
        path = self.write("s.jsonl", [human(at(0)), assistant(at(1), "m1", usage(o=1))])
        self.assertEqual(rr.progress_bar(4, 8), "İlerleme: ▓▓▓▓░░░░ 4/8 adım")
        self.assertEqual(rr.progress_bar(0, 5), "İlerleme: ░░░░░░░░ 0/5 adım")
        self.assertEqual(rr.progress_bar(5, 5), "İlerleme: ▓▓▓▓▓▓▓▓ 5/5 adım")
        self.assertEqual(rr.progress_bar(1, 3), "İlerleme: ▓▓▓░░░░░ 1/3 adım")     # round(8/3) = 3
        self.assertEqual(rr.progress_bar(1, 8), "İlerleme: ▓░░░░░░░ 1/8 adım")
        code, md = self.run_main(["--transcript", path, "--adim", "4/8"])
        self.assertEqual(code, 0)
        self.assertIn("İlerleme: ▓▓▓▓░░░░ 4/8 adım\n", md)
        self.assertIn("tahmini bitiş", md)
        code, text = self.run_main(["--transcript", path, "--adim", "3/8", "--format", "json"])
        self.assertEqual(json.loads(text)["ozet"]["ilerleme"], "İlerleme: ▓▓▓░░░░░ 3/8 adım")
        _, md = self.run_main(["--transcript", path])
        self.assertNotIn("İlerleme", md)
        err = io.StringIO()
        with redirect_stderr(err):
            for bad in ("9/8", "x", "4/0", "4-8"):
                self.assertEqual(rr.main(["--transcript", path, "--adim", bad], stdout=io.StringIO()), 2, bad)


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
        self.assertEqual(p9, {"no": 9, "hata": "PR okunamadı"})
        _, md = self.run_main(["--transcript", path, "--pr", "7", "--repo", "o/r"], gh=lambda r, n: fake_pr())
        self.assertIn("### PR #7", md)
        self.assertIn("2 geçti · 1 kaldı", md)

    def test_missing_gh_or_failing_gh_is_said_in_plain_turkish_and_exit_is_zero(self):
        path = self.write("s.jsonl", [human(at(0))])

        def no_gh(repo, number):
            raise FileNotFoundError("gh")

        def bad_gh(repo, number):
            raise RuntimeError("401 SECRET")

        code, md = self.run_main(["--transcript", path, "--pr", "1"], gh=no_gh)
        self.assertEqual(code, 0)
        self.assertIn("gh bulunamadı", md)
        code, md = self.run_main(["--transcript", path, "--pr", "1", "--format", "json"], gh=bad_gh)
        self.assertEqual(code, 0)
        self.assertIn("PR okunamadı", md)
        self.assertNotIn("SECRET", md)
        self.assertEqual(rr.pr_summary(1, None, lambda r, n: ["not", "a", "dict"]), {"no": 1, "hata": "PR okunamadı"})

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
        d = self.data([a, b])
        self.assertEqual(d["ozet"]["oturum_sayisi"], 2)
        self.assertEqual(d["ozet"]["insan_dokunusu"], 2)
        self.assertEqual(d["ozet"]["token_cikti"], 3)
        self.assertEqual(d["ozet"]["duvar_saati_dk"], 1.5)   # overlapping sessions are not added up
        self.assertEqual([o["ad"] for o in d["oturumlar"]], ["oturum-1", "oturum-2"])

    def test_a_resumed_copy_of_history_is_not_counted_twice(self):
        history = [human(at(0), uuid="h1"), assistant(at(1), "m1", usage(o=100)), human(at(2), uuid="h2"),
                   assistant(at(3), "m2", usage(o=50), blocks=[tool_use("t1", "Bash", command="ls")]), result(at(3, 30), "t1", uuid="r1")]
        a = self.write("a.jsonl", history)
        b = self.write("b.jsonl", history + [human(at(10), uuid="h3"), assistant(at(11), "m3", usage(o=7))])
        o = self.data([a, b])["ozet"]
        self.assertEqual(o["token_cikti"], 157)
        self.assertEqual(o["insan_dokunusu"], 3)
        self.assertEqual(o["arac_cagrilari_ana"], {"Bash": 1})
        self.assertEqual(self.data([a])["ozet"]["token_cikti"], 150)

    def test_summary_waits_table_and_turns_come_from_one_merged_timeline(self):
        a = self.write("a.jsonl", [human(at(0)), assistant(at(1), "a1", usage(o=1)), assistant(at(31), "a2", usage(o=1))])
        # a second session is busy during the first one's 30 minute silence: no waiting at all in the merged timeline
        b = self.write("b.jsonl", [human(at(2))] + [assistant(at(2 + 3 * k), "b%d" % k, usage(o=1)) for k in range(1, 10)])
        d = self.data([a, b])
        self.assertEqual(d["ozet"]["bekleme_dk"], 0.0)
        self.assertEqual(d["beklemeler"], [])
        self.assertEqual(sum(t["bekleme_dk"] for t in d["turlar"]), 0.0)
        self.assertEqual(round(sum(t["is_dk"] for t in d["turlar"]), 1), d["ozet"]["is_dk"])
        # and when nobody is busy, all three agree on the same 30 minutes
        c = self.write("c.jsonl", [human(at(0)), assistant(at(1), "c1", usage(o=1)), assistant(at(31), "c2", usage(o=1))])
        d = self.data([c])
        self.assertEqual(d["ozet"]["bekleme_dk"], 30.0)
        self.assertEqual([w["sure_dk"] for w in d["beklemeler"]], [30.0])
        self.assertEqual(sum(t["bekleme_dk"] for t in d["turlar"]), 30.0)

    def test_open_calls_are_tracked_across_one_merged_timeline(self):
        a = self.write("a.jsonl", [human(at(0)), assistant(at(1), "a1", usage(o=1), blocks=[tool_use("t", "Bash", command="make x")]),
                                   result(at(50), "t")])
        b = self.write("b.jsonl", [human(at(2)), assistant(at(3), "b1", usage(o=1))])
        waits = self.data([a, b])["beklemeler"]
        self.assertEqual([(w["tur"], w["arac"]) for w in waits], [(rr.WAIT_TOOL, "Bash: make")])


# ---------------------------------------------------------------------------------------------------- warnings
def chat_of(n, start_min=0):
    """One human message, then n assistant messages one second apart."""
    return [human(at(start_min))] + [assistant(at(start_min, 1 + i), "m%d-%d" % (start_min, i), usage(o=1)) for i in range(n)]


def kods(d):
    return [w["kod"] for w in d["uyarilar"]]


def texts(d):
    return [w["metin"] for w in d["uyarilar"]]


class Warnings(Base):
    def wait_events(self, gap_s):
        return [human(at(0)), assistant(at(1), "m1", usage(o=1)), assistant(at(1, gap_s), "m2", usage(o=1))]

    # --- U1
    def test_u1_long_wait_fires_at_the_threshold_not_below_it(self):
        below = self.data(self.write("a.jsonl", self.wait_events(60 * 60 - 1)))
        at_limit = self.data(self.write("b.jsonl", self.wait_events(60 * 60)))
        self.assertEqual(below["uyarilar"], [])
        self.assertEqual(kods(at_limit), ["U1"])
        self.assertEqual(texts(at_limit), ["Uzun bekleme: diğer · 60 dk"])
        self.assertEqual((at_limit["uyarilar"][0]["deger"], at_limit["uyarilar"][0]["esik"]), (60.0, 60))

    def test_u1_names_the_tool_longest_first_at_most_three_lines_and_counts_all(self):
        events = [human(at(0))]
        t = 1
        for i, gap in enumerate((61, 90, 75, 120, 66)):
            events += [assistant(at(t), "w%d" % i, usage(o=1), blocks=[tool_use("t%d" % i, "Bash", command="git push origin SECRETBRANCH")]),
                       result(at(t + gap), "t%d" % i)]
            t += gap + 1
        d = self.data(self.write("s.jsonl", events))
        self.assertEqual(texts(d), [
            "Uzun bekleme: izin ya da uzun komut · Bash: git push · 2 sa 0 dk",
            "Uzun bekleme: izin ya da uzun komut · Bash: git push · 90 dk",
            "Uzun bekleme: izin ya da uzun komut · Bash: git push · 75 dk",
            "ve 2 bekleme daha",
        ])
        self.assertEqual(d["ozet"]["uyari_sayisi"], 5)
        self.assertNotIn("SECRETBRANCH", json.dumps(d, ensure_ascii=False))

    def test_u1_threshold_flag_and_off_switch(self):
        path = self.write("s.jsonl", self.wait_events(30 * 60))
        self.assertEqual(self.data(path)["uyarilar"], [])
        self.assertEqual(kods(self.data(path, limits={"bekleme_dk": 30})), ["U1"])
        self.assertEqual(self.data(self.write("b.jsonl", self.wait_events(500 * 60)), limits={"bekleme_dk": 0})["uyarilar"], [])

    # --- U2
    def test_u2_touches_above_the_target_only(self):
        three = self.write("a.jsonl", [human(at(i)) for i in range(3)])
        four = self.write("b.jsonl", [human(at(i)) for i in range(4)])
        self.assertEqual(self.data(three)["uyarilar"], [])
        d = self.data(four)
        self.assertEqual(texts(d), ["İnsan dokunuşu hedefin üstünde: 4 (hedef: en çok 3)"])
        self.assertEqual((d["uyarilar"][0]["deger"], d["uyarilar"][0]["esik"]), (4, 3))
        self.assertEqual(self.data(four, limits={"dokunus": 0})["uyarilar"], [])
        self.assertEqual(self.data(four, limits={"dokunus": 4})["uyarilar"], [])

    def test_u2_counts_the_interval_only(self):
        path = self.write("s.jsonl", [human(at(i)) for i in range(6)])
        self.assertEqual(kods(self.data(path)), ["U2"])
        self.assertEqual(self.data(path, since=rr.parse_ts(at(3)))["uyarilar"], [])   # 3 messages left

    # --- U3
    def test_u3_output_tokens_main_plus_subagents(self):
        path = self.write("s.jsonl", [human(at(0)), assistant(at(1), "m1", usage(o=100000))])
        self.write("s/subagents/agent-1.jsonl", [sub_assistant(at(1, 10), "s1", u=usage(o=50000))])
        self.assertEqual(self.data(path)["uyarilar"], [])                      # 150000 = at the threshold
        self.write("s/subagents/agent-1.jsonl", [sub_assistant(at(1, 10), "s1", u=usage(o=50001))])
        d = self.data(path)
        self.assertEqual(texts(d), ["Çıktı token'ı yüksek: 150.001 (eşik 150.000)"])
        self.assertEqual(self.data(path, limits={"cikti_token": 0})["uyarilar"], [])
        self.assertEqual(self.data(path, limits={"cikti_token": 200000})["uyarilar"], [])

    def test_u3_four_kind_total_is_a_separate_rule(self):
        path = self.write("s.jsonl", [human(at(0)), assistant(at(1), "m1", usage(i=1, cw=2, cr=17999997, o=0))])
        self.assertEqual(self.data(path)["uyarilar"], [])                      # exactly 18.000.000
        self.assertEqual(self.data(path, limits={"toplam_token": 18000000})["uyarilar"], [])
        d = self.data(path, limits={"toplam_token": 17999999})
        self.assertEqual(texts(d), ["Toplam token yüksek: 18.000.000 (eşik 17.999.999)"])
        self.assertEqual(self.data(path, limits={"toplam_token": 0})["uyarilar"], [])
        big = self.write("b.jsonl", [human(at(0)), assistant(at(1), "m1", usage(cr=20000001))])
        self.assertEqual(texts(self.data(big)), ["Toplam token yüksek: 20.000.001 (eşik 20.000.000)"])
        self.assertEqual(self.data(big, limits={"toplam_token": 0})["uyarilar"], [])

    # --- U4
    def test_u4_long_chat_counts_messages_above_the_limit_only(self):
        self.assertEqual(self.data(self.write("a.jsonl", chat_of(400)))["uyarilar"], [])
        d = self.data(self.write("b.jsonl", chat_of(401)))
        self.assertEqual(texts(d), ["Sohbet uzadı: oturum-1 401 ileti; yeni sohbete devir düşünülmeli"])
        self.assertEqual((d["uyarilar"][0]["deger"], d["uyarilar"][0]["esik"]), (401, 400))
        self.assertEqual(self.data(self.write("c.jsonl", chat_of(401)), limits={"ileti": 0})["uyarilar"], [])
        self.assertEqual(kods(self.data(self.write("d.jsonl", chat_of(11)), limits={"ileti": 10})), ["U4"])

    def test_u4_one_compaction_summary_is_enough_and_is_named(self):
        events = chat_of(5) + [human(at(1), "SECRETSUMMARY", isCompactSummary=True)]
        d = self.data(self.write("s.jsonl", events))
        self.assertEqual(texts(d), ["Sohbet uzadı: oturum-1 5 ileti, 1 kez özetlendi; yeni sohbete devir düşünülmeli"])
        self.assertNotIn("SECRETSUMMARY", json.dumps(d, ensure_ascii=False))
        events += [human(at(2), "x", isCompactSummary=True)]
        self.assertIn(", 2 kez özetlendi", texts(self.data(self.write("t.jsonl", events)))[0])
        self.assertEqual(self.data(self.write("u.jsonl", events), limits={"ileti": 0})["uyarilar"], [])

    def test_u4_uses_the_whole_transcript_even_with_since(self):
        events = chat_of(450) + [human(at(60), "later"), assistant(at(61), "late", usage(o=1))]
        path = self.write("s.jsonl", events)
        d = self.data(path, since=rr.parse_ts(at(59)))
        self.assertEqual(d["turlar"][0]["asistan_iletisi"], 1)                  # the interval holds one message ...
        self.assertEqual(texts(d), ["Sohbet uzadı: oturum-1 451 ileti; yeni sohbete devir düşünülmeli"])  # ... the chat many
        compact = self.write("c.jsonl", chat_of(3) + [human(at(1), "s", isCompactSummary=True), human(at(60)),
                                                       assistant(at(61), "z", usage(o=1))])
        self.assertEqual(kods(self.data(compact, since=rr.parse_ts(at(59)))), ["U4"])

    def test_u4_is_per_session_and_dedupes_message_ids(self):
        a = self.write("a.jsonl", chat_of(3))
        b = self.write("b.jsonl", chat_of(401, start_min=30))
        d = self.data([a, b])
        self.assertEqual(texts(d), ["Sohbet uzadı: oturum-2 401 ileti; yeni sohbete devir düşünülmeli"])
        streamed = [human(at(0))] + [assistant(at(0, 1 + i), "same", usage(o=1)) for i in range(500)]
        self.assertEqual(self.data(self.write("c.jsonl", streamed))["uyarilar"], [])   # one message streamed 500 times

    # --- U5
    def launches(self, kinds):
        events = [human(at(0))]
        for i, kind in enumerate(kinds):
            events.append(assistant(at(0, 5 * (i + 1)), "a%d" % i, usage(o=1), blocks=[
                tool_use("ag%d" % i, "Agent", subagent_type=kind, description="SECRETDESC", prompt="SECRETPROMPT")]))
        return events

    def test_u5_same_subagent_type_more_than_twice(self):
        self.assertEqual(self.data(self.write("a.jsonl", self.launches(["Explore", "Explore", "Plan"])))["uyarilar"], [])
        d = self.data(self.write("b.jsonl", self.launches(["Explore"] * 3 + ["Plan"])))
        self.assertEqual(texts(d), ["Aynı alt ajan 3 kez başlatıldı: Explore"])
        self.assertEqual((d["uyarilar"][0]["deger"], d["uyarilar"][0]["esik"]), (3, 2))
        three = self.write("c.jsonl", self.launches(["Explore"] * 3))
        self.assertEqual(self.data(three, limits={"ajan_tekrar": 0})["uyarilar"], [])
        self.assertEqual(self.data(three, limits={"ajan_tekrar": 3})["uyarilar"], [])

    def test_u5_unprintable_type_is_called_alt_ajan_and_nothing_leaks(self):
        d = self.data(self.write("s.jsonl", self.launches(["SECRET type | x@y.z"] * 3)))
        self.assertEqual(texts(d), ["Aynı alt ajan 3 kez başlatıldı: alt ajan"])
        blob = json.dumps(d, ensure_ascii=False)
        for secret in ("SECRET", "x@y.z"):
            self.assertNotIn(secret, blob)

    def test_u5_tool_results_flagged_as_errors(self):
        def events(n):
            ev = [human(at(0))]
            for i in range(n):
                ev += [assistant(at(0, 2 * i + 1), "e%d" % i, usage(o=1), blocks=[tool_use("e%d" % i, "Bash", command="ls")]),
                       {"type": "user", "timestamp": at(0, 2 * i + 2), "message": {"role": "user", "content": [
                           {"type": "tool_result", "tool_use_id": "e%d" % i, "is_error": True, "content": "SECRETERR"}]}}]
            return ev
        self.assertEqual(self.data(self.write("a.jsonl", events(10)))["uyarilar"], [])
        d = self.data(self.write("b.jsonl", events(11)))
        self.assertEqual(texts(d), ["Hatalı araç çağrısı çok: 11"])
        self.assertNotIn("SECRETERR", json.dumps(d, ensure_ascii=False))
        self.assertEqual(self.data(self.write("c.jsonl", events(11)), limits={"hata": 0})["uyarilar"], [])
        self.assertEqual(self.data(self.write("d.jsonl", events(11)), limits={"hata": 11})["uyarilar"], [])
        # only a real `true` counts; ordinary results do not
        ok = [human(at(0))] + [result(at(0, i + 1), "ok%d" % i) for i in range(20)]
        ok += [{"type": "user", "timestamp": at(1), "message": {"content": [
            {"type": "tool_result", "tool_use_id": "q", "is_error": "true"}] * 20}}]
        self.assertEqual(self.data(self.write("e.jsonl", ok))["uyarilar"], [])

    # --- U6
    def open_pr(self, created="2026-11-16T10:00:00Z", failed=False):
        raw = fake_pr()
        raw["mergedAt"] = None
        raw["closedAt"] = None
        raw["createdAt"] = created
        if not failed:
            raw["statusCheckRollup"][2]["conclusion"] = "SUCCESS"
        return raw

    def test_u6_failed_checks_and_open_pr_age_with_fake_gh(self):
        path = self.write("s.jsonl", [human(at(0))])
        now = T0 + timedelta(days=6, minutes=1)
        d = self.data(path, prs=[7], repo="o/r", gh=lambda r, n: fake_pr(), now=now)        # merged, one red check
        self.assertEqual(texts(d), ["Otomatik kontrollerde kırmızı: 1 (PR #7)"])
        d = self.data(path, prs=[7], gh=lambda r, n: self.open_pr(failed=True), now=now)
        self.assertEqual(kods(d), ["U6", "U6"])
        self.assertEqual(texts(d)[1], "Birleştirme isteği 6 gündür açık (PR #7)")
        self.assertEqual((d["uyarilar"][1]["deger"], d["uyarilar"][1]["esik"]), (6, 5))
        self.assertEqual(d["ozet"]["uyari_sayisi"], 2)

    def test_u6_age_is_more_than_the_limit_and_off_with_zero(self):
        path = self.write("s.jsonl", [human(at(0))])

        def gh(repo, number):
            return self.open_pr()
        self.assertEqual(self.data(path, prs=[1], gh=gh, now=T0 + timedelta(days=5, hours=23))["uyarilar"], [])
        self.assertEqual(kods(self.data(path, prs=[1], gh=gh, now=T0 + timedelta(days=6))), ["U6"])
        self.assertEqual(self.data(path, prs=[1], gh=gh, now=T0 + timedelta(days=60), limits={"pr_gun": 0})["uyarilar"], [])
        self.assertEqual(self.data(path, prs=[1], gh=gh, now=T0 + timedelta(days=60), limits={"pr_gun": 60})["uyarilar"], [])
        # a merged PR is not "still open"
        merged = fake_pr()
        merged["statusCheckRollup"][2]["conclusion"] = "SUCCESS"
        self.assertEqual(self.data(path, prs=[1], gh=lambda r, n: merged, now=T0 + timedelta(days=60))["uyarilar"], [])

    def test_u6_needs_pr_and_survives_a_broken_gh(self):
        path = self.write("s.jsonl", [human(at(0))])
        self.assertEqual(self.data(path, now=T0 + timedelta(days=60))["uyarilar"], [])

        def bad(repo, number):
            raise RuntimeError("SECRET")
        code, md = self.run_main(["--transcript", path, "--pr", "3"], gh=bad)
        self.assertEqual(code, 0)
        self.assertIn("Uyarı yok.", md)

    def test_u6_through_main_prints_the_lines_and_exits_zero(self):
        path = self.write("s.jsonl", [human(at(0))])
        buf = io.StringIO()
        code = rr.main(["--transcript", path, "--pr", "7", "--uyari-pr-gun", "1"], gh=lambda r, n: self.open_pr(failed=True),
                       stdout=buf, now=T0 + timedelta(days=2))
        self.assertEqual(code, 0)
        self.assertIn("- Otomatik kontrollerde kırmızı: 1 (PR #7)\n- Birleştirme isteği 2 gündür açık (PR #7)\n", buf.getvalue())

    # --- output shape
    def test_no_rule_fires_says_so_in_one_line_and_json_list_is_empty(self):
        path = self.write("s.jsonl", [human(at(0)), assistant(at(1), "m1", usage(o=1))])
        code, md = self.run_main(["--transcript", path])
        self.assertEqual(code, 0)
        self.assertIn("## Uyarılar\n\nUyarı yok.\n", md)
        self.assertIn("- Uyarı sayısı: 0\n", md)
        code, text = self.run_main(["--transcript", path, "--format", "json"])
        obj = json.loads(text)
        self.assertEqual(obj["uyarilar"], [])
        self.assertEqual(obj["ozet"]["uyari_sayisi"], 0)

    def test_section_sits_right_after_the_summary_and_json_items_have_four_keys(self):
        events = [human(at(i)) for i in range(5)] + self.wait_events(90 * 60)[1:]
        path = self.write("s.jsonl", events)
        code, md = self.run_main(["--transcript", path])
        self.assertEqual(code, 0)
        self.assertLess(md.index("## Özet"), md.index("## Uyarılar"))
        self.assertLess(md.index("## Uyarılar"), md.index("## Turlar"))
        self.assertIn("- Uyarı sayısı: 2\n", md)
        self.assertIn("- İnsan dokunuşu hedefin üstünde: 5 (hedef: en çok 3)\n", md)
        _, text = self.run_main(["--transcript", path, "--format", "json"])
        items = json.loads(text)["uyarilar"]
        self.assertEqual([w["kod"] for w in items], ["U1", "U2"])
        for w in items:
            self.assertEqual(set(w), {"kod", "metin", "deger", "esik"})
            self.assertIsInstance(w["metin"], str)
        self.assertEqual(json.loads(text)["ozet"]["uyari_sayisi"], 2)

    def test_flags_switch_every_rule_off_and_reject_negative_values(self):
        path = self.write("s.jsonl", chat_of(5) + [human(at(1), "s", isCompactSummary=True)] + [human(at(i + 2)) for i in range(5)]
                          + [assistant(at(200), "late", usage(o=1))])
        _, md = self.run_main(["--transcript", path])
        self.assertNotIn("Uyarı yok.", md)
        off = ["--uyari-bekleme-dk", "0", "--uyari-dokunus", "0", "--uyari-cikti-token", "0", "--uyari-toplam-token", "0",
               "--uyari-ileti", "0", "--uyari-ajan-tekrar", "0", "--uyari-hata", "0", "--uyari-pr-gun", "0"]
        _, md = self.run_main(["--transcript", path] + off)
        self.assertIn("Uyarı yok.", md)
        err = io.StringIO()
        with redirect_stderr(err):
            self.assertEqual(rr.main(["--transcript", path, "--uyari-dokunus", "-1"], stdout=io.StringIO()), 2)

    def test_nothing_from_text_arguments_or_names_leaks_through_a_warning(self):
        events = [human(at(0), "SECRETHUMAN a@b.cz")]
        for i in range(12):
            events += [assistant(at(1, i * 20), "h%d" % i, usage(o=200000),
                                 blocks=[tool_use("h%d" % i, "SECRET_tool | x", command="rm -rf /SECRETPATH", file_path="/SECRETPATH"),
                                         tool_use("g%d" % i, "Agent", subagent_type="SECRET sub@x.y", description="SECRETDESC")]),
                       {"type": "user", "timestamp": at(1, i * 20 + 10), "message": {"role": "user", "content": [
                           {"type": "tool_result", "tool_use_id": "h%d" % i, "is_error": True, "content": "SECRETRES"}]}}]
        events += [human(at(2), "SECRETSUM", isCompactSummary=True), human(at(200), "SECRETLAST")]
        path = self.write("secret-name.jsonl", events)

        def gh(repo, number):
            raw = self.open_pr(failed=True)
            raw["reviews"] = [{"state": "APPROVED", "submittedAt": "2026-11-16T10:30:00Z", "author": {"login": "secret-login"}}]
            return raw
        _, md = self.run_main(["--transcript", path, "--pr", "5", "--idle-minutes", "1"], gh=gh)
        _, js = self.run_main(["--transcript", path, "--pr", "5", "--idle-minutes", "1", "--format", "json"], gh=gh)
        self.assertGreaterEqual(len(json.loads(js)["uyarilar"]), 5)
        for secret in ("SECRET", "secret", "a@b.cz", "sub@x.y", "rm -rf"):
            self.assertNotIn(secret, md, secret)
            self.assertNotIn(secret, js, secret)
        section = md.split("## Uyarılar")[1].split("## Turlar")[0]
        for line in section.strip().splitlines():
            self.assertTrue(line.startswith("- "), line)
            self.assertNotIn("|", line)


if __name__ == "__main__":
    unittest.main()
