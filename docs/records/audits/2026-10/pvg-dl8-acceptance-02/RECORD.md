---
format: rep-001-v2
module: MOD-0230
work_package: WP-0230-DL8
queue: pvg
evidence_level: E3
required_evidence_level: E3
required_evidence_source: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md@40d9d8effa64114b8bb387e7c4341c111242de81:1817
decision: accepted
completion: WP Done
conditions: -
commit: 40d9d8effa64114b8bb387e7c4341c111242de81
evidence: docs/records/audits/2026-10/pvg-dl8-live-01/run-03/run-record-v1.json@98bee103b52f52911f868f6e7d3fe3858bffdb88
acceptance_list: docs/records/audits/2026-10/pvg-dl8-acceptance-02/LIST.md@eb77d50578ac31ce0186f74ebd0f966bbdba4efe
date: 2026-10-27
supersedes: docs/records/audits/2026-10/pvg-dl8-acceptance-01/RECORD.md
verifier: PVG Control Tower (bağımsız doğrulayıcı; WP'yi yazan işçi oturumu değil)
implementer: WP-0230-DL8 işçi oturumu pvg-dl8 (integration-agent), 2026-10-23
module_pack: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md@40d9d8effa64114b8bb387e7c4341c111242de81
---

# Kabul kaydı — WP-0230-DL8 (02, paket revizyon 11)

**Karar: kabul edildi.** Bu kayıt `docs/records/audits/2026-10/pvg-dl8-acceptance-01/RECORD.md`'nin yerine geçer.

## Neden yeni kayıt

İlk kayıt paket revizyon 10'a (`c236cb87`) bağlıydı. PR #261 birleşince `main`'deki onaylı sürüm revizyon 11 (`918dd660`) oldu.

Kabul aracının S-5 denetimi bağlı paket sürümünü `main`'deki onaylı sürüme karşı okuyor. Bu yüzden ilk kayıt "kabul edilen paket sürümü main'de onaylı değil" engeline düştü. Bunu `ct_queue.py validate` ölçtü.

AC-16'nın metni iki sürümde aynı. Revizyon 11 yalnız AC-2, AC-4, AC-5 ve AC-10'un ön koşullarını değiştirdi. Kabul, revizyon 11 kod ağacında yeni bir koşu kaydıyla tekrarlandı.

## Dayanak

- **AC-16 geçti.**
  - `ocelot.json` tam olarak dört MOD-0230 route girdisi taşıyor; yöntemler paketteki gibi.
  - Arşiv, dışa aktarma, silme ya da toplu silme route'u yok.
  - Kanıt: `run-03` koşusunun `stdout.txt` dosyası, satır 4–10.
- **DL-8 bitiş koşulunun ikinci yarısı** ("Gateway üzerinden smoke çağrısı servisin sonucunu döndürür") da sağlandı.
  - Aynı GET isteği Gateway'e ve doğrudan servise gönderildi. İkisi de `409` döndü; gövdeler bayt bayt aynı.
  - Betik yalnız kendi başlattığı iki süreci kapattı. Sonra iki port da boştu.
  - Komutun çıkış kodu 0. Kod ağacı `40d9d8eff`; bu ağaçta `gateway/` ve `services/` `main` `3a607547e` ile aynı.
- **Gerekli seviye E3.** Kaynak, revizyon 11'in dilim tablosundaki DL-8 satırı (satır 1817).
- **Diğer 32 ölçüt bu kaydın kapsamı dışında (N/A).** Her biri kendi diliminin iş paketi kaydında ölçülür.

## Sınırlar

- Smoke kiracısız bir GET'tir. Gateway Development ortamında geliştirme kiracısı bypass'ını uyguladı.
- Koşu kaydı, ölçütün anlamca doğru ölçüldüğünü göstermez. Koşu sırasında değiştirilip geri alınan bir kaynağı da göstermez.
- **Kurulum bulgusu:** Bir paket revizyonu onaylanınca, önceki sürüme bağlı kabul kayıtları `ct_queue.py validate` içinde engele düşüyor. Ölçütü değişmemiş olanlar da bunun dışında kalmıyor. Bulgu kurulum listesine bildirildi.
