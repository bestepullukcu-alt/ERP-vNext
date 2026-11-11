# MOD-0230 — bağımsız inceleme isteği 19 (SOP §20.4 / CT-APR-02)

- **Hedef 1:** `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md` @ sha256 `de4f0cbc7dc7a54feb3fed26c36ae0a26fa96153c8d1c4f0783585deab9b921c` (revizyon 15).
- **Hedef 2:** `execution/portfolio/delivery-capability-packs/DCP-004-pvg-urgent-w3-development-block.md` @ sha256 `4111fcffec0e8e108455de40f871de68b0af004b1fbd78a429675b4754c22d8f`.
- **Kaynak commit:** `f5f5d77a1`. Dal `docs/pvg/mod0230-rev15-draft`, yerel, push edilmedi.
- **Risk:** İki kart da HIGH hesapladı (istek 17'de ölçüldü). Kopya depoda `pack_approval.py check`: paket "onaydan sonra değişti, 380 satır"; DCP-004 "onaydan sonra değişti, 61 satır".
- **Önceki sürüm ve incelemeler:** İnceleme 17 ve 18 KOŞULLU, ENGEL yok. İnceleme 18 ham sonucu sha256 `bcfaf21b59f667cc95e25355fc49409626885ed99ef850fc3aaef6f316a8ec22` (alt ajan a9907c44d4d737111, Claude Opus 5.5, high istendi, ölçülemedi). Bulgu dosyaları ve satırları sahibin onayını bekliyor (SOP §20.4/4).
- **Bu sürümün nedeni:** Sahibin "Duzelt" sözü (2026-10-31, `pvg-mod0230-kapali-kayit-04.md`) ve tek yazar geçişi (`7c653a09a`).
- **Yetki:** sahibin sözleri "Evet", "Evet, sürsün", "Duzelt" (2026-10-31). İnceleyen biçimi: sahip kararı, Kurulum sohbetinde, Yönetici iletti: yalnız ZIP gören, geçmişsiz Claude alt ajanı.
- **İsteyen / hazırlayan / tarih:** depo sahibi (rol) · PVG Control Tower · 2026-10-31.
- **İnceleyen:** yeni alt ajan, Opus · high, yalnız ZIP. Yanıtın başına model ve effort yazılır.
- **ZIP:** `MOD-0230-review-f5f5d77a1-since-324ec6600.zip`, sha256 `cec7e41266aefc06d5278656a6bfb91522d16ec6ca0ff864902b77fb15729fba`, 5312161 bayt. İçinde 259 dosya ve `MANIFEST.sha256` var.
  - Yeri: `/Users/alitufanoglu/ERP-vNext-review-zips/MOD-0230-review-19/MOD-0230-review-f5f5d77a1-since-324ec6600.zip` (depoya girmez).
  - Araç iki tohum dosyasını gizli değer deseni nedeniyle reddetti; ZIP'e girmediler. Değerler okunmadı.
- **Bulgu dosyaları:**
  - `docs/records/reviews/MOD-0230/<YYYY-AA-GG>-de4f0cbc.md`
  - `docs/records/reviews/DCP-004/<YYYY-AA-GG>-4111fcff.md`
- **Prompt bloğunun SHA-256'sı:** `3460627d1ff0871301aa28889f169c73935a78380023d87690476e931bf2af4c` (blok içeriği, son satır sonu dahil).

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: bağımsız inceleyen — MOD-0230 module pack revizyon 15 ve DCP-004 bağımsız incelemesi 19 (SOP §20.4 / CT-APR-02). İnceleyensin; belgeleri yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki iki bulgu bloğudur.
Kaynak: ekteki ZIP (yeri ve sha256'sı son satırlarda) — önce ZIP'in sha256'sını hesaplayıp son satırla karşılaştır, yeni ve boş bir klasöre aç, sonra `shasum -a 256 -c MANIFEST.sha256` ile doğrula. Kaynak commit f5f5d77a1 (sahip beyanı).
Hedef 1: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ sha256 de4f0cbc7dc7a54feb3fed26c36ae0a26fa96153c8d1c4f0783585deab9b921c
Hedef 2: execution/portfolio/delivery-capability-packs/DCP-004-pvg-urgent-w3-development-block.md @ sha256 4111fcffec0e8e108455de40f871de68b0af004b1fbd78a429675b4754c22d8f
Biri tutmuyorsa dur ve bildir.
Model · Effort: kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; veremiyorsan "verilemiyor" yaz.

Görev: revizyon 15'i ve DCP-004'ün aynı değişikliğini incele. Yetkisi karar kayıtlarıdır: docs/records/decisions/2026-10/pvg-mod0230-kapali-kayit-01.md, pvg-mod0230-kapali-kayit-02.md, pvg-mod0230-kapali-kayit-03.md, pvg-mod0230-kapali-kayit-04.md, pvg-mod0230-od-pvg-1-izin-01.md. Revizyon 14'e göre paket farkı ZIP'teki CHANGES.diff'te, öteki üç belgenin farkı docs/records/reviews/MOD-0230/rev15-diffs/ altındadır. Birlikte değişen iki belge de ZIP'te: execution/domains/pharmacovigilance/domain-config.md ve execution/domains/pharmacovigilance/work-packs/WP-0230-08-module-manifest.md.
Yeniden inceleme. Önceki tur ikisine de KOŞULLU verdi (ENGEL yok). Kapandı iddiaları, her birini kendin ölç: sıra ölçüsü kuyruk durumu adıyla (pvg-mod0230-kapali-kayit-04.md KK-7, KK-8, KK-9); çalışan ekran yolunun döngüsü; triyaj bağlama boşluğu, sahip dilimi ve yeni ölçüt; platform yöneticisinin doğrudan abonelik çağrısının rollere etkisi; ölçüm beyanındaki tek koşu; AC-36 ve AC-37 test ayrıntıları; düzenleme ekranı boşluğu; DCP-004 §10 sınırı. `AdminAccountSeed.cs` ve `DevAccountSeed.cs` ZIP'e girmedi, araç gizli değer deseni nedeniyle reddetti; bunlara dayanan iddia ölçülemez diye yazılabilir, ayrı bulgu değildir.
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
hedef: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ de4f0cbc7dc7a54feb3fed26c36ae0a26fa96153c8d1c4f0783585deab9b921c
F-1 | ENGEL|ORTA|DÜŞÜK | <bulgu; satır numarası ve dayanak dosya> | açık | <rol> | <YYYY-AA-GG>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL>

# DCP-004 — bağımsız inceleme
hedef: execution/portfolio/delivery-capability-packs/DCP-004-pvg-urgent-w3-development-block.md @ 4111fcffec0e8e108455de40f871de68b0af004b1fbd78a429675b4754c22d8f
F-1 | … (aynı biçim; numaralama her blokta F-1'den başlar)
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL>

Kurallar: seviye adını bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; kapanış yalnız açık / kapandı / kabul — onaylayan; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; ENGEL varsa Sonuç UYGUN DEĞİL, yalnız ORTA ya da DÜŞÜK varsa KOŞULLU olur.
ZIP'in yeri: /Users/alitufanoglu/ERP-vNext-review-zips/MOD-0230-review-19/MOD-0230-review-f5f5d77a1-since-324ec6600.zip
ZIP sha256: cec7e41266aefc06d5278656a6bfb91522d16ec6ca0ff864902b77fb15729fba
```
