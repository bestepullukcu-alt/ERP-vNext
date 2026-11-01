# MOD-0230 — bağımsız inceleme isteği 20 (SOP §20.4 / CT-APR-02)

- **Hedef 1:** `execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md` @ sha256 `1ba986621d2e7b406ae66891e9838067a1aa08eba674cd3c25e0f8edc9e3e9f5` (revizyon 15).
- **Hedef 2:** `execution/portfolio/delivery-capability-packs/DCP-004-pvg-urgent-w3-development-block.md` @ sha256 `ac1d72618b24224976f2c2bef8b5937752247f98dc5957b950fa683350267a43`.
- **Kaynak commit:** `0e0871277`. Dal `docs/pvg/mod0230-rev15-draft`, yerel, push edilmedi.
- **Risk:** İki kart da HIGH hesapladı (istek 17'de ölçüldü). Yazar `pack_approval.py check`: paket "onaydan sonra değişti, 399 satır"; DCP-004 "onaydan sonra değişti, 63 satır".
- **Önceki sürüm ve incelemeler:** İnceleme 17, 18 ve 19 KOŞULLU, ENGEL yok. İnceleme 19 ham sonucu sha256 `59a44d01c579af45376ab850ddf132bb772ffc27e42c2c64ef8a5f6a15f46537` (alt ajan a770a9a5386367e35, Claude Opus 5.5, effort beyanı 30). Bulgu dosyaları ve satırları sahibin onayını bekliyor (SOP §20.4/4).
- **Bu sürümün nedeni:** Kalıcı depo teslimi reddi (`VERIFY-DL10.md`), sahibin "Paketi düzelt" sözü (2026-11-01, `pvg-mod0230-kapali-kayit-05.md`), tek yazar geçişi (`4f40f75ab`).
- **Yetki:** sahibin sözü "Paketi düzelt" (2026-11-01). İnceleyen biçimi: sahip kararı, Kurulum sohbetinde, Yönetici iletti: yalnız ZIP gören, geçmişsiz Claude alt ajanı.
- **İsteyen / hazırlayan / tarih:** depo sahibi (rol) · PVG Control Tower · 2026-11-01.
- **İnceleyen:** yeni alt ajan, Opus · high, yalnız ZIP. Yanıtın başına model ve effort yazılır.
- **ZIP:** `MOD-0230-review-0e0871277-since-324ec6600.zip`, sha256 `4ddc55f699f19505d96904462ac16ef62abfb3e5d8335877d4285764701a8230`, 5378473 bayt. İçinde 262 dosya ve `MANIFEST.sha256` var.
  - Yeri: `/Users/alitufanoglu/ERP-vNext-review-zips/MOD-0230-review-20/MOD-0230-review-0e0871277-since-324ec6600.zip` (depoya girmez).
- **Bulgu dosyaları:**
  - `docs/records/reviews/MOD-0230/<YYYY-AA-GG>-1ba98662.md`
  - `docs/records/reviews/DCP-004/<YYYY-AA-GG>-ac1d7261.md`
- **Prompt bloğunun SHA-256'sı:** `050e96c4290251fa1568eaec63144ef83ecf4f4351297e69152d8519d457c113` (blok içeriği, son satır sonu dahil).

## İnceleme prompt'u (inceleyen oturuma olduğu gibi iletilir)

```text
Hedef: bağımsız inceleyen — MOD-0230 module pack revizyon 15 ve DCP-004 bağımsız incelemesi 20 (SOP §20.4 / CT-APR-02). İnceleyensin; belgeleri yazan oturum değilsin.
Depoya erişimin yok: yalnız ekteki ZIP'i oku. Dosya düzenleme, kod yazma, commit yok. Tek çıktın aşağıdaki iki bulgu bloğudur.
Kaynak: ekteki ZIP (yeri ve sha256'sı son satırlarda) — önce ZIP'in sha256'sını hesaplayıp son satırla karşılaştır, yeni ve boş bir klasöre aç, sonra `shasum -a 256 -c MANIFEST.sha256` ile doğrula. Kaynak commit 0e0871277 (sahip beyanı).
Hedef 1: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ sha256 1ba986621d2e7b406ae66891e9838067a1aa08eba674cd3c25e0f8edc9e3e9f5
Hedef 2: execution/portfolio/delivery-capability-packs/DCP-004-pvg-urgent-w3-development-block.md @ sha256 ac1d72618b24224976f2c2bef8b5937752247f98dc5957b950fa683350267a43
Biri tutmuyorsa dur ve bildir.
Model · Effort: kullandığın modeli, effort'u ve oturum kimliğini yanıtın başına yaz; veremiyorsan "verilemiyor" yaz.

Görev: revizyon 15'i ve DCP-004'ün aynı değişikliğini incele. Yetkisi karar kayıtlarıdır: docs/records/decisions/2026-10/pvg-mod0230-kapali-kayit-01.md, pvg-mod0230-kapali-kayit-02.md, pvg-mod0230-kapali-kayit-03.md, pvg-mod0230-kapali-kayit-04.md, pvg-mod0230-od-pvg-1-izin-01.md ve docs/records/decisions/2026-11/pvg-mod0230-kapali-kayit-05.md. Son geçişin dayanağı docs/records/audits/2026-10/pvg-wave8-verify-01/VERIFY-DL10.md. Revizyon 14'e göre paket farkı ZIP'teki CHANGES.diff'te, öteki üç belgenin farkı docs/records/reviews/MOD-0230/rev15-diffs/ altındadır. Birlikte değişen iki belge de ZIP'te: execution/domains/pharmacovigilance/domain-config.md ve execution/domains/pharmacovigilance/work-packs/WP-0230-08-module-manifest.md.
Yeniden inceleme. Önceki tur ikisine de KOŞULLU verdi (ENGEL yok). Son geçişin kapandı iddiaları, her birini kendin ölç: AC-35 (b) yeni ölçüsü ve test adı, AC-6'ya eklenen cümle, 404 açığının yazılışı (KK-11, KK-13); revizyon onayı ile DL-11 başlangıcının sırası (KK-12); `REWORK` son durum olduğu için WP-0230-DL10 `INTEGRATED` isteyen yerlerin WP-0230-DL10-R1'e dönmesi; AC-39 (b) sabotajı (KK-14); DCP-004 §10 yazım cümlesi (KK-14); revizyon notundaki "Not changed" cümlesi. Yeni ölçünün ("bilinmeyen kayıtla aynı yanıt") kiracı yalıtımını gevşetip gevşetmediğine ayrıca bak. Bilinen, bulgu değil: (1) ZIP'teki kuyruk dosyası taslak dalındadır; WP-0230-DL10 `REWORK` ve WP-0230-DL10-R1 `PLANNED` satırları Control Tower dalındadır (CT beyanı). (2) `pvg-mod0230-kapali-kayit-05.md` KK-12 adı WP-0230-DL10 yazar; kayıt değişmez, KK-15 ve paket okuması R1'dir. (3) İnceleme 19'un F-3, F-4, F-5, F-6 düşük bulguları bilerek düzeltilmedi (ölçüm beyanı, `INTEGRATED` tanımı, G-24 "bugün", `phase1-gates.yml`). `AdminAccountSeed.cs` ve `DevAccountSeed.cs` ZIP'te yoksa araç gizli değer deseni nedeniyle reddetmiştir; bunlara dayanan iddia ölçülemez diye yazılabilir, ayrı bulgu değildir.
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
hedef: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ 1ba986621d2e7b406ae66891e9838067a1aa08eba674cd3c25e0f8edc9e3e9f5
F-1 | ENGEL|ORTA|DÜŞÜK | <bulgu; satır numarası ve dayanak dosya> | açık | <rol> | <YYYY-AA-GG>
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL>

# DCP-004 — bağımsız inceleme
hedef: execution/portfolio/delivery-capability-packs/DCP-004-pvg-urgent-w3-development-block.md @ ac1d72618b24224976f2c2bef8b5937752247f98dc5957b950fa683350267a43
F-1 | … (aynı biçim; numaralama her blokta F-1'den başlar)
Sonuç: <UYGUN ya da KOŞULLU ya da UYGUN DEĞİL>

Kurallar: seviye adını bulgu satırı dışında büyük harfle yazma; `F-` ile başlayan satır yalnız bulgu satırıdır; kapanış yalnız açık / kapandı / kabul — onaylayan; bulgu yoksa F satırı yazma ve "Sonuç: UYGUN" de; ENGEL varsa Sonuç UYGUN DEĞİL, yalnız ORTA ya da DÜŞÜK varsa KOŞULLU olur.
ZIP'in yeri: /Users/alitufanoglu/ERP-vNext-review-zips/MOD-0230-review-20/MOD-0230-review-0e0871277-since-324ec6600.zip
ZIP sha256: 4ddc55f699f19505d96904462ac16ef62abfb3e5d8335877d4285764701a8230
```
