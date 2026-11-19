# MVP-2 (Procurement) — devir notu

> **Tarih:** 2026-11-17 · **Ölçüm:** origin/main `76466f413` · **Devreden:** Cihan (MOD-0140 sahibi, K4)
> **Kayıt kuralı:** bu bir tarihli kayıttır (`docs-organization.md` §1 soru 2); ana dalda düzeltilmez.
> **Not:** bu dosyada gizli bilgi, parola ve bağlantı adresi yoktur. Ortam ayarları ayrıca konuşulacak.

## 1. Önce bilinmesi gereken üç şey

1. **İşler uzak depoda, ama Cihan'ın adıyla değil.** Ekim–Kasım işleri bulut oturumunda yapıldı. Commit yazarı
   `Claude`; push ve PR'lar `bestepullukcu-alt` hesabıyla. Yazara "Cihan" diye süzülürse son commit 2026-09-23
   (`780f8368a`) görünür ve açık PR bulunmaz. Gerçek iş aşağıdaki dallarda ve PR'larda.
2. **MOD-0140 Rev 9.3 kayıt PR'ını yalnız Cihan açabilir.** Onay aracı (S-115 F-5), satırları main'e getiren PR'ı
   açanın satırın yazar hücresiyle aynı kişi olmasını şart koşuyor. Satırlarda bu kişi `cihancalli`. Cihan PR'ı
   açmadan ayrılırsa bu satırlar main'e geçemez; yeni sahip yeni sürüm ve yeni satırlarla baştan başlar.
   **Devrin ilk adımı budur.**
3. **MOD-0360-FU01 (tedarikçi kullanıcı bağı) kabulü DL-3'le tamamlanamaz.** Ayrıntı §5.

## 2. Modüller

Puanlar Ö1…Ö7 (1 = tam, 0,5 = kısmen, 0 = yok): Ö1 paket ve onay · Ö2 arka uç · Ö3 ekran · Ö4 test ·
Ö5 gerçek entegrasyon · Ö6 merkezi denetim · Ö7 kabul. Onay sütunu: `scripts/pack_approval.py check`,
main `76466f413`. "eski onay geçerli" = paket kural başlangıcından beri değişmedi.

| Modül | Rol | Paket durumu | Onay (main) | Dilim | Ö1–Ö7 | Kalan iş (özet) |
|---|---|---|---|---|---|---|
| MOD-0140 Tedarikçi Onboarding | Ana | ready-for-dev | **onay geçersiz** (Rev 9.2, inceleme dosyası araç biçiminde değil) — Rev 9.3 dalda | DL-1 main (#289), DL-2 main (#321), **DL-3 dalda**, DL-4…DL-7 yok | 0,5·1·1·0,5·0·0·0,5 | §3 |
| MOD-0141 Talep ve SAS | Ana | ready-for-dev | eski onay geçerli | — | 0,5·0,5·1·0,5·0·0·0 | talep onayı yok (talepten PO açılamıyor); talep/PO güncelleme yok; mal kabul PO'yu güncellemiyor; ürün doğrulaması izin verici; denetim yok |
| MOD-0142 Mal Kabul (GRN) | Ana | ready-for-dev | eski onay geçerli | — | 0,5·1·1·0,5·0·0·0,5 | stok hareketi taklit istemcide (GRN Posted, stok yok); lot/lokasyon/olay yok; telafi yok |
| MOD-0143 Fatura ve 3'lü Eşleştirme | Ana | ready-for-dev | eski onay geçerli | — | 0,5·1·1·0,5·0,5·0·0 | tolerans sıfır (OD-2 açık); istisna çözümü onaya bağlı değil; denetim yok. **PR #278** incelemede |
| MOD-0144 Sözleşme / Madde Kütüphanesi | Destek | ready-for-dev | eski onay geçerli | — | 0,5·1·1·0,5·0,5·0·0 | InReview/Expired geçişleri yok; OD-4 açık; denetim yok |
| MOD-0145 Kaynak Bulma (RFQ/RFP) | Ana | ready-for-dev | eski onay geçerli | — | 0,5·1·1·0,5·0,5·0·0 | değerlendirme/kapanış/iptal yok; RfxAwarded olayı yok; denetim yok |
| MOD-0021 Genel Denetim İzi (BL-501) | Destek | ready-for-dev | eski onay geçerli | — | 1·1·1·1·0,5·0,5·0,5 | Procurement bağlı değil (DL-3 ile 8 komut bağlanıyor); MDM/Auth eski iletici |
| MOD-0023 İş Akışı | Destek | ready-for-dev | onaylı (HIGH, 2026-10-30) | §17 DL-1 main | 0,5·1·1·1·0,5·0,5·0,5 | Procurement istemcisi yok (MOD-0140 DL-4); §17 DL-2 sahibin WP'si |
| MOD-0024 Görev / Kontrol Listesi | Destek | review | eski onay geçerli | — | 0,5·1·1·1·0,5·0,5·0,5 | ana paket sahip kabulünde; Procurement kullanmıyor |
| MOD-0173 Envanter Defteri | Destek | ready-for-dev | **onay yok** (paket kural başlangıcından sonra değişti) | — | 0,5·1·1·1·0,5·1·0,5 | koşullu kabul (G1-D1, G1-D2); Procurement'tan gerçek hareket yok. Sahibi MVP-1/SCE hattı |
| MOD-0290 Ürün Ana Verisi | Destek (MVP-2 + MVP-3) | in-progress | eski onay geçerli; FU06, FU07 onaylı | — | 0,5·1·1·1·0,5·0,5·0 | MVP-3 notunda |

## 3. MOD-0140 — nerede kaldı

| Parça | Dal / PR | Uç | Durum |
|---|---|---|---|
| Rev 9.3 (yalnız kayıt revizyonu) | `docs/mod0140/rev9-3` | `361623752` | Paket SHA-256 `6cb57a5a…`; D3 incelemesi UYGUN (3 DÜŞÜK); Natig'in HIGH onayı, kart `a50619e4…`. **PR açılmadı** (Cihan açacak). Main ile kayıt dosyalarında çakışıyor; bu beklenen bir durum. |
| DL-3 (uygunluk + ilişki uçları, BL-501) | `feature/mvp2/supplier-dl3-commands` | `d2476927c` | Kod, test ve sözleşme 1.4.0 hazır; main ile çakışmasız. **PR açılmadı**; Rev 9.3 main'de `check`'ten geçince açılır. |

**DL-3 ölçümü (2026-11-10):** Procurement 238/238 (gerçek replika kümesi Mongo), mimari 227/227; 15 sabotajın 15'i kırmızı.

**Sıra:**
1. Cihan Rev 9.3 PR'ını açar (base `main`).
2. Merge'ten hemen önce `approvals.tsv` / `reviews.tsv` çakışması çözülür: önce main'in satırları, Rev 9.3'ün iki satırı sonda (CT-APR kart §7a). Çözümden sonra kart `a50619e4` kalmalı ve satırlar `578cd3685`'e bağlı görünmeli; bu iki kontrol yapılır.
3. K1/K2/K3 merge eder; Cihan merge etmez.
4. Main'de `check` "onaylı" döner.
5. DL-3 dalına main birleştirilir; `supplier.openapi.yaml` içindeki `x-producer-status` tarihi merge gününe çekilir.
6. DL-3 PR'ı açılır (açıklamada §18 "DL-3 canlı öncesi" kapıları yer alır), incelenir ve merge edilir.

**Tahmin:** Rev 9.3'ün merge günü + 3 iş günü (2 iş günü DL-3 PR'ı, 1 iş günü main). Bugün açılıp merge edilirse
DL-3 2026-11-20'de main'de olur. 10, 17 ve 19 Kasım tahminlerinin hepsi Rev 9.3 PR'ı açılmadığı için kaydı.

**DL-3'ten sonra:** DL-3'ün ertelenen parçası (kiracı geneli arama + `read-tenant-wide`; ayrı sürüm ve inceleme) →
DL-4 (S-3 sonrası; MOD-0023 onayı, `Approved`/`Rejected` sonucunu yazar) → DL-5 → DL-6 → DL-7 (servis token'ı,
sözleşme 1.5.0; S-5 ve BL-652 sonrası).

## 4. CT kuyruğu (MVP-2)

| Bekleyen | Kimde | Not |
|---|---|---|
| Rev 9.3 kayıt PR'ının açılması | Cihan | §1 madde 2 |
| Rev 9.3 PR'ının merge'ü | K1 Natig / K2 Beste / K3 Ali | çakışma çözümünden sonra |
| DL-3 PR'ının incelenmesi ve merge'ü | CT + K1/K2/K3 | Rev 9.3'ten sonra |
| PR #278 (MOD-0143 hata kodu 7 dil) | inceleyen + merge eden | 2026-10-28'den beri açık; CI 40/40 yeşil, main ile çakışmasız |
| MOD-0173 onay satırı | MOD-0173 sahibi (MVP-1/SCE) | main'de "onay yok" |
| MOD-0024 ana paket kabulü | MOD-0024 sahibi | paket `review` durumunda |

## 5. Başka ekiplere verilmiş sözler

| Kime | Söz | Durum |
|---|---|---|
| **Tedarikçi kullanıcı bağı (MOD-0360-FU01, DC-03 — Beste)** | Rev 9.3 merge günü kesin DL-3 tarihi; DL-3 merge günü haber (§18 kapıları, izin hatırlatması) | DL-1'leri hazır; Auth uygunluğu Admin'in token'ı ve `X-Legal-Entity-Id` ile Gateway üzerinden okuyor. Aşağıdaki iki engele bakın. |
| Treasury CT (MOD-0119 / MOD-0289) | aynı iki haber + `procurement.suppliers.eligibility.read`'in tüketici rollerine verilmesi | MOD-0119 DL-1 sahte istemciyle 1.3.0'a göre ilerliyor |
| MVP-6 CT (MOD-0147 / MOD-0148) | aynı iki haber | MOD-0148 servis token'ıyla okuyacak (DL-7) |

Bu sözlerde **kaymaların günü günü bildirilmesi** de var. 11-17 ve 11-19 tahminleri kaydı; yeni tarih bildirilmeli.

**MOD-0360-FU01 için iki engel:**
1. **Başarılı bağ, DL-3 ile ölçülemez.** Onların paketi (F-5) bağ için `onboardingStatus = Approved` istiyor.
   Procurement'ta `Approved` yazan kod yolu yok; bu değeri DL-4 yazacak. DL-4 de S-3'e (MOD-0023 onboarding şablonu)
   bağlı ve tarihi yok. DL-3 ile yalnız "uygun değil → 422" ölçülebilir. Seçenekler onlara iletildi: DL-4'ü
   beklemek, test kiracısında test verisi hazırlamak ya da F-5'i S-1'e göre gözden geçirmek. Karar onların.
2. **`error.code` beklentisi.** Okuyucuları 1.3.0 sözleşme şekline göre yazıldı. Servis ise bütün yanıtları
   `data` / `statusCode` / `isSuccessful` / `errors` zarfıyla döndürüyor; 200 yanıtları da bu zarfın içinde.
   Zarf ↔ sözleşme kararı DL-3'e girmiyor (§20 açık madde, karar sahibi MOD-0140; üretim kullanımı §18 kapısına
   bağlı). Okuyucunun iki şekli de okuması önerildi. **Söz:** bir sonraki MOD-0140 paket revizyonunda
   MOD-0360-FU01 bu kararın danışılan tüketicilerine eklenecek.

## 6. Açık sahip kararları ve sorular

| Karar | Sahip | Etkisi |
|---|---|---|
| §20 yanıt zarfı ↔ sözleşme (MOD-0140) | MOD-0140 sahibi | MOD-0147 / 0148 / 0119 / 0289 / 0360-FU01 üretim kullanımı |
| S-3 MOD-0023 onboarding şablonu + atama kuralı | MOD-0023 sahibi | MOD-0140 DL-4 ve MOD-0360-FU01 başarılı bağ |
| S-5 servis token'ı audience (BL-652) | Auth sahibi | MOD-0140 DL-7 |
| MOD-0140 çift uygulama (Platform.API) — asıl kayıt hangisi | CT | MOD-0140 kabulü |
| MOD-0141 sözleşmeye onay/güncelleme işlemleri | sahip | MOD-0141 |
| MOD-0142 servisler arası kimlik | CT / Auth | MOD-0142 INT2-04 |
| MOD-0143 OD-2 tolerans | Finance / EA | MOD-0143 |
| MOD-0144 OD-4 (MVP-2'de mi) | sahip | MOD-0144 |
| MOD-0145 paket revizyonu (geçişler) | sahip | MOD-0145 |
| MOD-0148 paketindeki "1.4.0 DL-7" ifadesi | MOD-0148 sahibi | S-6'ya göre doğrusu 1.5.0 (DL-7); 1.4.0 DL-3'ün |

## 7. Bekleyen incelemeler ve son tarihler

| Ne | Son tarih | Durum |
|---|---|---|
| Rev 9.3 incelemesi F-3 (DÜŞÜK) | 2026-11-12 | **geçti**; sahibi MOD-0140, bir sonraki içerik revizyonunda |
| Rev 9.3 incelemesi F-1, F-2 (DÜŞÜK) | 2026-11-14 | **geçti**; aynı |
| PR #278 incelemesi | tarih yok | 2026-10-28'den beri bekliyor |

## 8. Süren iş paketleri ve oturumlar

- **WP-MVP2-SUPPLIER-TENANT-01 (MOD-0140):** Rev 9.3 + DL-3 (§3). Ölçüm kaydı
  `docs/records/audits/2026-10/mvp2-supplier-tenant-01/MEASUREMENT.md`; sahip kararı
  `docs/records/decisions/2026-10/mvp2-supplier-tenant-master-management-decision-01.md`.
- **MOD-0143 #278:** inceleme bekliyor; başka iş yok.
- **Oturum:** Bütün iş tek bir bulut oturumunda yürütüldü. Bu oturumda push edilmemiş commit, stash ya da
  commit'lenmemiş değişiklik yok (README §2). Oturumun ürettiği durum raporu ve yanıt taslakları depoda değil;
  içerikleri bu nota ve README'ye aktarıldı.

## 9. Test ortamı notları (gizli bilgi yok)

- DL-3 testleri gerçek bir Mongo replika kümesi ister. CI'daki tarball ve sha256 `.github/workflows/service-tests.yml`
  içinde.
- Eski 8.0.1xx SDK'sı Diten.Web'i ve mimari testleri derleyemez (collection-expression `Split` belirsizliği). Daha
  yeni bir 8.0 ya da 10.0 SDK'sı gerekir.
- Onay aracı sığ depoda geçmişi okumaz; önce `git fetch --unshallow`.
