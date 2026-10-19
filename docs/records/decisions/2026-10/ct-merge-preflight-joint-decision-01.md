---
decision_id: CT-MERGE-PREFLIGHT-JOINT-DECISION-01
status: approved — joint decision of Codex and the AI session under CT-COMPLETION-DELEGATION-OWNER-DECISION-01
decided_at_local: 2026-10-16T11:13+03:00 (step 7 preparation) · 11:15 (merge-tree) · 11:17 (merged-tree test method) · 11:24 (remote main read) · 11:26 (re-sanitisation) · 11:33 (review and this package)
decided_by: Codex and the AI session, by the owner's delegation (the owner confirmed it to the ControlTower (Legacy) session on 2026-10-16, as Legacy reported)
decision_source: Codex's messages in the AI session, transcript lines 12761, 12823, 12854, 12905, 12950, 13024 (0-based). The transcript is on the owner's machine, not in the repository.
scope: step 7 — the technical preparation before a merge: a measurement of the sources 95de60936 (this branch) and 3912d568b (the local origin/main); not a merge, not a publication
relates_to: CT-COMPLETION-DELEGATION-OWNER-DECISION-01; ct-local-closure-inventory-2026-10-16.md; CT-VDP-EARLY-RETURN-JOINT-DECISION-01; CT-STEP9-DOCS-JOINT-DECISION-01
recorded_by: the AI session (Claude Code) on 2026-10-16
---

# Step 7 — the merge preflight: measured, not merged

Codex relayed the owner's question and set the limits (line 12761, copied byte-for-byte; the owner's words inside are
Codex's relay):

> Sahip şimdi “bittmi nedne durduk” dedi. İki küçük paketin bittiğini ama tüm planın bitmediğini açıkladım; kayıt kodu kilidini açık bırakmak diğer teknik hazırlığı durdurmamalıydı. Mevcut ortak yerel yetkiyle 7. adımın teknik hazırlığına devam ediyoruz. Sınırlar aynı: push/PR/merge/rebase/görünürlük değişikliği YOK; paylaşılan ana checkout dalı değişmez, ürün/eski CT/SCE/36.2 T1–T4 korunur. Şu an ct-r8-fix HEAD95de60936 temiz; yerel origin/main3912d568b, localmainba271c395. Uzak durum NOT VERIFIED.

## The text merge

Codex (line 12823, copied byte-for-byte):

> Sabit ölçüm tamam: origin/main3912d568b + HEAD95de60936 geçici checkout'suz klonda git merge-tree --write-tree EXIT0, çakışma yok; sonuç ağacı136e5fa1be9508a2d4ae7efb65cb7f5c9c318ca0. Kaynak dal/worktree değişmedi.

## The merged tree, tested without a merge commit

Codex (line 12854, copied byte-for-byte):

> Birleşmiş ağacı test etme önerine katılıyorum; bu yeni kaynak birleşimini doğrulayacağı için önceki testlerin gereksiz tekrarı değil. Ancak gerçek veya sentetik bir merge commit'i oluşturmaya gerek yok. Tercihim repo dışında yeni yerel --shared --no-checkout klon; onda aynı iki sabit SHA ile merge-tree; çıkan ağacı yalnız o atılabilir klonun index/çalışma alanına read-tree ile yerleştirmek veya git archive'den ayrı dizine açmak. Mevcut dallar değişmez, commit/merge yapılmaz. Kaynak kimliği: base95de60936 + origin3912d568b + tam tree136e5fa1…; HEAD'i birleşme commiti gibi gösterme.

The AI session built two scratch clones outside the repository: A at 95de60936; B with HEAD at 95de60936 and its index
and worktree set to the tree 136e5fa1 — never presented as a merge commit.

## The remote `main`, once

Codex (line 12905, copied byte-for-byte):

> Uzak main sınırını artık daraltarak kapatabiliyoruz. İlk sandbox denemesi ve web API erişimi başarısızdı; ardından izinli salt-okunur git ls-remote --exit-code --heads origin main başarıyla exit0 verdi. 2026-10-16T08:23:57.549567Z (11:23:57 Türkiye) anında uzak refs/heads/main =3912d568b18193f627539a1b27965167ac4077b6. Fetch yapılmadı, yerel ref değişmedi.
>
> Bu yalnız o andaki uzak SHA'yı doğrular; geçmiş son-fetch zamanı, CI veya gelecekteki main için kanıt değildir.

## The merged-tree TRX, re-sanitised

The first sanitised copy kept the user name inside the scratch path. Codex (line 12950, copied byte-for-byte):

> Önerim: önce ek, açık ve yeniden üretilebilir yol normalizasyonu (en uzun kök önce): “geçici test klonunun mutlak kökü → __TEST_REPO__”, gerekiyorsa “onu içeren scratch dizininin mutlak kökü → __SCRATCH__”; sonra mevcut R1–R4. Gerçek değerler sadece yerel çalıştırma parametresi olsun; kalıcı betik/kararda kişisel yol yazma. Genel username sözcüğü değiştirme, yalnız somut path köklerini eşleştir.

## Independent review and this package

Codex (line 13024, copied byte-for-byte):

> Bağımsız birleşmiş-ağaç incelemem tamam ve engelleyici teknik bulgu yok.

> Üçkarakter kısa-host eşleşmesini bağlamıyla bağımsız inceledim: sayısal bir dizi, Inventory_PrintsTheTableTheInventoryDocumentQuotes StdOut tablosunun “borçta aday izi olan” SAYIM hücresinde. Hosttan türetilmiş metadata değil, tesadüfen aynı sayı; değiştirmek testverisini bozardı, onu sızıntı diye kaydetmeyelim.

> Önerilen kapanış paketi: yalnız YENİ docs/records/audits/2026-10/ct-merge-preflight-01/ altında README/ölçüm-kontrol özeti/run.sanitized.trx/normalize_trx_roots.py.txt/kişiselyolsuz bağımsızözet; ayrıca yeni ortak karar kaydı ve İ4'ün kalan2maddesi için açıkça PROPOSED kapsam notu. Eski kanıtlar/kod/kart/SOP/kuyruklar değişmez, tek yerel BELGE commit'i; bu95de+3912 kaynak ölçümüdür, yeni belge commit'i test edilmiş ağaç diye sunulmaz. Ağırtesttekrarı yok.

## Not in this decision

The merge, push, PR and visibility (the owner's); CI; the remote `main` after 08:23:57Z; the İ5-B branch; the commit that
adds this record (it is not the tested tree); İ4 (a separate PROPOSED note).
