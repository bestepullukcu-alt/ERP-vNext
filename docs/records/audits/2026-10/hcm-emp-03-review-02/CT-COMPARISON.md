# HCM-EMP-03 — Codex incelemesi 02'nin CT karşılaştırması (SOP §20.4/4)

> Control Tower kaydıdır; onay, paket değişikliği ya da kod izni değildir.

## Künye

- İnceleme: Codex, inceleme 02; kaynak GitHub dalı `chore/ct/hcm-employee-ct-01` @ 54b7fb5a0f67524706ef06ceea9cd5de18e6e54e
  (sahip sözü `push-54b7fb5`). Model, effort, oturum: "verilemiyor" (inceleyen beyanı). Sahip sohbete yapıştırdı, 2026-10-29.
- Hedef: MOD-0251 paketi revizyon 2 @ sha256 `47968562cc5b5fae9dea5bc8ad7ef2926a1f77fe9ecb07d80dd94a9028f950bc`.
- Sonuç: **UYGUN DEĞİL.** İnceleme 01'in 17 bulgusundan 13'ü kapandı; açık kalan 4 (F-4, F-9 ENGEL; F-11, F-13 ORTA) ve
  yeni 4 (F-18, F-19, F-21 ENGEL; F-20 ORTA). Açık ENGEL: 5.

## Biçim çevirisi (içerik değişmedi)

İnceleme isteği 02, önceki bulguların `F-n@260d1151 | ENGEL | … | kapandı` biçiminde yazılmasını istemişti. Bu **CT'nin
hatasıydı**: `scripts/pack_approval.py` `parse_findings` önceki kayda başvuruyu yalnız ENGEL'in `kabul — onaylayan`
kapanışında kabul eder ve ENGEL için `kapandı` değerini reddeder (ölçüm 2026-10-29: 13 satırda hata). Bulgu dosyası
`docs/records/reviews/MOD-0251/2026-10-29-47968562.md` bu yüzden S-59 biçimine çevrildi:

- Kapanan 13 önceki bulgu (F-1, F-2, F-3, F-5, F-6, F-7, F-8, F-10, F-12, F-14, F-15, F-16, F-17) tek bir düz
  "Kapananlar" satırında.
- Açık kalan 4 önceki bulgu bu sürümün bulgusu olarak yeniden numaralandı, metin aynen: F-22 ← F-4, F-23 ← F-9,
  F-24 ← F-11, F-25 ← F-13 (her satır "(inceleme 01 F-n bu sürümde açık kalıyor)" diye başlar).
- Yeni bulgular F-18…F-21 aynen. Ayrıştırıcı çeviriden sonra 8 bulgu okur, hata yok.
- Ham metin değiştirilmedi: `docs/records/reviews/MOD-0251/draft-codex-02-raw.txt` (commit'e girmez).
- Sonraki inceleme isteklerinde önceki bulgular için doğru biçim istenecek.

## Bulgu bulgu karşılaştırma

| Bulgu | CT kararı | CT'nin kendi ölçümü |
|---|---|---|
| F-22 (01/F-4) taslak denetimi DL-4'e ertelenmiş | **Kabul** | Paket:1518 A12'yi "DL-4"e, 1567 ve 1698 denetim bağlantısını DL-4'e veriyor. Ek ölçüm (S-84 "engel hâlâ geçerli mi"): `audit-trail-standard.md` §5 K4 HCM dahil on serviste yeni yazma komutunu "ortak iletici gelene kadar" engelli sayıyor; ortak iletici `Diten.BuildingBlocks.CentralAudit` olarak main'de (528cd415d, 2026-10-14, BL-501; son 111c60c46, 2026-10-23) ve InventoryService ile SupplyChainService kullanıyor. Kural metni güncellenmemiş. Taslak yazımlarının denetimi DL-2'de bu iletici üzerinden (yol `b`) bağlanabilir — K4'ün HCM için kalktığını kural sahibinin teyit etmesi gerekir. |
| F-23 (01/F-9) B07 çelişkisi, D03 "none" | **Kabul** | D03: `frontend/Diten.Web/tests/hcm-employee-registry.test.js:105-111` dışa aktarma düğmelerinin yokluğunu ölçüyor; "none" yanlış. B07: paket taslak oluşturmayı engelliyor, uç nokta haritası eksik referansla taslağa izin veriyor — revizyon 3'te tek anlam seçilmeli. |
| F-24 (01/F-11) MOD-0299 checklist | Kabul | — |
| F-25 (01/F-13) izin tohumu kararı | Kabul | Koruma da daraltma da sahip kararı kaydı istemeli. |
| F-18 Audited Events tablosu yok | **Kabul** | `.antigravity/rules/module-pack-standard.md` §10.1 (AUD-001) her yazma komutunu tabloda tam bir kez ister; pakette yok. |
| F-19 `data_mode` yok | **Kabul** | `module-pack-standard.md` künye tablosu: DataTable modüllerinde zorunlu; varsayılan `server`. |
| F-20 detay okumanın kabulü yok | Kabul | — |
| F-21 DL-6 → DL-5 bağımlılığı | **Kabul** | Paket:1720 DL-6 ön koşulu yalnız "DL-4 accepted"; `updatedSince` okuması DL-5'te. |

İtiraz: yok.

## Revizyon 3 için öneri (öneri — onay değil)

1. F-22: taslak yazımlarının merkezi denetimi DL-2'ye taşınır (yol `b`, ortak iletici); K4'ün HCM için kalktığı kural
   sahibine sorulur (açık karar).
2. F-18: "Audited Events" tablosu — bugünkü yazma komutları (taslak oluştur, güncelle, referans doğrula, gözden geçir;
   kapalı onaya gönder ve iş akışı kararı; smoke fixture) ve DL-4…DL-6'nın gelecek komutları, her biri yol, alanlar,
   istisna sınıfı, K2 durumu ve testle.
3. F-19: künyeye `data_mode: server` (rule varsayılanı; çalışan listesi sınırsız).
4. F-23: B07 tek anlam — öneri: taslak eksik referansla oluşturulabilir, gözden geçirme ve onaya gönderme geçerli kişi
   ister; D03 satırı mevcut kısmi testi gösterir.
5. F-20: DL-3'e detay okuma kabulü (alan projeksiyonu, yetki ve maskeleme negatifleri, E4) ya da ayrı dilim.
6. F-21: DL-6 ön koşuluna "DL-5 accepted".
7. F-24, F-25: checklist ve DL-2 ön koşul metni.
