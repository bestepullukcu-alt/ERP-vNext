# MOD-0230 — bağımsız inceleme isteği 17 (SOP §20.4 / CT-APR-02)

- **Hedef 1:** `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md` @ sha256 `280151294133b39ffb904cdb0ec4f00f63bc9a6720617b5778859810e8b28ef0` (revizyon 15).
- **Hedef 2:** `execution/portfolio/delivery-capability-packs/DCP-004-pvg-urgent-w3-development-block.md` @ sha256 `a64841559d8aef280c41c2e99301e61261cc2b1f49c3de2ef26fc213d6adc7db`.
- **Kaynak commit:** `2bfe93f36`. Dal `docs/pvg/mod0230-rev15-draft`, yerel, push edilmedi.
- **Risk:** İki kart da HIGH hesaplıyor (paket 5ea1a49b…, DCP-004 c8605303…). Kopya depoda (8b58b96b4 üstüne dört dosya) pack_approval.py check sonuçları: paket 'onaydan sonra değişti, 346 satır'; DCP-004 'onaydan sonra değişti, 55 satır'. Dalın kendi geçmişinde yerel merge c89fda3c4 bulunduğu için check --ref 2bfe93f36, o commit push edilene kadar onay dosyası şikâyeti verir.
- **Önceki sürüm ve incelemeler:** Revizyon 14'ün incelemesi 16 (`REVIEW-REQUEST-16.md`, hedefler `61c7902f` ve `3dfa4c47`); onay isteği henüz birleşmedi, bu yüzden revizyon 15 yerel olarak onun üstünde duruyor.
- **Bu sürümün nedeni:** Sahibin kapalı katalog kaydı kararı. Ön inceleme (S-93, yeni alt ajan, sha256 `2f5da9fcdeaae2ce1154383ea34355676219cfe06ca5ef81aa53a1baad306036`, commit `3aa7ae967`) engel vermedi; bulgular tek yazar geçişinde işlendi (`00e0a8a1b`), yeni ön inceleme turu açılmadı. Ardından sahibin ikinci sözü ve önkoşul düzeltmesi işlendi (`ab6d673e1`, `2bfe93f36`). Bu son iki değişikliği hiçbir inceleyen okumadı.
- **Yetki:** sahibin sözleri "Evet" ve "Evet, sürsün" (2026-10-31; `docs/records/decisions/2026-10/pvg-mod0230-kapali-kayit-01.md`, `-03`). Bu sözler paket içeriğini onaylamaz.
- **İsteyen / hazırlayan / tarih:** depo sahibi (rol) · PVG Control Tower · 2026-10-31.
- **İnceleyen:** yeni bir Codex oturumu, effort high. Yanıtın başına model, effort ve oturum kimliği yazılır.
- **ZIP:** `MOD-0230-review-2bfe93f36-since-324ec6600.zip`, sha256 `cd6221ae8d21fb264bc4626bcb38ffc920fc98690598dafeca58dd853a4145fd`, 4795731 bayt. İçinde 173 dosya ve `MANIFEST.sha256` var.
  - İnceleme isteği dosyası ZIP'te değil (S-98).
  - Yeri: `/Users/alitufanoglu/ERP-vNext-review-zips/MOD-0230-review-17/MOD-0230-review-2bfe93f36-since-324ec6600.zip` (depoya girmez).
  - E-posta taraması alan adları: example.test (6), x.invalid (5), gmgroup.ch (1), example.co.uk (1), example.org (1), example.com (1), diten.test (1), di10.test (1), b.test (1), x.test (1). Hepsi depoda zaten var olan dosyalardan gelir.
- **Bulgu dosyaları:**
  - `docs/records/reviews/MOD-0230/<YYYY-AA-GG>-28015129.md`
  - `docs/records/reviews/DCP-004/<YYYY-AA-GG>-a6484155.md`
- **Prompt bloğunun SHA-256'sı:** `26a3e4caf0a46497be05abbf4ffa4593a95b6c016605c0b48a6b4162d76ddf62` (blok içeriği, son satır sonu dahil).

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: Codex — MOD-0230 module pack revizyon 15 ve DCP-004 bağımsız incelemesi 17 (SOP §20.4 / CT-APR-02). İnceleyensin; belgeleri yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki iki bulgu bloğudur.
Kaynak: ekteki ZIP (yeri ve sha256'sı son satırlarda) — önce ZIP'in sha256'sını hesaplayıp son satırla karşılaştır, yeni ve boş bir klasöre aç, sonra `shasum -a 256 -c MANIFEST.sha256` ile doğrula. Kaynak commit 2bfe93f36 (sahip beyanı).
Hedef 1: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ sha256 280151294133b39ffb904cdb0ec4f00f63bc9a6720617b5778859810e8b28ef0
Hedef 2: execution/portfolio/delivery-capability-packs/DCP-004-pvg-urgent-w3-development-block.md @ sha256 a64841559d8aef280c41c2e99301e61261cc2b1f49c3de2ef26fc213d6adc7db
Biri tutmuyorsa dur ve bildir.
Model · Effort: kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; veremiyorsan "verilemiyor" yaz.

Görev: revizyon 15'i ve DCP-004'ün aynı değişikliğini incele. Yetkisi karar kayıtlarıdır: docs/records/decisions/2026-10/pvg-mod0230-kapali-kayit-01.md, pvg-mod0230-kapali-kayit-02.md, pvg-mod0230-kapali-kayit-03.md, pvg-mod0230-od-pvg-1-izin-01.md. Revizyon 14'e göre fark ZIP'teki CHANGES.diff'tedir. Birlikte değişen iki belge de ZIP'te: execution/domains/pharmacovigilance/domain-config.md ve execution/domains/pharmacovigilance/work-packs/WP-0230-08-module-manifest.md.
Şunları ölç:
1. Kayıtların kararı metne doğru ve eksiksiz geçmiş mi; kayıtların vermediği bir yetki açılmış mı.
2. Belgeler kendi içinde ve birbirleriyle çelişiyor mu; engelleyen bir satır uzlaştırılmadan kalmış mı.
3. Kod ve kural hakkındaki iddialar ZIP'teki kaynakla tutuyor mu.
4. Yeni kabul ölçütleri ölçülebilir mi; her sabotaj derlenir, adlı testi kırmızıya çevirir ve yalnız dilimin izinli yollarına dokunur mu; canlı ölçüm adımları gerçekten yapılabilir mi.
5. Kapalı kalması gerekenler (canlı kullanım kapısı, servisin reddeden kapıları, MOD-0231, MOD-0232, MOD-0234, seed, menü) kapalı kalıyor mu.
6. Dilim tablosu, kapsam ve önkoşullar tutarlı mı; bir işçi yalnız bu metinle işi yapabilir mi.
ZIP'te eksik girdi varsa (atıf verilen ama ZIP'te olmayan dosya) bunu ayrı bir bulgu olarak yaz. Revizyon 15'in değiştirmediği eski atıflar bulgu değildir.
Kod koşamazsın; iddiaları ZIP'teki metne ve kaynaklara karşı statik olarak kendin ölç.

Çıktı (başka bir şey yazma; kişi adı ve e-posta yok, rol yaz). İki blok, bu sırayla:
# MOD-0230 — bağımsız inceleme
hedef: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ 280151294133b39ffb904cdb0ec4f00f63bc9a6720617b5778859810e8b28ef0
F-1 | ENGEL|ORTA|DÜŞÜK | <bulgu; satır numarası ve dayanak dosya> | açık | <rol> | <YYYY-AA-GG>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL>

# DCP-004 — bağımsız inceleme
hedef: execution/portfolio/delivery-capability-packs/DCP-004-pvg-urgent-w3-development-block.md @ a64841559d8aef280c41c2e99301e61261cc2b1f49c3de2ef26fc213d6adc7db
F-1 | … (aynı biçim; numaralama her blokta F-1'den başlar)
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL>

Kurallar: seviye adını bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; kapanış yalnız açık / kapandı / kabul — onaylayan; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; ENGEL varsa Sonuç UYGUN DEĞİL, yalnız ORTA ya da DÜŞÜK varsa KOŞULLU olur.
ZIP'in yeri: /Users/alitufanoglu/ERP-vNext-review-zips/MOD-0230-review-17/MOD-0230-review-2bfe93f36-since-324ec6600.zip
ZIP sha256: cd6221ae8d21fb264bc4626bcb38ffc920fc98690598dafeca58dd853a4145fd
```
