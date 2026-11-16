"""EES planlama zinciri — scripts/pack_contract_check.py, kısa paket standardının YALNIZ UYARAN kontrolü.

Run by scripts/run_phase1_gates.sh:

    python3 -m unittest discover -s scripts/tests -p 'test_*.py'

Her kural U0..U8 için bir bozuk örnek vardır (uyarı çıkar) ve düzgün paket hiç uyarı vermez. Sabotaj kanıtı bu dosyadadır:
SabotageProof her kuralı `disabled=` ile kapatır ve kuralın kendi bozuk örneğinde uyarının KAYBOLDUĞUNU gösterir; yani hiçbir
kural testi kendiliğinden yeşil değildir. Muafiyet ve çıkış kodu da üretim koduyla ölçülür: eski paket (alanı olmayan) hiç
uyarmaz; kontrol bozuk argümanda, okunamayan dosyada, olmayan tabanda ve içeride patlayan hatada da 0 döner.
Okuyucu uyumu: kabul ölçütleri scripts/acceptance_record.py parse_pack ile, dilim tablosu scripts/pack_approval.py slices ile
okunur — bu testler o iki okuyucuyu gerçekten çağırır, kopyasını değil.
"""

from __future__ import annotations

import contextlib
import importlib.util
import io
import os
import re
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

REPO = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("pack_contract_check", REPO / "scripts" / "pack_contract_check.py")
pcc = importlib.util.module_from_spec(SPEC)
sys.modules["pack_contract_check"] = pcc
SPEC.loader.exec_module(pcc)

PACK_PATH = "execution/domains/human-capital-management/module-packs/MOD-0280-FU02-izin.md"
PROCESS_PATH = "execution/domains/human-capital-management/is-surecleri/SUR-HCM-01-izin.md"


def pack(front_extra: str = "", conditional: str = "[]", process: str = "yok — teknik: örnek", plan: str = "plan satırı yok — örnek",
         sections: dict | None = None, extra_sections: str = "", drop_front: tuple = ()) -> str:
    front = {
        "id": "MOD-0280-FU02", "name": "Leave", "domain": "human-capital-management", "service": "Diten.Platform",
        "shell": "tenant", "golden_reference": "slim", "status": "ready-for-dev", "pack_standard": "kisa-v1",
        "plan_row": plan, "business_process": process, "conditional": conditional,
    }
    for k in drop_front:
        front.pop(k, None)
    s = {
        "1": "## 1. Ne ve neden\nÖrnek amaç. Roller: çalışan.\n",
        "2": "## 2. Kapsam\n- Kapsam: izin.\n- Kapsam dışı: hastalık → takip paketi.\n",
        "3": "## 3. Bağlar\n| Kaynak | Atıf | Giren | Girmeyen |\n|---|---|---|---|\n| Süreç | yok | — | — |\n",
        "4": ("## 4. Sözleşme\n| Nesne | Tür | Açıklama | Dilim |\n|---|---|---|---|\n| `Leave` | varlık | izin | DL-1 |\n"),
        "5": ("## 5. Acceptance Criteria\n\n- [ ] AC-1 (zorunlu) — Talep açılır · dilim: DL-1 · test: entegrasyon\n"
              "- [ ] AC-2 (zorunlu) — Başka kiracı 404 · dilim: DL-1 · test: entegrasyon\n"),
        "6": ("## 6. Teslim dilimleri\n\n| dilim | ad | kapsadığı bölümler | süreç adımı | plan | ölçütler | bağımlı | kanıt |\n"
              "|---|---|---|---|---|---|---|---|\n| DL-1 | çekirdek | Sözleşme, Acceptance Criteria | teknik | plan satırı yok | 1–2 | "
              "bağımsız | otomatik test |\n"),
        "7": "## 7. Açık kararlar\n| # | Karar | Kim | Hangi dilimi bekletir |\n|---|---|---|---|\n| 1 | örnek | rol | DL-1 |\n",
    }
    s.update(sections or {})
    head = "---\n" + "".join(f"{k}: {v}\n" for k, v in front.items()) + front_extra + "---\n"
    return head + "\n".join(s[k] for k in sorted(s)) + extra_sections


def codes(warns) -> list[str]:
    return [w.code for w in warns]


def process_file(**kw) -> str:
    steps = kw.get("steps", 3)
    exc = kw.get("exceptions", 2)
    e2e = kw.get("e2e", 1)
    out = ["---", "id: SUR-HCM-01", "---", "", "# SUR-HCM-01 — örnek", ""]
    if not kw.get("no_roles"):
        out += ["## Roller", "| Rol | Ne yapar | Hangi izinle |", "|---|---|---|", "| Çalışan | ister | kendi kaydı |", ""]
    out += ["## Adımlar", "| # | Rol | Ne yapar | Girdi | Çıktı | Modül |", "|---|---|---|---|---|---|"]
    out += [f"| {i} | Çalışan | yapar | x | y | MOD-0280 |" for i in range(1, steps + 1)] + [""]
    out += ["## İş kuralları", "- K1. örnek", ""]
    out += ["## İstisnalar", "| # | Hangi adımda | Durum | Ne olur |", "|---|---|---|---|"]
    out += [f"| X{i} | 1 | durum | olur |" for i in range(1, exc + 1)] + [""]
    out += ["## Uçtan uca senaryolar", "| # | Senaryo | Başlangıç | Son |", "|---|---|---|---|"]
    out += [f"| E2E-{i} | s | b | s |" for i in range(1, e2e + 1)]
    return "\n".join(out) + "\n"


class Case(unittest.TestCase):
    root: str

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = self._tmp.name
        self.addCleanup(self._tmp.cleanup)

    def put_process(self, sid="SUR-HCM-01"):
        d = Path(self.root, "execution/domains/human-capital-management/is-surecleri")
        d.mkdir(parents=True, exist_ok=True)
        (d / f"{sid}-izin.md").write_text(process_file(), encoding="utf-8")

    def check(self, text, **kw):
        return pcc.check_pack(PACK_PATH, text, self.root, **kw)


class GoodAndExempt(Case):
    def test_good_pack_gives_no_warning(self):
        self.assertEqual(self.check(pack()), [])

    def test_good_pack_with_process_and_conditional_gives_no_warning(self):
        self.put_process()
        text = pack(process="SUR-HCM-01 adım 1-3", conditional="[zaman]",
                    extra_sections="\n## K-zaman. Zaman\n| Soru | Cevap | AC | Dilim |\n|---|---|---|---|\n| Saat dilimi | kiracı | 1 | DL-1 |\n")
        self.assertEqual(self.check(text), [])

    def test_old_pack_is_never_read(self):
        old = pack(drop_front=("pack_standard", "plan_row", "business_process", "conditional")).replace("## 6. Teslim dilimleri", "## Revizyon")
        old += "\nTBD\n- [ ] AC-7b (zorunlu) — bozuk\n"
        self.assertEqual(self.check(old), [])
        self.assertEqual(self.check("# başlıksız eski paket\nTBD\n"), [])
        self.assertEqual(self.check("---\nid: MOD-1\nkapanmayan künye\n"), [])        # eski paket, künyesi kapanmıyor: yine muaf

    def test_pack_standard_of_another_value_is_exempt(self):
        self.assertEqual(self.check(pack().replace("pack_standard: kisa-v1", "pack_standard: baska")), [])

    def test_unclosed_kunye_of_new_standard_warns(self):
        self.assertIn("U5", codes(self.check("---\nid: MOD-0280-FU02\npack_standard: kisa-v1\n## 1. Ne ve neden\n")))


class RuleCases(Case):
    """Her kural için bir bozuk örnek: (kod, metin, kök'te süreç dosyası var mı)."""

    def bad_cases(self):
        many = "".join(f"| DL-{i} | s | Sözleşme, Acceptance Criteria | teknik | plan satırı yok | 1 | bağımsız | otomatik test |\n" for i in range(2, 10))
        big_rows = "".join(f"| `Alan{i}` | varlık | {'x' * 200} | DL-1 |\n" for i in range(60))
        return {
            "U1": pack(drop_front=("business_process",)),
            "U1b": pack(process="yok — teknik:"),
            "U1c": pack(process="SUR-HCM-09 adım 1-3"),                         # süreç dosyası yok
            "U1d": pack(process="her şey serbest"),
            "U2": pack(drop_front=("plan_row",)),
            "U2b": pack(plan="v32 · BLK-07-MVP2 · ÖNEMLİ"),
            "U3": pack(sections={"4": "## 4. Sözleşme\n| Nesne | Tür | Açıklama | Dilim |\n|---|---|---|---|\n" + big_rows}),
            "U3b": pack(sections={"2": "## 2. Kapsam\n" + ("- uzun kapsam satırı " + "x" * 100 + "\n") * 90}),
            "U4": pack(sections={"6": pack().split("## 6. Teslim dilimleri")[1].split("## 7.")[0].join(["## 6. Teslim dilimleri", ""]).rstrip("\n") + "\n" + many + "\n"}),
            "U5": pack().replace("## 3. Bağlar", "## 3. Bağlantılar"),
            "U5b": pack(conditional="[zaman]"),                                 # listede var, başlık yok
            "U5c": pack(extra_sections="\n## K-zaman. Zaman\n| Soru | Cevap | AC | Dilim |\n|---|---|---|---|\n"),   # başlık var, listede yok
            "U5d": pack().replace("## 7. Açık kararlar", "## 7. Açık kararlar\n").replace("## 2. Kapsam", "## 2. Kapsam", 1) + "\n## Ekler\nx\n",
            "U6": pack().replace(" · dilim: DL-1 · test: entegrasyon\n- [ ] AC-2", "\n- [ ] AC-2", 1),
            "U6b": pack().replace("· dilim: DL-1 · test: entegrasyon\n", "· dilim: DL-7 · test: entegrasyon\n", 1),
            "U6c": pack().replace("test: entegrasyon\n- [ ] AC-2", "test: elle bakılır\n- [ ] AC-2", 1),
            "U6d": pack().replace("- [ ] AC-1 (zorunlu)", "- [ ] AC-1b (zorunlu)", 1),                  # okuyucu okuyamaz
            "U6e": pack().replace("| DL-1 | çekirdek | Sözleşme, Acceptance Criteria | teknik", "| DL-1 | çekirdek | Sözleşme, Acceptance Criteria | yok", 1),
            "U6f": pack(sections={"2": "## 2. Kapsam\n- Kapsam: TBD\n"}).replace("| 1 | örnek | rol | DL-1 |\n", ""),
            "U7": pack(extra_sections="\n## Revizyon geçmişi\nx\n"),
        }

    def expected(self, key):
        return key[:2]

    def test_each_bad_example_warns_with_its_own_code(self):
        for key, text in self.bad_cases().items():
            with self.subTest(key=key):
                self.assertIn(self.expected(key), codes(self.check(text)), key)

    def test_each_bad_example_gives_nothing_but_warnings_and_never_raises(self):
        for key, text in self.bad_cases().items():
            with self.subTest(key=key):
                for w in self.check(text):
                    self.assertTrue(w.message)

    def test_unreadable_slice_table_names_the_faulty_cell(self):
        text = pack().replace("| DL-1 | çekirdek | Sözleşme, Acceptance Criteria |", "| DL-1 | çekirdek | Sözleşme, Zaman |")
        msg = " ".join(w.message for w in self.check(text) if w.code == "U6")
        self.assertIn("kusurlu: 'Zaman'", msg)

    def test_tbd_with_a_row_in_section_7_is_clean(self):
        self.assertNotIn("U6", codes(self.check(pack(sections={"2": "## 2. Kapsam\n- Kapsam: TBD (bölüm 7 karar 1)\n"}))))

    def test_process_file_present_in_the_tree_clears_u1(self):
        self.assertIn("U1", codes(self.check(pack(process="SUR-HCM-01 adım 1-3"))))
        self.put_process()
        self.assertNotIn("U1", codes(self.check(pack(process="SUR-HCM-01 adım 1-3"))))

    def test_follow_up_and_technical_forms_are_valid(self):
        for v in ("yok — takip paketi: üst paketin süreci", "yok — teknik: kayıt kodu servisi"):
            self.assertEqual([w for w in self.check(pack(process=v)) if w.code == "U1"], [], v)


class SabotageProof(Case):
    """Her kuralı kapat → kendi bozuk örneğindeki uyarı kaybolur (kural testi kendiliğinden yeşil değil)."""

    def test_disabling_a_rule_removes_exactly_its_warning(self):
        bad = RuleCases("test_each_bad_example_warns_with_its_own_code").bad_cases()
        for key, text in bad.items():
            code = key[:2]
            with self.subTest(key=key):
                self.assertIn(code, codes(pcc.check_pack(PACK_PATH, text, self.root)))
                self.assertNotIn(code, codes(pcc.check_pack(PACK_PATH, text, self.root, disabled=frozenset({code}))))

    def test_u0_and_process_rules_sabotage(self):
        text = pack(drop_front=("pack_standard",))
        self.assertEqual(codes(pcc.check_pack(PACK_PATH, text, self.root, added=True)), ["U0"])
        self.assertEqual(pcc.check_pack(PACK_PATH, text, self.root, added=True, disabled=frozenset({"U0"})), [])
        for code, kw in (("U3", {"steps": 3}), ("U8", {"no_roles": True})):
            bad = process_file(**kw) if code == "U8" else process_file() + "\n".join(["x" * 100] * 100)
            self.assertIn(code, codes(pcc.check_process(PROCESS_PATH, bad)))
            self.assertNotIn(code, codes(pcc.check_process(PROCESS_PATH, bad, frozenset({code}))))

    def test_readers_are_the_production_ones(self):
        """U6 kendi okuyucusunu yazmaz: ana daldaki iki okuyucu çağrılır. Okuyucu bozulursa uyarı da bozulur."""
        text = pack()
        with mock.patch.object(pcc.reader("pack_approval"), "slices", return_value=None):
            self.assertIn("U6", codes(self.check(text)))
        with mock.patch.object(pcc.reader("acceptance_record"), "parse_pack",
                               return_value=({}, ["paket: AC biçimine uymayan madde var: 'x'"], False)):
            self.assertIn("U6", codes(self.check(text)))


class ProcessFile(Case):
    def test_good_process_is_clean(self):
        self.assertEqual(pcc.check_process(PROCESS_PATH, process_file()), [])

    def test_missing_section_and_counts(self):
        self.assertIn("U8", codes(pcc.check_process(PROCESS_PATH, process_file(no_roles=True))))
        self.assertIn("U8", codes(pcc.check_process(PROCESS_PATH, process_file(steps=13))))
        self.assertIn("U8", codes(pcc.check_process(PROCESS_PATH, process_file(exceptions=13))))
        self.assertIn("U8", codes(pcc.check_process(PROCESS_PATH, process_file(e2e=5))))

    def test_oversize_process_warns_u3(self):
        self.assertIn("U3", codes(pcc.check_process(PROCESS_PATH, process_file() + "\n".join(["satır"] * 90))))


class ExitCodeIsAlwaysZero(Case):
    def run_main(self, argv, env=None):
        buf = io.StringIO()
        with contextlib.redirect_stdout(buf), mock.patch.dict(os.environ, env or {}, clear=False):
            code = pcc.main(argv)
        return code, buf.getvalue()

    def write_pack(self, text):
        p = Path(self.root, PACK_PATH)
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(text, encoding="utf-8")

    def test_bad_pack_still_exits_zero_and_prints_the_warning(self):
        self.write_pack(pack(drop_front=("plan_row",)))
        code, out = self.run_main(["--root", self.root, "--all"])
        self.assertEqual(code, 0)
        self.assertIn("U2", out)
        self.assertIn("1 uyarı", out)

    def test_github_actions_format(self):
        self.write_pack(pack(drop_front=("plan_row",)))
        code, out = self.run_main(["--root", self.root, "--all"], {"GITHUB_ACTIONS": "true"})
        self.assertEqual(code, 0)
        self.assertIn(f"::warning file={PACK_PATH},line=1::U2", out)

    def test_bad_arguments_exit_zero(self):
        for argv in (["--bilinmeyen"], ["--base"], ["--root"], ["-h"]):
            with self.subTest(argv=argv):
                self.assertEqual(self.run_main(argv)[0], 0)

    def test_unreadable_files_exit_zero_with_a_warning(self):
        d = Path(self.root, PACK_PATH)
        d.mkdir(parents=True)                                   # dizin dosya gibi okunamaz
        code, out = self.run_main(["--root", self.root, "--all"])
        self.assertEqual(code, 0)
        self.assertIn("okunamadı", out)
        d.rmdir()
        d.write_bytes(b"\xff\xfe\x00bozuk")                      # geçersiz UTF-8
        code, out = self.run_main(["--root", self.root, "--all"])
        self.assertEqual(code, 0)
        self.assertIn("okunamadı", out)

    def test_internal_crash_exits_zero(self):
        with mock.patch.object(pcc, "run", side_effect=RuntimeError("patladı")):
            code, out = self.run_main([])
        self.assertEqual(code, 0)
        self.assertIn("tamamlanamadı", out)

    def test_missing_base_warns_and_exits_zero(self):
        subprocess.run(["git", "init", "-q", self.root], check=True)
        code, out = self.run_main(["--root", self.root, "--base", "yok-boyle-bir-ref"])
        self.assertEqual(code, 0)
        self.assertIn("taban yok", out)

    def test_script_run_as_a_program_exits_zero(self):
        r = subprocess.run([sys.executable, str(REPO / "scripts" / "pack_contract_check.py"), "--bilinmeyen"],
                           capture_output=True, text=True)
        self.assertEqual(r.returncode, 0)


class GitSelection(Case):
    """--base: yalnız bu değişiklikte eklenen ya da değişen dosyalar; yeni pakette alan yoksa U0, eski paket sessiz."""

    def sh(self, *a):
        subprocess.run(["git", "-C", self.root, "-c", "user.name=t", "-c", "user.email=t@t", *a], check=True, capture_output=True)

    def test_new_pack_without_field_gets_u0_and_touched_old_pack_stays_silent(self):
        self.sh("init", "-q", "-b", "main")
        old = Path(self.root, "execution/domains/human-capital-management/module-packs/MOD-0001-eski.md")
        old.parent.mkdir(parents=True)
        old.write_text("---\nid: MOD-0001\n---\n## Revizyon\nTBD\n", encoding="utf-8")
        self.sh("add", str(old.relative_to(self.root)))
        self.sh("commit", "-q", "-m", "taban")
        self.sh("checkout", "-q", "-b", "dal")
        old.write_text("---\nid: MOD-0001\n---\n## Revizyon\nTBD ve küçük düzeltme\n", encoding="utf-8")
        new = old.parent / "MOD-0002-yeni.md"
        new.write_text("---\nid: MOD-0002\n---\n## 1. Ne ve neden\n", encoding="utf-8")
        good = old.parent / "MOD-0003-iyi.md"
        good.write_text(pack().replace("MOD-0280-FU02", "MOD-0003"), encoding="utf-8")
        self.sh("add", "-A")
        self.sh("commit", "-q", "-m", "dal")
        warns, read = pcc.run(["--root", self.root, "--base", "main"])
        self.assertEqual(read, 3)
        self.assertEqual([(w.path.split("/")[-1], w.code) for w in warns], [("MOD-0002-yeni.md", "U0")])


class RepositoryAndTemplate(unittest.TestCase):
    def test_no_old_pack_on_this_tree_warns(self):
        """Ağaçtaki eski paketlerin hiçbiri alan taşımıyor → ilk koşu sıfır uyarı (muafiyet künyeden gelir)."""
        warns, read = pcc.run(["--all"])
        self.assertGreater(read, 100)
        exempt = []
        for w in warns:
            if PACK_RE_OLD(w.path) and "pack_standard: kisa-v1" not in (REPO / w.path).read_text(encoding="utf-8"):
                exempt.append(w)
        self.assertEqual(exempt, [])

    def test_template_headings_match_the_standard_and_template_reads_through_the_old_readers(self):
        text = (REPO / ".ees" / "sablonlar" / "paket.md").read_text(encoding="utf-8")
        heads = [l.rstrip() for l in text.split("\n") if l.startswith("## ")]
        self.assertEqual(heads, pcc.STANDARD_HEADINGS)
        for field in ("pack_standard", "plan_row", "business_process", "conditional"):
            self.assertRegex(text, rf"(?m)^{field}:")
        self.assertIsNotNone(pcc.reader("pack_approval").slices(text), "dilim tablosu şablonda okunamıyor")
        crit, bad, old = pcc.reader("acceptance_record").parse_pack(text.replace("MOD-<nnnn>[-FU<nn>]", "MOD-0001").replace("status: draft", "status: ready-for-dev"), "MOD-0001")
        self.assertFalse(old)
        self.assertEqual([b for b in bad if "künyede" not in b], [])
        self.assertGreaterEqual(len(crit), 2)


def PACK_RE_OLD(path: str) -> bool:
    return bool(pcc.PACK_RE.search(path)) and (REPO / path).is_file()


if __name__ == "__main__":
    unittest.main()
