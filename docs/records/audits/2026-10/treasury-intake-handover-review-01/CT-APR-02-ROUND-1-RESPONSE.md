---
record_id: TREASURY-CT-APR-02-ROUND-1-RESPONSE
kind: CT-APR-02 1. tur bulgularına yazarın (Treasury Intake CT) cevabı — onay değildir, inceleme değildir
reviewed_round: Codex oturumu 01a14687-c8a7-7891-8ec9-35fda19061f3; bulgular docs/records/reviews/*/2026-10-25-*.md (commit 1cb85b1f6), satırlar execution/approvals/reviews.tsv (commit 74edb4b5b)
fixed_in: dokuz hedef (bu kaydın commit'i) — hepsinin SHA-256'sı değişti, 2. tur bağımsız inceleme gerekir
---

# CT-APR-02 1. tur — bulgular ve düzeltmeler

| Hedef | Bulgu | Düzeltme |
|---|---|---|
| MOD-0220 §22 | F-1 ENGEL: `Teslim dilimleri` yok | Pack sonuna `## Teslim dilimleri` (tek dilim DL-1, yalnız §22; eski kapsamdan yetki almaz / vermez; `whole_pack` onaylayanda); bitiş AC-T1…T5. AC başlığı virgülsüz yeniden adlandırıldı (dilim ayrıştırıcısı virgülle böler). `pack_approval.slices()` → `['DL-1']`. |
| MOD-0023 §17 | F-1 ENGEL: dilim tablosu yok | `## Teslim dilimleri`: DL-1 silmeme + okuma alanları (AC-W1…W4, W13), DL-2 yarış / sıralı tekrar / fingerprint (AC-W5…W12, W14); teslim MOD-0023 sahibinin WP'si (D25/1). `slices()` → `['DL-1','DL-2']`. |
| MOD-0023 §17 | F-2 ORTA: AC-W2 / W6 / W7 §17.7 ile çelişiyor | AC-W2: `instances/{id}` anahtarı isteğe bağlı, AC kapsamı by-objects + history; AC-W6/W7: son yanıt 200 ya da 409 / 503 (§17.7/5), yarış sonrası aynı anahtar tekrarı 200 `isIdempotent`. |
| MOD-0048-FU01 §21 | F-1 ENGEL: dilim tablosu yok | `## Teslim dilimleri` tek dilim DL-1 (yalnız §21; adlandırılmış "BRD Provider Internal Foundation" adımından yetki almaz); bitiş AC-R1…R4. `slices()` → `['DL-1']`. |
| MOD-0048-FU01 §21 | F-2 ORTA: AC-R3 seed dosyası yokluğu = küme yokluğu sanıyor | AC-R3 temiz veritabanı önkoşuluyla; ayrı vaka (d): daha önce yayımlanmış + dosya kaldırılmış → küme DB'den yayımlı kalır, kaldırma stewardship akışıyla. |
| DCP | F-1 ENGEL: §15 her üyeden pack + kod alma izni ister, MOD-0269'un pack'i yok | §15'e MOD-0269 istisnası: teslim FROZEN sözleşme + MOD-0126 içi port / bağdaştırıcı, yalnız MOD-0126 pack kapısından; ayrı `ready-for-dev` / izin aranmaz. §5 üye satırı aynı. |
| DCP | F-2 ORTA: §11 aday kapsam dışı, §8.1 TRM-S4 aday içeriyor | TRM-S4 "planlı, bu DCP onayıyla dağıtılamaz; kapsama girmesi DCP değişikliği ister". |
| MOD-0126 | F-1 ORTA: AC-6a dilime atanmamış | DL-2 bitişine açıkça AC-6a. |
| MOD-0126 | F-2 ORTA: AC-41(d) DL-1'de, Approve / Release sonraki dilimlerde | AC-41 (a–c, e) DL-1; (d Approve) DL-2; (d Release, tam kapanış) DL-3. |
| MOD-0126 | F-3 DÜŞÜK: port kaydı bekliyor; izin öneki "öneri" | Port D30 ile yapıldı; `finance-treasury.*` KARAR D11/4. |
| MOD-0119 | F-1 ORTA: AC-40 tamamı DL-1'de | AC-40 (Validate) DL-1; (Submit / Approve, tam kapanış) DL-2. |
| MOD-0119 / 0120 / 0124 | DÜŞÜK: port kaydı bekliyor | D30 ile hizalandı. |
| MOD-0289 | (bulgu yok, UYGUN) — CT ölçümü | `pack_approval.slices()` dilim bölümünde ikinci bir tablo (dilim kabul kapısı) olduğu için `None` dönüyordu; tablo ayrı `## Dilim kabul kapısı` başlığına taşındı → `['DL-1'…'DL-4']`. Bu yüzden MOD-0289'un da hash'i değişti ve yeniden incelenmeli. |

Bulgu dosyaları değiştirilmedi (inceleyenin kaydıdır). Düzeltmeler hedeflerin yeni sürümleridir; POLICY-REVIEW-START gereği her
hedef yeni sürümüyle 2. tur bağımsız inceleme ister. Belge düzeltmesidir; build / test / runtime yok.
