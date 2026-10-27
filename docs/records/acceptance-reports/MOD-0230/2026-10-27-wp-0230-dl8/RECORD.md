---
format: rep-001-v2
module: MOD-0230
work_package: WP-0230-DL8
queue: pvg
evidence_level: E3
required_evidence_level: E3
required_evidence_source: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md@7ec93ba98d004a123b1001b51944a29461997a91:1794
decision: accepted
completion: WP Done
conditions: -
commit: 7ec93ba98d004a123b1001b51944a29461997a91
evidence: docs/records/audits/2026-10/pvg-dl8-live-01/run-02/run-record-v1.json@77c998bdc0a4e9316f1b7fa760cd7eeb94d8c7a8
acceptance_list: docs/records/acceptance-reports/MOD-0230/2026-10-27-wp-0230-dl8/LIST.md@f7c6bdf96925b3c8c1966ccbdcdfea292300dc1e
date: 2026-10-27
supersedes: -
verifier: PVG Control Tower (bağımsız doğrulayıcı; WP'yi yazan işçi oturumu değil)
implementer: WP-0230-DL8 işçi oturumu pvg-dl8 (integration-agent), 2026-10-23
module_pack: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md@7ec93ba98d004a123b1001b51944a29461997a91
---

# Kabul kaydı — WP-0230-DL8

**Karar: kabul edildi.** DL-8 Gateway route ailesi kabul edildi.

Kabul kayıtlı bir canlı denemeye dayanır. Deneme `scripts/run_record.py` ile, `7ec93ba98` kod ağacında yapıldı. Bu ağaçta `gateway/` ve `ocelot.json` `main` ile aynıdır.

## Dayanak

- **AC-16 geçti.** `ocelot.json` tam olarak dört MOD-0230 route girdisi taşıyor ve yöntemleri paketteki gibi. Arşiv, dışa aktarma, silme ya da toplu silme route'u yok. Kanıt: koşu çıktısı `stdout.txt` satır 4–10.
- **DL-8 bitiş koşulunun ikinci yarısı da sağlandı:** "Gateway üzerinden smoke çağrısı servisin sonucunu döndürür".
  - PVG servisi (5011) ve Gateway (5000) bu ağaçtan, `Development` ortamında başlatıldı.
  - Aynı GET isteği Gateway'e ve servise gönderildi. İkisi de `409` döndü; gövde bayt bayt aynıydı (`PVG_TENANT_CONTEXT_REQUIRED`).
  - Gateway günlüğü isteğin `localhost:5011/api/v1/pv-case-intake-triage`'e yönlendirildiğini gösteriyor.
  - Betik yalnız kendi başlattığı iki süreci PID ile kapattı. Sonra iki port da boştu. Komutun çıkış kodu 0.
- **Önceki koşu (`run-01`, 4fe25fa29) de kayıtlı.** Aynı ölçümü geçti, ama son kontrolde Gateway süreci kapanmadı (alt kabuk sorunu). Süreç PID 38068 ile elle kapatıldı. Betik `exec` ile düzeltildi (7ec93ba98) ve koşu `run-02` olarak tekrarlandı.
- **Gerekli seviye E3.** Kaynak: paketin teslim dilimleri tablosundaki DL-8 satırı. Karar `docs/records/decisions/2026-10/pvg-mod0230-kanit-seviyesi-01.md` bu kabulün yolunu önceden yazmıştı.
- **Diğer 32 ölçüt bu kaydın kapsamında değil (N/A).** Her biri kendi diliminin iş paketi kaydında ölçülür.

## Sınırlar

- **Smoke kiracısız bir GET'tir.** Gateway, Development ortamında geliştirme kiracısı atlamasını uyguladı; bu mevcut davranıştır. Kiracı ve token ile uçtan uca akış DL-4 kısım B (AC-5) ile ölçülür.
- **Kod ağacı `main`'de değil.** `7ec93ba98` CT dalındadır; sahibin kararıyla iş yalnız bu bilgisayarda kalıyor, PR bütün dalgalardan sonra açılacak. Ölçülen Gateway dosyaları `main`'deki (`500c122ba`) ile aynıdır.
- **Koşu kaydının ölçmedikleri.** Ölçütün doğru ölçüldüğünü bu kayıt değil, bağımsız inceleme gösterir. Koşu sırasında bir dosyanın değiştirilip geri konmadığını da göstermez; yalnız koşunun öncesi ve sonrası karşılaştırılır.
