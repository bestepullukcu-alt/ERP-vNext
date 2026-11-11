---
status: approved
decided: 2026-10-27
module: MOD-0230
decisions: OD-PVG-6
---

# PVG-MOD0230-ODPVG6-01 — Runtime bayrağının onay kaydına bağlanması (OD-PVG-6)

> **Karar sahibi:** sahip, Enterprise Architecture sıfatıyla (`docs/records/decisions/2026-10/pvg-mod0230-mimari-sahibi-01.md`, MIMARI-01). Sahibin sözü: "bayrak-evet" (2026-10-27, PVG Control Tower sohbeti).
> **Kaydı tutan:** PVG Control Tower.

## Önceki durum

- Paket revizyon 10, satır 173 (OD-PVG-6): PVG tarafı CI taramasını 2026-10-21'de kararlaştırdı. Bu tarama WP-0230-DL3B ile `main`'de (#234).
- Bekleyen kısım: canlı çalışma onaylandığında bayrağın onay kaydına nasıl bağlanacağı. Bu karar Enterprise Architecture'ındı.
- AC-10 bu bağı ölçüyor: üretim benzeri bir açılışta `Pvg:Runtime:OperationalRuntimeAuthorized=true` ise ve onay-kanıt bağı sağlanmıyorsa açılış reddedilir. Bunu bir test kanıtlar.

## Onaylanan bağ biçimi

1. `Pvg:Runtime:OperationalRuntimeAuthorized=true` ise host bir onay kaydı numarası da ister: `Pvg:Runtime:ApprovalReference`. Değer, depodaki operasyonel runtime onay kaydının kimliğidir.
2. Bu değer yoksa, boşsa ya da yalnız boşluktan oluşuyorsa üretim benzeri açılış reddedilir. Bayrak tek başına yetmez.
3. Otomatik bir kontrol (CI), commit edilmiş bir yapılandırmada geçen her `ApprovalReference` değerinin depodaki onay kayıtlarında gerçekten bulunduğunu ölçer. Bulunmayan bir değer kırmızı verir.
4. Kaydın tam biçimini, kontrolün hangi dosyayı okuyacağını ve testlerin adlarını bu bağı uygulayan iş paketinin prompt'u yazar. Bu iş DL-3'ün AC-10 işidir. Bu kayıt yalnız yukarıdaki üç kuralı bağlar.

## Sonuç

- AC-10 artık OD-PVG-6'yı beklemiyor. DL-3'ün son işi yazılabilir; o iş bitince DL-3 dilimi tamamlanabilir.
- Paket metni (satır 173 ve AC-10'un "after OD-PVG-6 …" ifadesi) bu onayı henüz göstermiyor. Paket, OD-PVG-2 ve OD-PVG-7 onaylarıyla birlikte tek bir revizyonla güncellenir. O revizyon onaylanmadan AC-10 prompt'u yazılmaz.

## Gerileme değerlendirmesi

Bu kayıt kod değiştirmiyor. Bugün bayrak yalnız testlerde açılıyor ve canlı kapı kapalı; kurallar uygulandığında bile kullanıcıya bir etkisi olmaz.
