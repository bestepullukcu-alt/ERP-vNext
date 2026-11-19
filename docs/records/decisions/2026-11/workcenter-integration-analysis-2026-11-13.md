# Görev Merkezi'ne bağlanacak işler — analiz ve sahip kararı (2026-11-13)

Kaynak: CT salt okunur analizi (MVP-5 WMS CT oturumu `ca7382cf`, alt ajan; kod + paket okuması, çalışan ortamda ölçülmedi) ve sahip
kararı E-6 (`mod0182-mod0365-owner-decisions-02.md`: "kişiye atanan her iş Görev Merkezi'nde"). Sahip: Beste.

**Sahip kararı (Beste, 2026-11-13):** "Önce işlerimizi tamamlayalım; **Görev Merkezi kısmını MVP-5 bittikten sonra ekleriz.**" Bu kayıt
o işin girdisidir. MVP-5 bitene kadar yeni Görev Merkezi bağlantısı yapılmaz; mobil ara dönemde modül uçlarından okur (mobil not §3B).
Açık sorular aşağıda — iş başlarken sahibe sorulur (CT önerileriyle).

## Görev Merkezi bugün (koddan)
- Platform her açılışta kayıtlı sağlayıcıları sırayla çağırıp tek listede toplar (`WorkItemsController.cs:45,91,137,175`; eylemler ayrı parça,
  `DependencyInjection.cs:428-440`). Platform içi kaynaklar: Workflow onayları (MOD-0023, `:389`), Görevler (MOD-0024, `:420`), toplantı
  davetleri (MOD-0357, `:423`).
- Workflow başlatan her modülün onayı kendiliğinden gelir (CRM, MDM, Inventory'de yalnız karantina — `QuarantineWorkflow.cs:13-20`); e-imza
  Workflow onay adımına bağlı kapıdır (`ESignatureGate.cs`).
- Başka servisteki modül: liste ucu `api/v1/work-items/projection` + eylem ucu `.../{itemId}/actions/{actionCode}` + Platform ayarında bir satır
  (`WorkAggregation:RemoteProviders`, `RemoteProviderOptions.cs:3-20`). Bugün tek satır `dev-reference` (geliştirme ayarı).
- MDM ürün kısaltma onayları ucu hazır (`ProductAbbreviationWorkItemsController.cs:15`) ama Platform ayarında satırı yok.
- Risk BL-303: sağlayıcılar sırayla çağrılır; çok sağlayıcı = yavaşlık (`GetMyWorkItemsHandler.cs:40-43`).

## Modül modül
| Modül | İş | Bugün GM'de | Kime | Öneri |
|---|---|---|---|---|
| 0175 Karantina | Karar onayı (e-imza, Workflow `INV-QUARANTINE-RELEASE`) + lot serbest bırakma | **var** | Onaycı (bugün tek kişi) | Kalsın |
| 0175 | İnceleme bekleyen karantina kaydı (`Open`, `Waiting/InReview`) | yok | KK havuzu | Orta |
| 0175 | Takılan karar (`StartFailed` / `ApplyFailed`) | yok | Kararı veren | Düşük |
| 0178 | Toplama / yerleştirme görevi (`Open` / `InProgress`) | yok | Atanan kişi ya da havuz | **Yüksek** (E-6) |
| 0178 | Kayıt bütünlük hatası (`IntegrityError`) | yok | Vardiya sorumlusu | Orta |
| 0178 | Reddedilen kayıt (`Refused`) | yok | Görev sahibi | Görev rozeti, ayrı iş değil |
| 0178 | Paketleme (`Picked → Packed → ReadyToShip`) | yok | Kişi alanı yok | Karar (S-2) |
| 0178 İzleme | 9 istisna türü (kayıt tutulmuyor) | yok | Vardiya sorumlusu | Düşük, yalnız görüntü |
| 0181 | Atanmış sayım (`Released` / `Counting`) | yok | Sayan kişi | **Yüksek** |
| 0181 | Onay bekleyen fark (`AwaitingApproval`) | yok | Onaycı | **Yüksek** (modül içi onay korunur, GM'den eylem) |
| 0181 | Takılan düzeltme (`Refused` / `IntegrityError`) | yok | Yetkili | Düşük |
| 0176 SKT | İstisna vakası (`Open` / `InReview`, havuz + al) | yok (FU-0176-WC-01 "yalnız görüntü") | KK havuzu | **Yüksek** |
| 0177 Geri çağırma | Kapsam onayı (`PendingScopeApproval`, e-imza) | yok (FU-0177-WC-01 "yalnız görüntü") | Onaycı | **Yüksek** |
| 0177 | Etkinlik kontrolü / kapanış | yok | Vaka sahibi | Orta |
| 0177 | Başarısız karantina isteği | yok | Vaka sahibi | Düşük |
| 0177 | Vadesi gelen tatbikat | yok | Plan sahibi | Düşük-orta |
| 0180 Dalga | Dalga sorumlusu + istisnalar (`ShortPick`, `StockMissing`, `LotUnderRecall`, `CutoffMissed`, `TaskWaitingLong`) | paket taslağı | Dalga / vardiya sorumlusu | MVP-5 sonrası FU (paket r4'e eklenmez — sahip kararı) |
| 0173 + FU'lar, 0174, MOD-0040-FU02, MOD-0262-FU01 | Kişisel iş yok | — | — | Bağlanmaz |
| 0182 Mobil | GM'yi **kullanan** taraf (E-6) | — | — | Sağlayıcı değil |

## Önerilen sıra (MVP-5 sonrası)
1. Platform: BL-303 (sırayla çağırma) + liste üst sınırı + Inventory sağlayıcı satırı kararı.
2. 0178 toplama / yerleştirme + 0181 sayım ve fark onayı (büyük; mobil bunu bekliyor).
3. 0176 SKT istisnaları + 0177 kapsam onayı / kapanış (orta; e-imza GM detayında).
4. 0175 inceleme + takılan kararlar, 0178 bütünlük hatası (küçük-orta).
5. 0180 dalga istisnaları (FU), izleme istisnaları, tatbikat (küçük).
Her parça için takip paketi / FU numarası `verify_module_id.py` ön kontrolüyle açılır (MOD numarası uydurulmaz).

## Açık sorular (iş başlarken sahibe; CT önerisiyle)
| # | Soru | CT önerisi |
|---|---|---|
| S-1 | Toplama görevleri: görev başına iş mi, bölge / dalga başına mı? | Kişiye atanan her görev ayrı iş; havuzdakiler bölge başına gruplu; sayfalı liste |
| S-2 | Paketlemede atanan kişi yok | Paketleme istasyonu havuzu; "Görevi al" ile kişiye geçer |
| S-3 | Mal kabul kimin? | Mobil "Mal kabul" kutucuğu `Receipt` kökenli yerleştirme görevlerini açar; gerçek mal kabul MVP-6 — onlara sorulur |
| S-4 | 0176 / 0177 "GM yalnız görüntü" kararları | Değişsin: eylem GM'den, e-imza GM detayında (karantina deseni) |
| S-5 | Karantina onayı tek kişiye gidiyor | Onaycı havuzu; biri alınca diğerlerinden düşer (MOD-0023 sınırı) |
| S-6 | İzleme istisnaları kayıt tutmuyor | İlk sürüm yalnız görüntü ("aç" bağlantısı); kalıcı kayıt sonra |
| S-7 | MDM ürün kısaltma sağlayıcısı | Ali'ye soruldu (2026-11-13) |
| S-8 | Paket / FU numaraları | İş başlarken CT açar (DCP-002) |

## Ölçülmeyenler
Çalışan ortamda `work-items/mine` çağrılmadı; üretim / hazırlık ayarları okunmadı; hacimler tahmin; GM havuz sekmesinin uzak sağlayıcıyla
çalıştığı doğrulanmadı; MOD-0040-FU02 / 0262-FU01 yalnız paket metninden; MOD-0022 / 0023 ayrıntılı okunmadı; 0178 paketi tam okunmadı.
