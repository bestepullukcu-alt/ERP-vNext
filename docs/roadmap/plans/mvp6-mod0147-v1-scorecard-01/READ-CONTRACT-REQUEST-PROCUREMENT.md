# MOD-0147 v1.1 — read-contract request to the Procurement owner (XMC-001) — draft

DURUM: TASLAK — Natig gönderir; cevap gelince MOD-0147 paket revizyonunun DL-3 dilimi yazılır.
Kaynak karar: `docs/records/decisions/2026-10/mvp6-mod0147-v1-scorecard-split-owner-decision-2026-10-22.md`.

## Ne istiyoruz

MOD-0147 v1.1, tedarikçi performans metriklerini Procurement verisinden **hesaplar** (v1'de değerlendirici elle girer).
Bunun için Procurement'tan **yalnız okuma** gerekir; MOD-0147 hiçbir Procurement kaydını yazmaz ya da kopyalamaz (yalnız
hesaplanan değeri ve kaynak özetini saklar).

| Metrik | Gereken alanlar (dönem: takvim ayı, UTC) | Beklenen kaynak |
|---|---|---|
| `OTD` | sipariş satırı: söz verilen teslim tarihi; teslim alma satırı: alım tarihi, bağlı sipariş satırı | MOD-0141 PO, MOD-0142 GRN |
| `QTY_ACC` | sipariş satırı: sipariş miktarı + birim; teslim alma satırı: alınan miktar + birim | MOD-0141, MOD-0142 |
| `QUAL_ACC` | kalite muayenesi: muayene edilen miktar, kabul edilen miktar | **kaynak modül belirsiz** — hangisi? |
| `INV_MATCH` | fatura: 3'lü eşleşme sonucu (istisnasız geçti / istisna) | MOD-0143 |
| (5 satır kuralı) | dönemdeki teslim alma satırı sayısı | MOD-0142 |

Süzgeç: kiracı (token), tüzel kişilik (`X-Legal-Entity-Id`, MDM ile doğrulanmış), **kiracı düzeyindeki tedarikçi ana kaydı
kimliği** (MOD-0140 1.3.0), dönem `[from, to)`. Çağrı Gateway üzerinden, kullanıcının token'ıyla; okuma izni Rol → İzin ile
verilir (BL-652 ara çözümü ve WP-P1 G5 ile aynı desen).

## Sorular

1. Bu alanları bugün okuyan uçlar var mı (hangi sözleşme, hangi sürüm)? Yoksa salt-okunur bir özet ucu (tedarikçi + dönem →
   satırlar ya da hazır sayılar) eklenebilir mi, hangi sürümde?
2. Kalite muayenesinin (QUAL_ACC) kaynağı hangi modül ve kimin?
3. Okuma izni anahtarları ne olacak (`procurement.*.read`)?
4. Tedarikçi kimliği: 1.3.0 sonrası satırlar ana kayıt kimliğini mi taşıyacak, LE başına ilişki kimliğini mi?

## Mesaj metni (Natig gönderir)

```text
Cihan merhaba — MOD-0147 (tedarikçi performans karnesi) v1'i başlattık (Natig'in kararı, 2026-10-22). v1'de metrik
değerlerini değerlendirici giriyor; v1.1'de bunları Procurement verisinden hesaplamak istiyoruz. Bunun için yalnız okuma
gerekiyor (XMC-001), hiçbir Procurement kaydına yazmıyoruz.

Gereken alanlar, tedarikçi + LE + takvim ayı için:
- OTD: sipariş satırının söz verilen teslim tarihi ve teslim alma tarihi (MOD-0141 / MOD-0142)
- Miktar doğruluğu: sipariş miktarı ve alınan miktar, birimiyle (MOD-0141 / MOD-0142)
- Kalite kabul oranı: muayene edilen ve kabul edilen miktar — kaynak modül hangisi?
- Fatura eşleşme oranı: 3'lü eşleşmenin istisnasız geçip geçmediği (MOD-0143)
- Dönemdeki teslim alma satırı sayısı (en az 5 satır kuralı için)

Sorular: (1) bu alanları okuyan uçlar bugün var mı, yoksa salt-okunur bir özet ucu eklenebilir mi, hangi sürümde?
(2) kalite muayenesinin sahibi kim? (3) okuma izni anahtarları ne olacak? (4) 1.3.0 sonrası satırlar kiracı düzeyindeki
tedarikçi ana kayıt kimliğini mi taşıyacak?

Ayrıntı: docs/roadmap/plans/mvp6-mod0147-v1-scorecard-01/READ-CONTRACT-REQUEST-PROCUREMENT.md (push edilince okunur).
Acele yok; v1 senin S1'inin (1.3.0) main'e girmesini zaten bekliyor.
```
