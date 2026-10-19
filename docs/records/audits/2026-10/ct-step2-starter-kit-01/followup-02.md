# Evidence — step 2, follow-up 02 (2026-10-15)

A second independent review (Codex, 2026-10-15, issued by the owner; transcript line 9138, 0-based) of 171e949f3:

> Kalan sorun: Genel geçmiş hatası dağıtımı durdurmuyor.

> Gerekli düzeltme küçük: Yeni katı kuyrukta, geçmişin doğrulanmasını engelleyen genel hata varsa require DUR / çıkış 1 vermeli. Test yalnız hatanın bulunduğunu değil, komutun gerçekten dağıtımı reddettiğini doğrulamalı. SCE’nin mevcut davranışı korunmalı.

171e949f3 and the earlier evidence in this folder are not changed; this follow-up is a new commit on top of it. Details
are in `checks-followup-02.txt`. Only `scripts/ct_queue.py` changed; nothing under `.antigravity/`, so no single-writer
agreement was needed.

## The approval

The owner was away. Codex relayed the owner's answer to its question about this exact scope (one code file, three
evidence files, one architecture run, one local commit), in a message the owner pasted into this session (transcript
line 9197; the owner's words as Codex quoted them, copied byte-for-byte):

> "ben bır sure pc basında ılmucam aam sız durmammalısını bu ısı bıtırın bu tarz onayları verıyorum sen ve calude ortak karar verdıgınız surece unutma her zman claudun da fıkrını al."

Codex's limits on that approval, from the same message (copied byte-for-byte):

> Push, PR, merge, rebase, görünürlük değişimi veya CT-PQG-01 gibi yeni aşama yetkisi çıkarmıyoruz.

The AI session agreed with the scope (it had proposed it) and named one technical choice before writing: the shared
decision function covers every `require` stop — structural error, profile error, an error on the id, an error on no
single id. Codex agreed (transcript line 9225, a message the owner sent into this session; copied byte-for-byte):

> Codex: Teknik tercihine katılıyorum. Yapı/profil/işe bağlı/genel hata yollarını ortak karar fonksiyonunda toplamak mevcut kapsamın doğru uygulaması.

and asked to keep the positive control: "özellikle sağlıklı bir strict kuyrukta READY işin hâlâ dağıtılabildiği pozitif kontrolü de mevcut öz-testlerde koru."

This approval covers this follow-up only. It gives no push, PR, merge, rebase, visibility change or new stage.

## Defect and fix

| | 171e949f3 | Follow-up 02 |
|---|---|---|
| Cause | `check_queue` attaches some errors to no single id (a history version without its `id` or `state` column; a strict queue without a `module` column). `require` refused only ids that carried an error, so those errors printed "NOT: kuyrukta 1 doğrulama hatası var" and the id stayed DAĞITILABİLİR | `check_queue` keeps the id-less errors apart (`global`). In a strict queue `require` refuses every id while one is open: "DUR <id>: katı kuyrukta bir işe bağlanmayan doğrulama hatası var — …", exit 1. Tolerant queues (SCE / MVP6) are unchanged |
| Where the decision lives | inline in `main()` | `require_decision()`: structural error, profile error, id error, id-less error. The command line and the self-test call the same function, so the tests measure the refusal itself, not only that an error was found |

The missing `module` column was found by the AI session while reproducing Codex's case; it is the same cause.

## Proof

| Check | 171e949f3 | Follow-up 02 |
|---|---|---|
| Scratch clone, strict queue `hist-ct` whose first committed version has no `state` column: `require W1` | DAĞITILABİLİR, exit 0 | DUR, exit 1 |
| Strict queue `nomod-ct` without a `module` column: `require W2` | DAĞITILABİLİR, exit 0 | DUR, exit 1 |
| Healthy strict queue `ok-ct` (positive control): `require W5` | DAĞITILABİLİR, exit 0 | DAĞITILABİLİR, exit 0 |
| `test-ct` (MOD-9999); the same with the transition table moved away | DUR · DUR | DUR · DUR (follow-up 01 unchanged) |
| SCE `require Q124` · `require Q99` | DAĞITILABİLİR 0 · DUR 1 | the same |
| `validate --queue hist-ct` | FAIL, exit 1 | FAIL, exit 1 |
| `ct_queue.py --self-test` | 52/52 | 57/57, including a strict clean-queue positive control (exit 0); with the new rule removed: 55/57, the two named refusal cases fail |
| SCE / MVP6 on every ref holding the queue: exit code and decision line of `require` for every id, and `validate` errors, 171e949f3's command-line path against `require_decision` | — | 55 refs, 27,528 id comparisons: 0 differences; `strict` = no, no profile |

## Run 3 — source identity

The code was pinned before the run and was unchanged after it:

| Item | Value |
|---|---|
| Base commit | 171e949f3c66ec8e1a0ccc806b649cec87c16011 |
| Changed file | `scripts/ct_queue.py` (nothing else) |
| `scripts/ct_queue.py` sha256 | `f3335ae1d5bd38e4200fa7c8e4dcdfca549dc9705d0a60b7cecd3dec451de0c0` |
| Diff against the base, sha256 | `688ae77a79d6e2c8376c92a2dab1afc898e1edbfc99b9c8b466031425ddbb66e` |
| Run 3 raw TRX sha256 (raw kept outside the repository) | `5fbcede9783f2663f3693dde0d216c5aa4b02272e6cedf8999baa9da7077c7d2` |
| `run3.sanitized.trx` sha256 | `e7021a365d1b9baae3c703bb87a7ee83288f7a0cc6354d974139e78393f973d9` |
| Sanitisation | Same tool and rules as step 1; R1 306 · R2 145 · R3 1 · R4 1; home, user, unsanitised computerName and full host name left: 0 |

Result: 131/138; 7 BİLİNEN, exit 0, raw and sanitised. The seven failure fingerprints are identical to `run2.sanitized.trx`.

## Regression risk

- 🟢 SCE / MVP6: tolerant; measured above.
- 🟢 New Control Towers: no strict queue is registered today. Once one is, an id-less error stops every dispatch from
  that queue until it is fixed — intended.

## NOT VERIFIED

- Installing `scripts/requirements-tools.txt` from PyPI; failure fingerprints in CI; the remote state of `main` (only
  the local ref `origin/main`, 3912d568b, was read; no fetch was made).
