---
format: rep-001-v2
module: MOD-0230
work_package: WP-0230-DL8
queue: pvg
evidence_level: E3
required_evidence_level: E3
required_evidence_source: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md@5f0c21add9da9635c9be4e5756f94b42b3ef322f:1933
decision: accepted
completion: WP Done
conditions: -
commit: 5f0c21add9da9635c9be4e5756f94b42b3ef322f
evidence: docs/records/audits/2026-10/pvg-dl8-live-01/run-05/run-record-v1.json@9934e311e36f872b6d1e6d56f6f75317e7b2dcc5
acceptance_list: docs/records/audits/2026-10/pvg-dl8-acceptance-04/LIST.md@0aff562534876a31a36207f19070c0f226869548
date: 2026-10-30
supersedes: docs/records/audits/2026-10/pvg-dl8-acceptance-03/RECORD.md
verifier: PVG Control Tower (bağımsız doğrulayıcı; WP'yi yazan işçi oturumu değil)
implementer: WP-0230-DL8 işçi oturumu pvg-dl8 (integration-agent), 2026-10-23
module_pack: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md@5f0c21add9da9635c9be4e5756f94b42b3ef322f
---

# Kabul kaydı — WP-0230-DL8 (04, paket revizyon 13)

**Karar: kabul edildi.** Bu kayıt `docs/records/audits/2026-10/pvg-dl8-acceptance-03/RECORD.md`'nin yerine geçer.

## Neden yeni kayıt

Kayıt 03, paket revizyon 12'ye (`a51f2349`) bağlıydı. #290 birleşince (Beste'nin merge commit'i `496ab5659`) `main`'deki onaylı sürüm revizyon 13 (`ee175d00`) oldu. Kart S-86 ve aracın S-5 kuralı, kabulün `main`'deki güncel onaylı sürüme bağlanmasını istiyor. Bu yüzden kanıt koşusu yeni sürümü taşıyan commit'te yeniden alındı.

AC-16'nın metni değişmedi. Revizyon 13 AC-2, AC-14, AC-15, AC-20, AC-32'yi değiştirdi ve AC-34'ü ekledi; DL-8'e dokunmadı.

## Dayanak

- **AC-16 geçti.**
  - `ocelot.json` tam olarak dört MOD-0230 route girdisi taşıyor.
  - Yasak route (arşiv, dışa aktarma, silme ya da toplu silme) yok.
  - Kanıt: `run-05` koşusunun `stdout.txt` dosyası, satır 4–10.
- **DL-8 bitiş koşulunun ikinci yarısı da geçti.**
  - Aynı GET isteği hem Gateway'e hem doğrudan servise gönderildi.
  - İkisi de **401** döndü, gövdeler bayt bayt aynı (stdout satır 32).
  - Smoke'un ölçtüğü şey, yani Gateway'in servisin cevabını aynen döndürmesi, kayıt 03'tekiyle aynı.
- **Kod ağacı ve kapanış.**
  - Kod ağacı `5f0c21add` (`main` `d27f0272d` CT dalına alındıktan sonra).
  - Bu ağaçta `gateway/` ve `services/` `main` (`d27f0272d`) ile aynı; `gateway/` ve `services/Diten.PvgService` kaydın yazıldığı günkü `main` ile de aynı.
  - Betik yalnız kendi başlattığı süreçleri kapattı; sonra iki port da boştu.
  - Komutun çıkışı 0; `run_record.py verify` geçerli.
- **Gerekli seviye E3.** Kaynak: revizyon 13'ün dilim tablosunda DL-8 satırı (satır 1933).
- **Diğer 33 ölçüt bu kaydın kapsamı dışında (N/A).**

## Sınırlar

- Smoke çağrısı token'sızdır. Kimlik doğrulamalı uçtan uca akış AC-5'in (WP-0230-DL4B) testlerinde ölçülüyor.
- Koşu kaydı, ölçütün anlamca doğru ölçüldüğünü kanıtlamaz. Koşu sırasında değiştirilip geri alınmış bir kaynağı da göstermez.
