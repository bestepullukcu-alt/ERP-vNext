---
decision_id: CT-OPERATING-MODEL-OWNER-DECISIONS-01
status: approved — the decisions below were in force on 2026-10-15; the "Open" section lists items that are NOT decisions
decided_at_local: 2026-10-15T02:35+03:00 to 2026-10-15T13:03+03:00 (each decision carries its own time)
decided_by: the owner of this work stream (the user of the AI session; this record adds no name)
decision_source: owner messages in the Claude Code session "AI" (accdcf92), 2026-10-15. Most texts were drafted by Codex and issued by the owner by pasting them. The session transcript is on the owner's machine, not in the repository.
scope: the Control Tower operating process — SOP v2.5, the daily operating card, CT task queues (read-only), known failing tests, decision records
bound_to: commits a6082e248 (branch chore/ct/ct-ops-01-card-approvals-known-failures) and 849f9847a (branch chore/ct/ct-ops-02-queue-validator), both local only, not pushed; docs/roadmap/plans/mvp6-process-pilot-01/CT-QUEUE.tsv sha256 ea74b4f8a6aa601802994d8d5f6824059bba3f773e305c3dee2c5c1473118cba (579 rows, unchanged by this work)
recorded_by: the AI session (Claude Code) on 2026-10-15, on the owner's instruction of 2026-10-15T12:53+03:00; this record makes no decision of its own
---

# Control Tower operating model — owner decisions in force

Every quotation is copied byte-for-byte from the owner's message, the source's spelling included. Items still waiting
for the owner are listed under "Open" and are not decisions.

## 1. Improve the existing system; no parallel system

2026-10-15T02:35+03:00:

> * SOP v2.5, REP-001, CT-QUEUE ve mevcut status_report.py korunacak.
> * Yeni ve paralel bir yönetim sistemi oluşturulmayacak.
> * Yeni uzun SOP metinleri yazılmayacak.
> * Eksik mekanizmalar mümkün olduğunca doğrulayıcılarla tamamlanacak.
> * Raporlama ile kabul/veri kaydı birbirinden kopuk işlere ayrılmayacak.

Percentages:

> Geçerli efor tabanı bulunmayan alanlarda "Taban yok — hesaplanamadı" yaz.

> Yeni formül veya tahmini yüzde uydurma.

> Genel yüzdeyi yalnız uygun onaylı ağırlıklar ve kanıtlar varsa hesapla.

Records and acceptance:

> * Karar künyelerini doğrulanabilir kaynaklarla ilişkilendir.
> * Eksik künyeleri tahmin ederek doldurma.
> * Doğrulanamayan eski kayıtları ayrı işaretle.
> * ACCEPTED, DONE ve DELIVERED gibi durumları anlamlarını doğrulamadan birbirine dönüştürme.
> * Kanıtı olmayan işleri tamamlandı sayma.

Standing approvals and ownership:

> Kalıcı onay listesi oluşturulmasını kabul ediyorum, fakat bu liste mevcut GIT-002 ve XMC-001 kurallarını geçersiz kılamaz.

> Domain → Control Tower sahiplik haritasını gerçek kayıtlardan çıkar. Belirsiz sahiplikleri kendiliğinden atama.

Deferred from the first package:

> CT-QUEUE taşıma, 1.103 kayda ilişkin geriye dönük işlem, backlog temizliği ve büyük raporlama değişiklikleri bu ilk pakette yapılmasın.

**Status:** in force. The percentage fields are not implemented yet (CT-OPS-04).

## 2. CT-OPS-01 — standing approvals, ownership, known failing tests

2026-10-15T02:39+03:00. Standing (no repeated approval inside an authorised scope):

> * Salt-okunur inceleme ve ölçüm.
> * REP-001'e uygun rapor üretme.
> * Ortak test kilidine uyarak, onaylı kapsamda test çalıştırma.
> * Kendi geçici çalışma dosyalarını kullanma.
> * Açıkça onaylanmış iş paketi için dal ve worktree oluşturma.

Explicit approval every time:

> * Commit, push, PR ve merge.
> * Yıkıcı Git işlemleri.
> * `.antigravity/**` değişiklikleri.
> * Başka modüle müdahale (XMC-001).
> * Mimari veya kapsam değişikliği.
> * Canlı ortam müdahalesi.
> * Ortak dosyalarda tek-yazar anlaşması.

> Kalıcı onay listesi GIT-002 veya diğer üst kuralları değiştiremez.

Known failing tests:

> Yalnız doğrulanmış mevcut 7 mimari test hatasını kaydet.

> Ancak ATLA yalnızca tekrarlanan yerel tanılama için öneridir.

> CI, zorunlu kabul testleri, güvenlik testleri veya kullanıcı tarafından açıkça istenen testler bu mekanizmayla atlanamaz.

Acceptance, 2026-10-15T10:41+03:00:

> CT-OPS-01 sonuçlarını kabul ediyorum.

> 1. Belirsiz 7 domain'e şu anda Control Tower sahibi atama. Kanıt bulunana ve açıkça onaylayana kadar "Belirsiz — sahip ataması bekliyor" olarak kalsın.

> 2. Aktif Control Tower'ın 6 ek önerisini sonraki ilgili iyileştirme aşamalarının kapsamına al. CT-OPS-01'i yeniden açma.

**Status:** in force. Implemented in a6082e248: operating card §7 (approvals) and §8 (domain → Control Tower map),
`tests/known-failures.tsv`, `scripts/known_failures.py`.

## 3. Prompt Quality Gate

2026-10-15T11:11+03:00:

> 1. Prompt gönderilmeden önce kalite kapısı gerekli.
> 2. Module Pack, API sözleşmesi, izin anahtarları, test adları ve sabotaj maddeleri gerçek kaynaklarından doğrulanmalı. Sözleşme yüzeyleri yorumlanarak değiştirilmemeli.
> 3. Her uygulama promptunda düşük maliyetli mekanik kontrol kullanılmalı.
> 4. HIGH riskli promptlar gönderilmeden önce bağımsız incelemeden geçmeli.
> 5. İnceleme puanına değil, açık kritik/engelleyici bulgulara ve kaynak uyumuna dayanmalı.
> 6. Kritik bulgu düzeltildiğinde bütün sözleşme yüzeyi yeniden doğrulanmalı.
> 7. İnceleme agent'ına sınırlı fakat doğrulanabilir kaynak paketi verilmeli. Bütün repository'yi gereksiz yeniden incelemesin.
> 8. Mekanik kontrol, bağımsız incelemenin yerine geçmemeli.
> 9. Başarı yalnız geliştirme agent'ının durmamasıyla ölçülmemeli. Prompt kaynaklı yeniden iş ve çelişkiler de izlenmeli.
> 10. Bu kural yalnız kişisel hafızada kalmamalı; daha sonra repository'deki kalıcı Control Tower sürecine işlenmeli.

Planning:

> Bu işi CT-OPS-02'den sonra, ayrı bir iş olarak takip edelim.
> Geçici iş adı:
> CT-PQG-01 — Prompt Quality Gate
> Mevcut CT-OPS-03 (Kabul ve Kanıt) ve CT-OPS-04 (Raporlama) adlarını değiştirme.

**Status:** decided, not implemented. CT-PQG-01 has not started.

## 4. CT-OPS-02 — Control Tower task queues and the R8 target model

Architecture, 2026-10-15T11:24+03:00:

> 1. SCE/MVP6 CT-QUEUE yalnız SCE Control Tower'ına ait olarak kaydedilecek.
> 2. Diğer Control Tower'ların görevleri SCE kuyruğuna yazılmayacak.
> 3. Gelecekte birden fazla Control Tower kuyruğu registry üzerinden okunabilecek.
> 4. Kayıtlı görev kaynağı bulunmayan Control Tower için dağıtım kendiliğinden serbest olmayacak.
> 5. Resmî olarak doğrulanmamış durumlar otomatik READY, DONE veya DELIVERED sayılmayacak.
> 6. Eski durum metinleri ve kimlikler değiştirilmeden korunacak.
> 7. R8'in ortak görev kaynağıyla ilgili çelişkisi ayrı ve açık bir mimari karar olarak takip edilecek.

User experience:

> Yeni mekanizma Control Tower kullanıcılarına elle veri doldurtmamalı.
> Mümkün olan bilgiler Git, mevcut kuyruk ve onaylı kayıtlardan otomatik alınmalı.
> Bir kullanıcı yalnızca gerçekten gerekli mimari veya yetki kararını vermeli.

Acceptance and R8, 2026-10-15T12:03+03:00:

> 1. CT-OPS-02'nin yerel geliştirmesini ve testlerini kabul ediyorum.
> 2. Commit `849f9847a` korunsun. Push, PR veya merge yapma.
> 3. CT-QUEUE dosyasına dokunma. Mevcut 62 tekrarlı kimliği ve Q484–Q489 çakışmasını otomatik düzeltme.
> 4. SCE kuyruğunun sahipliği ve durum semantiği onay bekliyor olarak kalsın.
> 5. R8 için hedef model:
> Her Control Tower kendi kayıtlı ve yetkilendirilmiş görev kaynağından sorumludur.
> 6. Genel R8 istisnası oluşturulmasın.
> Kayıtlı görev kaynağı olmayan Control Tower'ın yeni dağıtımı kendiliğinden serbest bırakılmasın.
> 7. SOP R8/T4 mevcut hâliyle SCE kuyruğuna bağlı olduğundan, çoklu kuyruk modelini henüz zorunlu olarak etkinleştirme.
> 8. R8/T4 uyumluluk düzeltmesini bir sonraki öncelikli geçiş işi olarak açık tut. Mevcut CT-PQG-01, CT-OPS-03 ve CT-OPS-04 işlerini silme veya numaralarını değiştirme.
> 9. SCE Control Tower'ından üç bağlayıcı açıklama bekliyoruz:
>    * CT-QUEUE sahipliği ve yedi resmî durum;
>    * tekrarlanan Q kimliklerinin tarihsel anlamı ve T4 yazma semantiği;
>    * Q484–Q489 dal çakışmalarının çözümü.

**Status:** in force. Implemented in 849f9847a: `scripts/ct_queue.py`, `execution/delivery/ct-queue-registry.tsv`,
`execution/delivery/ct-queue-states.tsv` (owner confirmation and state classification both `pending`), operating card
v1.2 (an advisory queue check and the open R8 decision). SOP R8 and T4 are unchanged.

**Correction.** The approval of 11:24 contained this criterion:

> * Mevcut sekiz kimlik tekrarı uyarı olmalı.

It rested on a wrong figure in the AI session's own report. Measured at 6b8a62d5a: 62 ids repeat, 141 rows. The
acceptance of 12:03 uses 62. All 62 are reported as warnings; a new repeat is an error.

## 5. Decision records and local commits

2026-10-15T12:53+03:00:

> Mevcut repository karar kayıt standardını kullan.
> Bu çalışmada alınmış ve hâlen geçerli kararları, kaynaklarını ve durumlarını kalıcı hâle getirmek için sınırlı bir kayıt paketi hazırla.
> Eski kararları tahmin ederek yazma.
> Onaylanmamış görüşleri onaylı karar gösterme.
> Yeni paralel karar sistemi oluşturma.
> Karar kaydı oluşturulmasını onaylıyorum.
> Ortak dosyalara yazmadan önce tek-yazar koordinasyonunu doğrula.

> Repository public olduğu için şu anda push yapma.
> Hassas içerik taşımayan güvenli bir yedekleme yöntemi veya repository görünürlüğü doğrulandıktan sonraki push planını öner.
> Main'e doğrudan commit veya push yapma.
> PR veya merge yapma.

**Status:** in force. This record and the İ5 record next to it are its first output.

## 6. R8 transition — a Control Tower dispatches after registering its own queue

2026-10-15T13:03+03:00, the owner's answer to a question asked through the session's question tool:

> 6+7 itirazı: kayıtlı kuyruğu olmayan Control Tower'lar SOP güncellenene kadar ne yapsın?

Selected option, with the description shown to the owner:

> B: kendi kuyruğunu kaydettiren dağıtır (Önerilen)
>
> Genel istisna yok; her CT kendi kuyruğunu kaydeder. Bunun için CT-OPS-01/02'nin ana dala girmesi gerekir.

**Status:** decided, not yet effective. A Control Tower registers its queue in `execution/delivery/ct-queue-registry.tsv`,
which exists only in 849f9847a; that commit reaches `main` only through push, PR and merge, and push is held by
decision 5. SOP R8 and T4 are unchanged.

## Work order on 2026-10-15

R8/T4 compatibility fix (decision 4, item 8) → CT-PQG-01 → CT-OPS-03 (acceptance and evidence) → CT-OPS-04 (reporting).
The R8/T4 fix changes T4, the SCE Control Tower's writing template, so it depends on the SCE answers below.

## Open — NOT decisions

| Item | State on 2026-10-15 |
|---|---|
| Decision 6 precondition | Decision 6 needs CT-OPS-01 and CT-OPS-02 on `main`; decision 5 holds the push. Until both move, a Control Tower without a registered queue still has no compliant path for new dispatch. Measured: no active non-SCE work package has a CT-QUEUE row. |
| SCE Control Tower | Three binding answers requested (decision 4, item 9); none received. |
| Operating card line "Ledger row (v2.5 R8)" | Added by CT-OPS-01; it carries the SCE-bound R8 to every Control Tower. Proposed for the R8/T4 fix; not decided. |
| Prompt Quality Gate notes from the AI session | Reuse SOP §9 / §9.1 for HIGH risk; extend the §20 preflight and §36.1 instead of a new document. The "independent evaluation" cited in decision 3 was not made in the AI session. Proposals only. |
| Repository visibility and the push plan for local commits | The repository is public; owner decision pending. |
| Single-writer record | The Closure Control Tower acknowledged CT-OPS-01 on 2026-10-15T02:47+03:00 and stated it would not write the SOP or the operating card until the PR merges. CT-OPS-02's card change relied on that statement and a git measurement; no new acknowledgement was requested. For this record and the İ5 record next to it, the Closure Control Tower acknowledged single-writer on 2026-10-15. |
| Overlapping Control Tower rule for HIGH-risk prompts | Reported by the Closure Control Tower on 2026-10-15, in its words: "pack sözleşme yüzeyi alıntıyla, mekanik kontrol, tek taze alt ajan incelemesi, 0 engelleyicide dur"; it also reports that the owner left making it permanent in the SOP and the card to the AI session. The owner's own text is in that session and is not quoted here. To be reconciled with decision 3 in CT-PQG-01. |
