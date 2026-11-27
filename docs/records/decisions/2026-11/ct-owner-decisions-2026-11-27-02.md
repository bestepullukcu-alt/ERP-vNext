# CT sahip kararları — 2026-11-27 (2) (Beste)

- **Tarih:** 2026-11-27
- **Karar veren:** Beste, sohbette: 12 maddelik öneri listesine "önerin gibi".
- **Önceki kayıt:** `ct-owner-decisions-2026-11-27.md`. Bu kayıt kod, commit ya da dağıtım izni değildir (madde 10 ve 12 dışında; onlar açık push/PR iznidir).

| # | Konu | Karar |
|---|---|---|
| 1 | MOD-0180 DL-5 | Başlatılır (WP-0180-DL5-kpis-csv). WMS dilim sırası: **0180 DL-5 → 0182 DL-4 → 0179 DL-2** (ortak sayaç satırları ve makinede tek Inventory suite). |
| 2 | MOD-0182 DL-4 | DL-2 için verilen XMC onayları (X-10 `Perm.Entity` satır adları, X-5 SSOR koleksiyon listesi) DL-4'ü de kapsar. |
| 3 | MOD-0185 / MOD-0187 | Korelasyon düzeltmesinde MOD-0186 C yolu geçerli; iki modül SHIPMENT-BUNDLE **3.8.0**'ı paylaşır; 3.7.0 (MOD-0183 intake) takılırsa numaralar yer değiştirebilir (CT kararı). |
| 4 | MOD-0186 DL-6 | 3.6.0 yaması DL-6 koduyla aynı commit'te; DocsPathGuard yeniden sabitlemesi ayrı CT commit'inde (ayrı sahip kaydıyla). RC-8 canlı Gateway ölçümü canlı turda. |
| 5 | Görev Merkezi A yolu | Başlangıç koşulu **"MOD-0180 dalga canlı turundan sonra"** (11-19 kararı) geçerlidir; tur 2026-11-20'de geçti. 11-13 kaydındaki "MVP-5 bitince" ifadesinin yerine geçer. |
| 6 | MOD-0140 | Sıradaki adım DL-6 (Blocked/OnHold kontrolü) merkezi ekipçe alınır; yüksek riskli revizyon onayı Ali (GMG-ERP). DL-4 / DL-7 dış bağımlılıkları (MOD-0023 S-3, S-5, BL-652) Ali'ye sorulur. |
| 7 | MOD-0142-FU01 toplu silme | "Hep ya da hiç": seçilenlerden biri silinemiyorsa hiçbiri silinmez, 409. |
| 8 | MOD-0142-FU01 S-9, S-12 | Paket önerileri (S-9: ayar eksikse açılış hatası; S-12: başarısız akış için ek GRN kaydı yok) kabul; Ali'nin onay satırı notuna yazılır. |
| 9 | MOD-0177-FU01 DL-3 S-1 | Kod paket metnini aynen uygular; §8.11 madde 5 açıklamasıyla bağlayıcı F-1 kuralı ("en son yazılan kalem") arasındaki fark **bilinen sapma** olarak kayda geçer; r11 / RECALL 1.11.1 için takip maddesi açılır. S-2: "en son yazılan kalem" bütün Shipment kalemleri arasından (paket metni). |
| 10 | MOD-0360-FU01 DL-2.0 | origin/main'den kısa bir dal açılıp küçük bir PR gönderilir (AC-28 (a)–(d), sabotaj 1, 2, 5, 6). Push ve PR izni bu iş için verildi. |
| 11 | DCP-011 D150 onay satırı | Main'deki satır `pack_approval check`'te geçersiz (PR'ı açan satırın yazarı değil). Yeni satır: yazar `bestepullukcu-alt` (CT), onaylayan ve merge eden Ali (GMG-ERP). |
| 12 | Docs PR | CT zincirindeki main'de olmayan kayıtlar (paketler, incelemeler, kararlar, WP'ler) ve Ali'nin dört onay satırı (MOD-0191 r7, MOD-0142-FU01 r4, MOD-0183 r12, DCP-011 D150) tek docs PR'ında; push ve PR izni verildi. Merge commit ile birleştirilir. |

## Uygulanan adım (aynı gün)

- WP-0027-FIX-01 dev adımı: `tenant.invite.email` en (`179da371…`) ve tr (`7bcc1159…`) 1.1.0 platform şablonları Platform yönetici ekranından arşivlendi (CT, Beste'nin Platform oturumu); Platform yeniden başlatılınca seed 1.2.0 satırlarını ekler. Ek gözlem: şablon listesi yalnız ilk 100 kaydı getiriyor (en / tr 2. sayfadaydı) — WP-0027-FIX-01'e not.
