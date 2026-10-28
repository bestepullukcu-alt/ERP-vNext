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
acceptance_list: docs/records/audits/2026-10/pvg-dl8-acceptance-01/LIST.md@5684afed0d27254b5d502f37a9b845e2567ceb65
date: 2026-10-27
supersedes: -
verifier: PVG Control Tower (bağımsız doğrulayıcı; WP'yi yazan işçi oturumu değil)
implementer: WP-0230-DL8 işçi oturumu pvg-dl8 (integration-agent), 2026-10-23
module_pack: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md@7ec93ba98d004a123b1001b51944a29461997a91
---

# Kabul kaydı — WP-0230-DL8

**Karar: kabul edildi.** DL-8 Gateway route ailesi kabul edildi.

Kabul kayıtlı bir canlı denemeye dayanır. Deneme `scripts/run_record.py` ile `7ec93ba98` kod ağacında yapıldı. Bu ağaçta `gateway/` klasörü ve `ocelot.json` `main` ile aynıdır.

## Dayanak

- **AC-16 geçti.** `ocelot.json` tam olarak dört MOD-0230 route girdisi taşıyor. Yöntemler paketteki gibi. Arşiv, dışa aktarma, silme ya da toplu silme route'u yok. Kanıt: koşu çıktısının (`stdout.txt`) 4–10. satırları.
- **DL-8 bitiş koşulunun ikinci yarısı da sağlandı:** "Gateway üzerinden smoke çağrısı servisin sonucunu döndürür".
  - PVG servisi (5011) ve Gateway (5000) bu ağaçtan, `Development` ortamında başlatıldı.
  - Aynı GET isteği hem Gateway'e hem doğrudan servise gönderildi. İkisi de `409` döndü ve gövdeler bayt bayt aynıydı (`PVG_TENANT_CONTEXT_REQUIRED`).
  - Gateway günlüğü, isteğin `localhost:5011/api/v1/pv-case-intake-triage` adresine yönlendirildiğini gösteriyor.
  - Betik yalnız kendi başlattığı iki süreci PID ile kapattı. Ardından iki port da boştu. Komutun çıkış kodu 0.
- **Önceki koşu da kayıtlı** (`run-01`, 4fe25fa29). Aynı ölçümü geçti, ama son kontrolde Gateway süreci kapanmadı: süreç alt kabukta başladığı için betik onu durduramadı. Süreç PID 38068 ile elle kapatıldı. Betik `exec` ile düzeltildi (7ec93ba98) ve koşu `run-02` olarak tekrarlandı.
- **Gerekli seviye E3.** Kaynak, paketin teslim dilimleri tablosundaki DL-8 satırıdır. `docs/records/decisions/2026-10/pvg-mod0230-kanit-seviyesi-01.md` kararı bu kabulün yolunu önceden tarif etmişti.
- **Diğer 32 ölçüt bu kaydın kapsamında değil (N/A).** Her biri kendi diliminin iş paketi kaydında ölçülür.
- **Yer.** Kayıt ve liste ilk olarak `docs/records/acceptance-reports/` altına yazıldı. Ancak kabul aracı kanıt belgelerini yalnız `docs/records/audits/` altında kabul ediyor. İkisi gönderilmeden önce buraya taşındı (5684afed0).

## Sınırlar

- **Smoke çağrısı kiracısız bir GET'tir.** Gateway, Development ortamında geliştirme kiracısı atlamasını uyguladı; bu mevcut bir davranıştır. Kiracı ve token ile uçtan uca akış DL-4 kısım B'de (AC-5) ölçülür.
- **Kod ağacı `main`'de değil.** `7ec93ba98` CT dalındadır. Sahibin kararıyla iş şimdilik yalnız bu bilgisayarda kalıyor; PR bütün dalgalar bittikten sonra açılacak. Ölçülen Gateway dosyaları `main`'dekilerle (`500c122ba`) aynıdır.
- **Koşu kaydının kanıtlamadıkları.** Ölçütün anlamca doğru ölçüldüğünü göstermez; bunu bağımsız inceleme gösterir. Koşu sırasında değiştirilip geri alınan bir dosyayı da yakalamaz: yalnız koşunun öncesi ve sonrası karşılaştırılır.
