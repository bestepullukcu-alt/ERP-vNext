# MOD-0220-FU01 — kısa kod kilidinin anı (AK-1) için sahip kararı

tarih: 2026-11-10
karar veren: sahip, bu oturumda kendi tıklamasıyla (soru kartı)
kayıt eden: CT · Ürün ana verisi (mdm-ct-02)

## Bağlam

N-12 sahip kararı A (2026-11-10, CT · Destek sohbeti): "A, kalıcı kilit" — bir şirkete ilk kayıt kodu verildikten sonra kısa kodu MDM'de değiştirilemez ve başka şirkete verilemez. Paket yazarının ölçümü (paket 8afcb483, rev 1): Platform kod ayırmada olay yayımlamıyor ve "kod verildi mi" diye sorulacak bir iç ucu yok; MDM ilk ayırma anını bugün bilemiyor. Platform yalnız etkin, başvurulabilir ve kısa kodu `^[A-Z]{2,5}$` kalıbına uyan şirkete kod veriyor.

## Seçenekler

1. Kod alabilir olunca (önerilen): şirket kod alabilir hale geldiği anda kilit; Platform'a dokunulmaz; etkinleşmiş ama hiç kod almamış şirket de kilitlenir.
2. Tam ilk kodda: Platform'a iç uç (Beste'nin modülü, ayrı XMC-001 işi, sahip + Beste izni); kilit o iş bitmeden çalışmaz.

## Karar

"Kod alabilir olunca (Önerilen)".

## Sonuç

- AK-1 kapanır: kilit anı, şirketin kayıt kodu alabilir hale geldiği ilk andır. Bu, N-12 kararının bilinçli ve daha sıkı bir uygulamasıdır.
- Platform'da değişiklik gerekmez; Repo Scope'a Platform dosyası girmez.
- Etkinleştirmeden önce yanlış kısa kod düzeltilmelidir; kilitli kodun açılması v1'de yoktur (AK-6 ayrı).
- Bu karar kod, commit, push ya da dağıtım izni değildir.
