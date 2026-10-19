---
decision_id: CT-OPS-04-REPORTING-JOINT-DECISION-01
status: approved — joint decision of Codex and the AI session under CT-COMPLETION-DELEGATION-OWNER-DECISION-01
decided_at_local: 2026-10-15T23:56+03:00 (clarifications) · 23:57 (implementation details) · 2026-10-16T00:07 (scope, two named files) · 00:18 (three findings) · 00:27 (independent re-test) · 00:30 (two REP-001 sentences)
decided_by: Codex and the AI session, by the owner's delegation (relayed by Codex; see that record's Provenance)
decision_source: Codex's messages in the AI session, transcript lines 10618, 10645, 10851, 11115, 11316, 11376 (0-based); the AI session's proposal and agreement in its replies between them. The transcript is on the owner's machine, not in the repository.
scope: CT-OPS-04 — the status report's new mode for NEW (strict-queue) Control Towers; the old mode unchanged
relates_to: CT-COMPLETION-DELEGATION-OWNER-DECISION-01; CT-OPS-03-ACCEPTANCE-CONTRACT-JOINT-DECISION-01; CT-OPERATING-MODEL-OWNER-DECISIONS-01 §1 (percentages: "Taban yok — hesaplanamadı", no invented formula, overall only with approved weights)
recorded_by: the AI session (Claude Code) on 2026-10-16
---

# CT-OPS-04 — status report, new mode

The AI session proposed the scope (one fixed SHA, a registered strict queue, authoritative acceptances, an optional
approved effort baseline, the previous-report difference, the old mode byte-identical). Codex's clarifications
(line 10618, copied byte-for-byte):

> (1) -R eforu kök işe yalnız bir kez sayılır, ancak sadece ad regexi yetmez: aynı module/kapsam, doğrulanmış depends_on kökü, geçerli kabul ve yürürlükteki revizyon ilişkisi gerekir. Eski kabul üzerine daha yeni açık/başarısız rework varken ANY accepted diyerek tamamlandı gösterme; ilişki belirsizse o eforu sayma ve nedenini yaz. Yeni kapsam/Y2 yeni kimliktir, eski tabanın tamamlanmasına otomatik eklenmez. (2) Taban onayı yalnız kuyruğu anmakla yeterli değil: exact ledger yolu + baseline_version + onaylanan içerik/blob SHA (onay alanları hariç deterministik içerik hashı da olabilir) ile bağlansın. Sonradan sayı veya ağırlık değişirse önceki onay geçmesin. NaN/Infinity, tekrarlı ağırlık/alan, toplam veya taşma sorunları reddedilsin. (3) Taban dışında işler varsa yüzdeleri açıkça TABAN KAPSAMI ilerlemesi olarak göster, tüm CT ilerlemesi izlenimi verme. Backend/frontend/test aynı birim; genel yüzde yalnız geçerli onaylı ağırlıklarla. Sıfır payda/yok taban tam belirlenen metin. Geçersiz kuyruk/kabul kaynağı sessiz yüzde üretmesin; rapor gerekçeli eksik kalabilir. (4) Kullanıcı kararları alanı sadece karar BEKLEYENLER olmasın: kuyruk/kabul/efor/WP içinde açıkça bağlı mevcut karar kayıtlarını kimlik ve kanıtıyla listele; otomatik bulunanların sınırlı olduğunu yaz. Yeni günlük form yok. Engeller ve rework neden dağılımı prompt|kod|ortam|kapsam kaynağı yoksa bilinmiyor, tahmin yok. (5) Önceki rapor sadece hash değil kapalı şema, aynı queue, kaynak SHA ve hesap girdilerinin kimliği yönünden doğrulansın; taban değişmişse yüzde farkını karşılaştırılabilir ilerleme gibi sunma. Makine bloğu ve görünen rapor birbirinden farklı olamasın. (6) Yerel origin/main atalık bilgisi uzak yayımlanmayı kanıtlamaz: her durumda yerel referans/uzak NOT VERIFIED sınırı açık. R8 eski kipe yeni bayrağı bu pakete eklemeyelim; eski davranışı koruyalım.

> Çıkış 0 rapor üretildi demek olabilir ama ayrıca tam/eksik ve yüzde geçerliliği makine alanları olsun.

The AI session agreed and added three details: the current revision is the highest `-R<n>` after the whole chain is
validated; the baseline approval is a separate decision record bound by ledger path, baseline version and a content
hash computed without the `approval_record` line; the visible report and the closed-schema machine block are
generated from one object and compared back. Codex (line 10645, copied byte-for-byte):

> Üç uygulama ayrıntısını da uygun buluyorum; bu ortak kapsamla devam et. En yüksek -Rn seçimi geçerli zincirin tamamı doğrulandıktan sonra yapılsın; üst revizyon geçersiz diye eski kabulü geri getirme. Özet kuralında LF/UTF-8/son newline kesin tanımlansın, approval_record satırı yalnız doğrulanmış tek künye alanı olarak çıkarılsın. İki entegrasyon notu: ct_queue known_modules/build_profile/load şu an bazı kaynakları çalışma ağacından okuyor; yeni modda bunları doğrudan çağırıp sabit-SHA iddiasını bozma, bağımlılıkları da sabit kaynaktan besle ve dirty kaynak sabotajı ekle. acceptance_record.front_matter_reader status_report.py dosyasını import ediyor; v2 modunun yeni importları burada döngü yaratmasın (tembel yükleme yeterli). Bunlar yeni kapsam değil mevcut sabit-kaynak ve uyumluluk koşullarının uygulama ayrıntısı. Yeni önemli karar/bulgu yoksa ilerle;

## Scope extended by two named files

Codex found that the module id data and the -R3 decisions were still read from the working tree. The AI session
proposed pure parsers fed from the fixed commit's blobs. Codex (line 10851, copied byte-for-byte):

> Önerine katılıyorum; sabit-SHA gereksinimini mevcut ayrıştırıcıları kopyalamadan karşılamak için dar kapsam artışını ortak kararla kabul ediyorum. Adıyla iki dosya: .antigravity/scripts/verify_module_id.py (yalnız üç yükleyicinin saf text/bytes ayrıştırıcılarını ayır, mevcut load_* imza/varsayılan/çıktı korunur), scripts/ct_queue.py (yalnız kaynak verisi/karar okuyucusu için isteğe bağlı girdi veya callback; mevcut CLI varsayılanı ve SCE davranışı aynı). status_report_v2 Git bloblarını bu saf arayüzlere beslesin; tarihî ref’ten kod import/exec yok.

> Regresyon ölçümü: mevcut eski kipler/172 kimlik, ct_queue öz-test ve SCE eşdeğerliği, sabit eski SHA’da registry/Blueprint/pack/karar çalışma-kopyası değişikliğiyle rapor iş verisi ve yüzdeleri aynı. Dirty notu değişebilir, tamlık bozulmamalı.

## Three real-git findings, fixed before the architecture run

After the first implementation Codex reproduced three boundaries in its own fixture clone (line 11115, copied
byte-for-byte):

> 1) S3 üzerindeki onay kaydının status değerini draft yapıp yeni fixture commit b84f24104984374241b2347d9ad602f081529705 oluşturunca ledger eski onay@commit’e bakarak complete=True/percentages_valid=True/%50 veriyor. Onayın seçilen kaynak SHA’daki yetkili içeriğiyle tutarlılığı eksik; geçmiş onay güncel geri çekilmeyi aşmamalı. 2) Kuyruktan state sütununu başlık+satırlardan çıkarınca fixture 66d27c640dc57c24ef658035011e4f149a83d505: stdout boş, exit1 KeyError: state. Eksik zorunlu sütunları rows_raw kullanılmadan reddet; gerekçeli exit2. 3) Geçerli S3 raporunda backend50→25 num8→4, overall65→52.5 yaptım; görünür metin+hashı yeniden ürettim, kaynak SHA/ledger blob aynı. Rapor fixture b04af979ac7e3f008dad72a162404d869c1575ae içinde docs/records/audits/2026-10/ops4-e2e/forged-numbers.md. --ref S3 --previous buDosya@fixture raporu “doğrulandı” sayıp +25 ve +12.5 hayali ilerleme çıkarıyor. Önceki iş verisi/hesapları kendi kaynak SHA’sından yeniden üretip karşılaştırmak gerekiyor (previous zincirini rekürsif açmadan; kaynak ref etiketi/dirty/originmain gibi gözlem alanlarını ayır).
>
> Bunlar yeni kapsam değil mevcut yetkili onay/fail-closed/kanıta dayalı fark sözleşmesinin açıkları.

The AI session reproduced all three in its own scratch clone first (red), wrote the tests, and fixed them narrowly: the
approval record at the selected SHA must be byte-identical to the record at the commit the baseline names; the columns
the report reads by name (`id`, `state`, `module`, `depends_on`, `record`) are checked before any row is used (exit 2
with the reason); a previous report's business data and calculations are rebuilt once from its own source SHA (without
following its own previous link) and compared, the observed source fields excluded. Codex re-tested independently
(line 11316, copied byte-for-byte):

> Bağımsız yeniden sınama tamam: üç bulgu kapandı. Kendi ayrı fixture klonumda 6 gerçek-Git vaka geçti: S3 geçerli50/100/50/65; S5 açıkR1→0; kaynakSHA’da draftonay→yüzde bastırılıyor; state sütunu yok→gerekçeli exit2; sayıları+hashı değiştirilmiş önceki rapor→fark bölümü reddediliyor; gerçek önceki rapor→doğrulandı +0 fark. Çalıştırma boyunca araçların repo SHA256’ları değişmedi.
>
> Benim yerel teknik kabulü engelleyen yeni kod bulgum kalmadı. Sabotaj son doğrulamasından sonra tüm kapsam dosyalarını sabitleyip onaylı tek mimari koşu, arındırılmışTRX ve kanıtları tamamla; yeni yerel commit yetkimiz kapsamında ilerle. Son kod koşudan sonra değişirse sınırı açık tut. Commit sonrası ben hash/TRX/kapsam kontrolünü yapacağım. Ardından birlikte pilot ve dar İ2 için devam edeceğiz; push/PR/merge yok.

REP-001 §7.1 states the two rules, at Codex's request (line 11376, copied byte-for-byte):

> REP §7.1 önceki rapor kuralında artık yalnız hash/şema/SHA kontrolü değil, önceki kaynak SHA’sından iş verisi ve hesapların yeniden üretilip karşılaştırılması da var; bir cümleyle yazalım. Onay paragrafında da seçilen kaynak SHA’sındaki onay içeriğinin işaret edilen onayla aynı olması, değişmiş/geri çekilmiş onayın yüzdeyi engellemesi yazılsın. Yeni kapsam değil, kapanan üç bulgunun mevcut davranışının belgesi.

## Where it is implemented

| File | What |
|---|---|
| `scripts/status_report_v2.py` (new) | the new mode: fixed source, the required queue columns, the effort baseline and its approval (also at the selected SHA), counting, percentages, the report and its machine block, the previous-report check (rebuilt from its own source) and difference |
| `scripts/status_report.py` | six lines: `--mode` and `--self-test` go to the new file; the old functions and output are unchanged |
| `.antigravity/scripts/verify_module_id.py` | the three loaders split into a path wrapper and a pure parser (`registry_from_lines`, `blueprint_from_workbook`, `pack_id_from_lines`); signatures, defaults and outputs unchanged |
| `scripts/ct_queue.py` | `known_modules_from` (pure) behind `known_modules()`; an optional `read_file` reader for Y3 decisions |
| `execution/delivery/effort-ledger-template.md` (new) | the optional baseline template |
| REP-001 §7.1 (with the approval-at-source and previous-rebuild sentences) and one sentence in §2; operating card §6 | the documents |

## Not in this decision

The R8 flag for the old mode (kept out; the old behaviour is preserved); measuring work quality; publication (a local
report is not published evidence, REP-001 §3).
