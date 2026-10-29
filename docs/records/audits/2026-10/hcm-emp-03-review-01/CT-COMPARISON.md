# HCM-EMP-03 — Codex incelemesi 01'in CT karşılaştırması (SOP §20.4/4)

> Control Tower kaydıdır; onay, paket değişikliği ya da kod izni değildir. CT sonucu körü körüne kopyalamaz: her bulgu
> paketle ve kodla karşılaştırıldı.

## Künye

- İnceleme: Codex, inceleme isteği 01 (`docs/records/reviews/MOD-0251/REVIEW-REQUEST-01.md`); kaynak GitHub dalı
  `chore/ct/hcm-employee-ct-01` @ 0b52c12dc2805a26bfca9a80f7cd00195fa9a47f (sahip sözü `push-0b52c12`). Model, effort ve
  oturum: "verilemiyor" (inceleyen beyanı).
- Hedef: MOD-0251 paketi @ sha256 `edb2c3215e731fa596c6629b8e75259c48ef5ed124e1de3f1bebc60cb506f5ea`.
- Sonuç: **UYGUN DEĞİL** — 10 ENGEL (F-1…F-10), 6 ORTA (F-11…F-16), 1 DÜŞÜK (F-17).
- Ham metin: `docs/records/reviews/MOD-0251/draft-codex-01-raw.txt` (değiştirilmedi); bulgu dosyası
  `docs/records/reviews/MOD-0251/2026-10-27-edb2c321.md` (biçim: yalnız bağlantılar depo-göreli yola çevrildi, başlık sırası
  POLICY-REVIEW-START örneğine göre).
- CT doğrulaması 2026-10-27, dal HEAD 0b52c12dc (kod yolları origin/main a974171fd ile aynı).

## Bulgu bulgu karşılaştırma

| Bulgu | CT kararı | CT'nin kendi ölçümü |
|---|---|---|
| F-1 sağlayıcının olumsuz sonucu başarılı sayılıyor | **Kabul — CT denetiminde kaçırıldı** | `GatewayReferenceValidationClient.cs:124-127` her 2xx yanıtı `IsReferenceable=true` yapıyor; `ValidatePersonReferencesQueryHandler.cs:34-40` bulunamayan kişi için `referenceable=false`'u 200 ile döndürüyor. Bulunamayan ya da başka kiracıdaki kişi "geçerli" görünür. P2'nin çekirdek iddiası (GWT-A03) bozuk. |
| F-2 doğrulanan kimlikler taslağa bağlı değil | **Kabul — kaçırıldı** | `ValidateDraftReferencesHandler.cs:53-58` istek gövdesindeki kimlikleri doğruluyor, taslaktakileri değil; `EmployeeDraftHandlerHelpers.cs:27-30` yalnız `CanReview` bayrağına bakıyor. |
| F-3 taslakta kişisel veri koruması | **Kabul — kaçırıldı** | `EmployeeDraftMapper.cs:62-70` yalnız üst düzey anahtarları maskeliyor. Şifreleme ve özyinelemeli koruma yok (ajan okumasıyla tutarlı; alt anahtar örneği ölçülmedi). |
| F-4 audit yalnız log | Kabul — CT denetimi madde 8 ile aynı | Ek: aktör ve korelasyonun null gitmesi (`CreateEmployeeDraftHandler.cs:55`) CT'de yoktu; kabul. |
| F-5 liste/detay kapsam dışı | Kabul — CT madde 4 ve 11 ile aynı | — |
| F-6 Gateway ve fixture | **Kabul, CT notu düzeltilir** | CT notu "koruma yalnız ortam adı" demişti; fixture uçları JWT + iki izin de istiyor (alt ajan tablosunda vardı, nota eksik geçti). Yeniden yazma ve silineni canlandırma bulgusu yeni; kabul. |
| F-7 kabul kanıtı yok | Kabul, ifade daraltılır | CT notu "depoya hiç girmemiş" dedi; ölçüm bu klonun ref'leriyle sınırlıydı. Doğru ifade: "incelenen ref'lerde yok". |
| F-8 Teslim dilimleri yok | Kabul | CT isteğinde "bilinen eksik" diye yazılmıştı. Codex'in DL-1…DL-6 + kanıt seviyesi önerisi HCM-EMP-05 paket revizyonuna girdi olarak alınır (öneri — onay değil). |
| F-9 kriter→test eşlemesi yok | Kabul | CT notu yalnız test sayısını eskimiş saydı; kriter bazlı eşleme yoktu. |
| F-10 aktifleştirme ön koşulları açık | Kabul, kapsam notu | Engel **yürütme ve onay** içindir. HCM-EMP-05 paket taslağı açık kararları (K-1…K-9) yazarak hazırlanabilir; taslak onaya ancak kararlar kapanınca gider. |
| F-11 MOD-0299 gerekçesi eskimiş | Kabul | `module-id-registry.md` MOD-0299 satırı ölçülmedi; Codex dayanağı paket revizyonunda doğrulanacak. |
| F-12 MOD-0298 ve izdüşüm | Kabul | HCM-EMP-01 kaydı bulgu 2 ile tutarlı; MOD-0298 sahip ataması yok (`dagilim-evet`). |
| F-13 izin tohumu | Kabul — CT madde 9 ile aynı | — |
| F-14 workflow-decisions izni yok | Kabul — CT ek bulgusu ile aynı | — |
| F-15 yetkisiz kullanıcıya sayfa iskeleti | **Kabul — kaçırıldı** | `Views/HCM/Employees/Create.cshtml:7-20` izin bayrağını yalnız `data-can-create-draft` olarak JS'e veriyor, başlık ve iskelet koşulsuz çiziliyor (UAS-001). |
| F-16 eşzamanlı idempotency | **Kabul — kaçırıldı** | `services/Diten.HcmService/src` içinde `CreateIndex` yok; tekil indeks kurulmuyor. |
| F-17 eskimiş metin | Kabul — CT notu ile aynı | — |

İtiraz: yok. Kapsam notu: F-10 (yukarıda).

## CT'nin kendi denetimine dönük ders

HCM-EMP-02 notu (ae6ed9d2e) taslak dilimini "çalışıyor ve testlerle korunuyor" diye özetledi. F-1, F-2, F-3, F-15 ve F-16
bu özeti çürütüyor: onaylı P2 diliminin kendisinde düzeltilmesi gereken kod hataları var. Not düzeltmesi bu kayıtla
yapılır; eski not üzerine yazılmaz.

## Sonraki adım önerisi (öneri — onay değil)

1. Bulgu dosyasının commit'i ve `review-row` satırı (inceleyen codex, model/effort/oturum "verilemiyor").
2. HCM-EMP-05 paket revizyonu `module-pack-author` ile: Teslim dilimleri tablosu + dilim başına kanıt seviyesi (S-85),
   P2 kapanışının "kayıtlı kanıt yok" diye düzeltilmesi, F-1/F-2/F-3/F-15/F-16 için bir "P2 düzeltme" dilimi, liste/detay
   kararı için ayrı dilim, açık kararlar K-1…K-9.
3. P2 düzeltme dilimi bir kod işi olur (ayrı WP, ayrı onay, Gateway kısmı yalnız `integration-agent`).
