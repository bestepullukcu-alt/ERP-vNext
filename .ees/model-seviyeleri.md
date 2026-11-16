# Model seviyeleri ve araç eşlemesi

Kural (çekirdek Ç30, kule K4/K5/K16): model seçimi araca değil **seviyeye** bağlıdır; iş türüne, riske ve token maliyetine
göre seviye seçilir, her araç seviyeyi kendi modeline ve eforuna eşler. Bu dosya yalnız eşlemeyi tutar; fiyat ya da model
değişince yalnız bu dosya ve üreticideki eşleme tablosu değişir, kartlar değişmez.

## 1. Üç seviye

| Seviye | Ne için | Örnek işler |
|---|---|---|
| **Üst** | Yanlışı herkesi durdurabilecek ya da güvenliği bozabilecek iş; uzun, belirsiz, çok adımlı akıl yürütme | modül paketi; onay akışı, komut freni, birleştirmeyi durduran kontrol; yüksek etkili kör denetim; kule karar hazırlığı |
| **Standart** | Varsayılan yazma ve denetim | işçi kodu ve testi; olağan kör denetim; belge ajanı; iş analizi taslağı; hat koordinasyonu; modül araştırması |
| **Hafif** | Kuralı belli, çıktısı araçla doğrulanan, çok tekrarlanan iş | çeviri dosyası doldurma; çeviri denetimi; dosya ve yol tarama; özet satırı; biçim düzeltme |

## 2. Seçim ve yükseltme

1. En ucuz yeterli seviyeden başla; iş tablodaki bir örneğe benzemiyorsa standart.
2. Yüksek riskte (güvenlik, kiracı ayrımı, yetki, geri alınamaz etki, kulenin "yüksek etkili" saydığı değişiklik) doğrudan
   bir üst seviye.
3. Bir kalite hatasından sonra tek düzeltme hakkı bir üst seviyede kullanılır; ikinci başarısızlık kişiye gider, üçüncü
   seviyeye değil (çekirdek Ç16).
4. Geçici hata seviye değiştirmez; en çok 2 yeniden deneme (Ç15).
5. Araç destekliyorsa önce eforu artırmayı dene; aynı modelde yüksek efor çoğu zaman bir üst modelden ucuzdur (araç
   belgesinin önerisi; bizim işlerimizde **ölçülmedi**).
6. Seviye seçimi ve tablo içinde bir üst seviyeye çıkış kulenin M1 kararıdır; en üst seviye model ve "yüksek etkili mi"
   anlaşmazlığı M2'dir (kule K4, K5).

## 3. Araç eşlemesi

### Claude (Claude Code)

| Seviye | Model | Efor | Not |
|---|---|---|---|
| Üst | Opus 5.5 | `high` (gerekirse `xhigh`) | Varsayılan efor `medium`; açıkça yazılmalı |
| Üst+ (M2: yalnız üst seviye bir kez başarısız olursa ya da sahip isterse) | Fable 5.2 | `high` | En yetenekli ve en pahalı; varsayılan değil |
| Standart | Sonnet 5.5 | `medium`–`high` | Kodlama ve çok adımlı araç işinde `medium` ile başla |
| Hafif | Haiku 5.5 | `low`–`medium` | |

Model adları bu oturumun ortam bilgisinden (2026-11-16). Göreli fiyat sırası Haiku < Sonnet < Opus < Fable; tasarım notundaki
liste fiyatları (12 Kasım) burada tekrar edilmedi, **yeniden doğrulanmalı**. Claude Code alt ajan tanımı bugün
`model: sonnet | opus | haiku | inherit` ve `effort:` alanlarını kullanıyor (ölçüldü: üretim listesi, bugün `.ees/adapters.json`); Fable
için takma ad **doğrulanmalı**. Claude Code abonelikle ödendiği için tablo göreli maliyet içindir.

### Codex — **doğrulanmalı**

| Seviye | Model | Efor |
|---|---|---|
| Üst | doğrulanmalı (Codex'in en yetenekli kodlama modeli) | doğrulanmalı (`model_reasoning_effort` alanı ve değerleri) |
| Standart | doğrulanmalı | doğrulanmalı |
| Hafif | doğrulanmalı | doğrulanmalı |

Bugünkü `.codex/agents/*.toml` dosyalarında model alanı yok (ölçüldü, 12 Kasım). Codex'in kimlik ve fatura düzeni (API
anahtarı mı, abonelik mi) açık sahip kararıdır.

### Gemini — **doğrulanmalı**; bugün onaylı araç listesinde değil

| Seviye | Model | Not |
|---|---|---|
| Üst | doğrulanmalı | |
| Standart | doğrulanmalı | |
| Hafif | doğrulanmalı | |

Yeni araç EES sahibinin önerisi ve Karar Kurulu onayıyla eklenir (rehber §5, çekirdek Ç9). Antigravity'nin hangi modeli
çalıştırdığı da **doğrulanmalı**.

## 4. Eşleme nerede durur

- Asıl ajan tanımı yalnız `seviye: ust | standart | hafif` yazar (`.ees/ajanlar/*.md`, iş 3'ün uygulama adımı).
- Üreticinin girdi dosyasında araç başına `tier_map`: seviye → (model, efor). Üretici kopyaya aracın kendi alanını yazar.
- Seçilen ve gerçekten kullanılan seviye, model ve efor çalıştırma kaydına girer (`sablonlar/calistirma-kaydi.md`).

## 5. Maliyet nasıl izlenir

| Ne | Nereden | Kim |
|---|---|---|
| İş başına token, süre, seviye, model, efor | çalıştırma kaydı | araç (iş 5–6); o güne dek kule |
| Hat başına haftalık toplam | durum sayfası | araç; o güne dek kule |
| Seviye yükseltme sayısı, tamamlanan iş başına maliyet | haftalık özet | kule (K21) |

Ölçüt istek başına değil **tamamlanan iş başına** maliyettir: iki tur tekrar eden ucuz seviye pahalıdır. Başlangıç tabanı
(12 Kasım ölçümü): işçi 95–155 bin token, yeni kule ~150 bin token okuyordu.
