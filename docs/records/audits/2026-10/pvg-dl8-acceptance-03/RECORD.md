---
format: rep-001-v2
module: MOD-0230
work_package: WP-0230-DL8
queue: pvg
evidence_level: E3
required_evidence_level: E3
required_evidence_source: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md@74d95ac38715d6cdb9f24fd5bc07ae6f33f8a127:1847
decision: accepted
completion: WP Done
conditions: -
commit: 74d95ac38715d6cdb9f24fd5bc07ae6f33f8a127
evidence: docs/records/audits/2026-10/pvg-dl8-live-01/run-04/run-record-v1.json@4752fbee103ef135c2d02ef40b268bce163f4752
acceptance_list: docs/records/audits/2026-10/pvg-dl8-acceptance-03/LIST.md@22d3ec2e30dd6d86e0c562a04bea919077e18381
date: 2026-10-28
supersedes: docs/records/audits/2026-10/pvg-dl8-acceptance-02/RECORD.md
verifier: PVG Control Tower (bağımsız doğrulayıcı; WP'yi yazan işçi oturumu değil)
implementer: WP-0230-DL8 işçi oturumu pvg-dl8 (integration-agent), 2026-10-23
module_pack: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md@74d95ac38715d6cdb9f24fd5bc07ae6f33f8a127
---

# Kabul kaydı — WP-0230-DL8 (03, paket revizyon 12)

**Karar: kabul edildi.** Bu kayıt `docs/records/audits/2026-10/pvg-dl8-acceptance-02/RECORD.md`'nin yerine geçer.

## Neden yeni kayıt

Kayıt 02, paket revizyon 11'e (`918dd660`) bağlıydı. #267 birleşince `main`'deki onaylı sürüm revizyon 12 (`a51f2349`) oldu. Kart S-86 ve aracın S-5 kuralı, kabulün `main`'deki güncel onaylı sürüme bağlanmasını istiyor. Bu yüzden kanıt koşusu yeni sürümü taşıyan commit'te yeniden alındı.

AC-16'nın metni değişmedi. Revizyon 12 yalnız AC-2, AC-5 (ön koşul) ve AC-20'yi değiştirdi.

## Dayanak

- **AC-16 geçti.**
  - `ocelot.json` tam olarak dört MOD-0230 route girdisi taşıyor.
  - Yasak route (arşiv, dışa aktarma, silme ya da toplu silme) yok.
  - Kanıt: `run-04` koşusunun `stdout.txt` dosyası, satır 4–10.
- **DL-8 bitiş koşulunun ikinci yarısı da geçti.**
  - Aynı GET isteği hem Gateway'e hem doğrudan servise gönderildi.
  - İkisi de **401** döndü, gövdeler bayt bayt aynı.
  - Önceki koşularda (run-01…03) bu cevap 409'du. Fark WP-0230-DL4B'den geliyor: servis artık geçerli bir JWT istiyor ve token'sız istek kimlik doğrulama ara katmanında 401 alıyor.
  - Smoke'un ölçtüğü şey, yani Gateway'in servisin cevabını aynen döndürmesi, değişmedi.
- **Kod ağacı ve kapanış.**
  - Kod ağacı `74d95ac38`.
  - Bu ağaçta `gateway/` ve `services/` `main` (`7495c4d27`) ile aynı.
  - Betik yalnız kendi başlattığı süreçleri kapattı; sonra iki port da boştu.
  - Komutun çıkışı 0.
- **Gerekli seviye E3.** Kaynak: revizyon 12'nin dilim tablosunda DL-8 satırı (satır 1847).
- **Diğer 32 ölçüt bu kaydın kapsamı dışında (N/A).**

## Sınırlar

- Smoke çağrısı token'sızdır. Kimlik doğrulamalı uçtan uca akış AC-5'in (WP-0230-DL4B) testlerinde ölçülüyor.
- Koşu kaydı, ölçütün anlamca doğru ölçüldüğünü kanıtlamaz. Koşu sırasında değiştirilip geri alınmış bir kaynağı da göstermez.
