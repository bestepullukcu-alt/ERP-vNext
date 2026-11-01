---
record_id: PSS-MOD0286-TRACKING-PAGE-01
status: opened — sahip `sayfa-ac` (2026-10-23); CT-TRK-01 (SOP §22.2), kart §0 "İş takip sayfası (her CT için zorunlu — S-39)"
prepared_at_local: 2026-10-23
prepared_by: PSS-UI Control Tower (Claude Code oturumu 05ce4e4b)
block: MOD-0286 UI Design System & Page Pattern Governance (P1: DL-1 şartname + DL-2 uyum kontrol listesi; P2 konsol ertelendi)
page: https://claude.ai/artifact/8aGEWHrcX2hAi5Gs3U85cu
page_name: UI Tasarım Sistemi İş Takibi
visibility: ÖZEL (yalnız sahip; paylaşım sahibin Share menüsünden, herkese açık yapılmaz — SOP §22.2/6)
---

# MOD-0286 — iş takip sayfası kaydı (CT-TRK-01)

> SOP §22.2/8: sayfa bağlantısı block'un kuyruk kaydının not alanına ya da karar kaydına yazılır; sonraki oturumlar aynı
> sayfayı kullanır, yenisini açmaz. Bu block'un kayıtlı kuyruğu henüz yok (`ct-queue-registry.tsv`'de `pss-ui` satırı
> sahip onayı bekliyor); bağlantı bu kayıtta tutulur, kuyruk satırı açılınca `note` alanına da yazılır.

## Sayfa

- Bağlantı: https://claude.ai/artifact/8aGEWHrcX2hAi5Gs3U85cu
- Biçim: claude.ai Artifact, paylaşılan veritabanı (`db`): `items` koleksiyonu (iş başına bir belge: kod, başlık, bir cümle, sahip, risk, bitti, tarih) ve `meta/state` (paket sürümü, dal/HEAD, onay durumu, son güncelleme). Kurallar: `items` ve `meta` herkes okur, yalnız Editor/Owner yazar.
- Bölümler (SOP §22.2/5): Şimdi ve sırada · Karar bekleyenler · Sonraki fazlar · Bizim takipler · Başka sahiplerin işi (XMC-001) · Ortak bileşenler · Duraklatılanlar · Geçmiş (bitti).
- İlk doldurma 2026-10-23: 50 iş kaydı (6 sırada, 12 karar [4'ü bitti], 2 faz, 5 takip, 7 başka sahip, 6 ortak bileşen, 2 duraklatılan, 11 geçmiş) + 1 durum kaydı; kaynak paket rev 06, karar kaydı 01, inceleme kayıtları 01–06, `reviews.tsv` / `approvals.tsv`.
- Nasıl kullanılır: sahip bir işi işaretler → sayfa `done` ve tarihi yazar; CT bir sonraki oturumda okur ve depoya işler (karar kaydı, kuyruk durumu). İşaret tek başına karar kaydı değildir (SOP §22.2/4). CT her kabul, prompt ve karardan sonra listeyi depodan günceller (§22.2/7). Çelişkide depo kazanır.

## Doğrulama (2026-10-23)

- Yayım: sürüm 1, `db` (2 kural) + `user`; görünürlük yalnız sahip.
- Veri: iki toplu yazım, 51 belge; `view` seviyesiyle `items` okuması: 50 belge görünür (kurallar gereği okunabilir).
- Ölçülmeyen: sayfanın tarayıcıda görüntüsü bu kayıtta yok; sahibin işaretleme yazması canlı denenmedi (ilk işaret CT'nin bir sonraki okumasında doğrulanır).
