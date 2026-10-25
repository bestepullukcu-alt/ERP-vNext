---
record_id: TREASURY-GAP-LIST-17
kind: sahip inceleme bulguları ve kapanışları (D26 paketi — MOD-0119 + ortak kurallar dört pack'te) — onay değildir
received_at_local: 2026-10-25
received_from: depo sahibi (K1), sohbette yapıştırılan inceleme ("CONDITIONAL PASS — D26 doğru işlenmiş; iki düzeltme")
reviewed_input: .ct-teslim/treasury-intake/treasury-D26-MOD0119-bf21f079f.zip (sha256 b78570ec0041afa6a59c520693081c4a2adf0ca57c6bd1701ffcab2b2382a266)
fixed_in: MOD-0126, MOD-0119, MOD-0120, MOD-0124 pack'leri (bu kaydın commit'i)
previous: GAP-LIST-13-MOD0289.md
---

# Gap listesi 17 ve kapanışları

| # | Öncelik | Gap | Kapanış |
|---|---|---|---|
| 1 | P1 | tx2 kesin ret tablosu 404'ü `Refused` sayıyor; D21/7 404'ü `Unknown` sayar | Dört pack'te tx2: "kesin ret (400/403/409 — 404 **hariç**: belirsiz → `Unknown`, D21/7; …)"; Test Expectations sahte sunucu satırı "404 belirsiz → `Unknown`". Yeni AC (sabotajlı): 404 sonrası niyet `Unknown`, iş kaydı durumu geri alınmaz, 202, sonuç olayı yok — MOD-0126 AC-40 (DL-2), MOD-0119 AC-41 (DL-2), MOD-0120 AC-43 (DL-3), MOD-0124 AC-37 (DL-3). |
| 2 | P2 | D25 eski metinlere uygulanmamış ("silinmişi ayıran okuma gerekmez") | Dört pack'te bağımlılık satırı, yeniden yazma kapısı maddesi ve kontrol listesi D25/2'ye göre birleştirildi: silmeme politikası korunur, silinmiş kayıtlar `isDeleted` / `includeDeleted=true` ile de okunur, görülürse yeniden yazma yok (AC-W13). Kalan "AC-W…W12" atıfları W14'e güncellendi. |

D26 tutar kuralında yeni açık yok (sahip). Procurement değişiklikleri ve çalışma zamanı testleri bu belgelerle doğrulanamaz.
Kontroller: dört pack 24 bölüm, AC'ler tekil (MOD-0126 41 satır, MOD-0119 41, MOD-0120 43, MOD-0124 37), yeni AC'ler dilimlerinde
kapanıyor. Belge incelemesidir; build / test / runtime yok.
