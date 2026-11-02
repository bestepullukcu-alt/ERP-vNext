---
format: rep-001-v2
module: MOD-0230
work_package: WP-0230-DL4D
queue: pvg
evidence_level: E4
required_evidence_level: E4
required_evidence_source: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md@edfde7292f314ad7056ea68af7c2d0fc71df6f98:1929
decision: accepted
completion: WP Done
conditions: -
commit: edfde7292f314ad7056ea68af7c2d0fc71df6f98
evidence: docs/records/audits/2026-10/pvg-dl4d-live-01/run-02/run-record-v1.json@c09dd1d74b1ccdc9406e1a57321da079fa7b1848
acceptance_list: docs/records/audits/2026-10/pvg-dl4d-acceptance-02/LIST.md@88dd2864865a435cd98a2a31e07bbc5472f39f61
date: 2026-10-30
supersedes: docs/records/audits/2026-10/pvg-dl4d-acceptance-01/RECORD.md
verifier: PVG Control Tower (bağımsız doğrulayıcı; WP'yi yazan işçi oturumu değil)
implementer: WP-0230-DL4D işçi oturumu pvg-dl4d (backend-architect), 2026-10-30
module_pack: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md@edfde7292f314ad7056ea68af7c2d0fc71df6f98
---

# Kabul kaydı — WP-0230-DL4D (02, paket revizyon 13)

**Karar: kabul edildi.** Bu kayıt `docs/records/audits/2026-10/pvg-dl4d-acceptance-01/RECORD.md`'nin yerine geçer. Kayıt 01'in listesinde AC-34'ün kanıt hücresi iki başvuru taşıyordu; kabul aracı ise tek bir `yol@commit:satır` istiyor. Liste 02 kanıtı canlı koşunun satırıyla veriyor; bağımsız ölçüm Gerçek sonuç hücresinde. Ölçüm ve karar aynı.

## Dayanak

- **AC-34 geçti: canlı ölçüm.** Servis ve Gateway bu kod ağacından başlatıldı. Kanıt: `run-02` `stdout.txt`.
  - Kimliksiz çağrı **401** aldı (satır 21).
  - Geçerli token **200** aldı (satır 28). Gövdede tam üç üye var: `reportTypes`, `productRoles`, `unknownCountry`. Kodlar `PvgIntakeOptionSets` ile aynı ve `StringComparer.Ordinal` sırasında; beklenen değer kaynak dosyadan okundu.
  - Aynı istek doğrudan servise gönderildi. Durum aynı, gövde bayt bayt aynı (satır 31).
  - Kiracı eksik ve kiracı farklı istekler servis tarafında liste ucuyla aynı durum ve neden koduyla reddedildi; seçenek kodu dönmedi (satır 34, 36).
  - `PUT …/options` **404** aldı; `options` ayrılmış parça (satır 39).
  - Betik yalnız kendi başlattığı iki süreci kapattı; sonra iki port da boştu. Komutun çıkışı 0, `run_record.py verify` geçerli.
- **Bağımsız ölçüm: KABUL EDİLEBİLİR.** Kayıt: `docs/records/audits/2026-10/pvg-wave6-verify-01/VERIFY-DL4D.md`.
  - Yalnız izinli üç dosya değişti.
  - Beş PVG süiti yeşil (pvg-api 298/298).
  - Altı adlı test ve prompt'un bütün sabotajları beklenen kırmızıyı verdi.
- **Gerekli seviye E4.** Kaynak: revizyon 13'ün dilim tablosunda DL-4 satırı (satır 1929).
- **Paket sürümü.** Kayıt, `main`'deki güncel onaylı sürüme bağlıdır (kart S-86). Revizyon 13 = `ee175d00…`, #290 ile `main`'de. `pack_approval.py check --ref origin/main` 2026-10-30'da çıkış 0 verdi.
- **Kapsam dışı (N/A).** Diğer 33 ölçüt bu kaydın kapsamı dışında.

## Sınırlar

- Ekran tarafı (MVC vekili ve formdaki seçim) WP-0230-DL7B'nindir; bu kayıt yalnız servis ucunu kabul eder.
- Koşu kaydı ölçütün anlamca doğru ölçüldüğünü kanıtlamaz. Koşu sırasında değiştirilip geri alınmış bir kaynağı da göstermez.
- Doğrulama notu N-1: PUT test gövdesi bugünkü güncelleme doğrulamasından geçmiyor; koruma yine ayırt edici. Bu not kabulü etkilemez.
