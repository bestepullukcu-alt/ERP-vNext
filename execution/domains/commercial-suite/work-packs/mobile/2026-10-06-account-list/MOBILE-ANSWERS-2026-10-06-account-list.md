# Mobil talebe yanıt — iş yeri listesi: aktif kişi sayısı, filtre, bölge seçenekleri (2026-10-06)

> Talep: [BACKEND-CRM-ACCOUNT-LIST-ACTIVE-CONTACTS-REQUIREMENTS.md](BACKEND-CRM-ACCOUNT-LIST-ACTIVE-CONTACTS-REQUIREMENTS.md) (R1–R4).
> CT kararı + plan yeri: [ROADMAP-visit-planning.md](../../ROADMAP-visit-planning.md) **Faz 2b**.

## Kararlar
| Talep | Karar | Ne zaman |
|---|---|---|
| **R1** `activeContactCount` | **Kabul.** | Faz 2b (WP-VP-2 bittikten hemen sonra) |
| **R2** `hasActiveContacts=true\|false` | **Kabul.** | Faz 2b |
| **R3** bölge seçenekleri | **R3-a kabul:** `GET /api/crm/accounts/filter-options`, `crm.account.read`. Bölgesi atanmış temsilci için yalnız **kendi bölgesindeki** düğümler. | Faz 2b |
| **R4** iş yeri türü etiketleri | **Uç zaten açık:** `api/lookups/reference-data/consumable-sets/account-type/published-values` (main'de, PR #132; sizin incelediğiniz `b994c813a` bunu içeriyor). **Eksik olan Türkçe etiket verisi** (bugün yalnız İngilizce "Hospital"…), plan 0.5. | Veri işi |

**R1 sayım kuralı:**
- `F5` ile aynı: silinmemiş bağlantı, durumu `ended` / `inactive` değil; kişisi silinmiş bağlantı sayılmaz.
- `/accounts/{id}/contacts`'ın aktif saydığıyla birebir eşit olmalı.
- Sayfa başına tek toplama yapılır (N+1 yok).

**R2 davranışı:**
- Diğer tüm filtrelerle VE ile birleşir.
- `total` filtreyi yansıtır, `unfilteredTotal` kiracı geneli kalır.
- Geçersiz değer → **400** (doğrulama hatası).

**R3 kapsamı:**
- Yalnız mevcut hesaplarda bulunan değerler döner (düğüm id / kod / ad + iş yeri türü kodu); `search` ile daraltılabilir.
- Bölge hiyerarşisinde yazma verisi yok.

## Açık soruya CT yanıtı
- `activeContactCount` **`crm.account.read` altında** döner. Bu yalnız bir sayıdır, kişi verisi değildir; `crm.account-contact.read` aranmaz.

## Ek not — temsilcinin hedef seçici listesi
- Ürün kararı (2026-10-06): temsilcinin hedef evreni **kendi bölgesidir** (K-6).
- Faz 2'de (WP-VP-2) yeni uç geliyor: `GET /api/crm/visit-plan/my-accounts`. Temsilcinin ilçelerindeki hesapları döner; bölgesi atanmamışsa `territoryStatus = unassigned` + tüm hesaplar.
- R1 / R2 bu uca da eklenecek. Mobil hedef seçici bu uca geçtiğinde liste otomatik olarak temsilcinin bölgesine iner.
- Genel `GET /api/crm/accounts` "bölge dışı ekle" için kalır.
- Alan adları ve zarf aynı olacak; kesin sözleşme notu Faz 2b bitince gönderilecek.

## Ara dönem
- Mobilin geçici çözümü (ekrandaki satırlar için sınırlı eşzamanlı `/contacts` yoklaması) Faz 2b gelene kadar uygundur.
- Önerilen eşzamanlılık: en fazla 4 istek.
