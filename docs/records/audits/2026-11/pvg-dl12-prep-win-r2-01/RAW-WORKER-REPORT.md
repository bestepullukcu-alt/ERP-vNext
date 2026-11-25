**WP-0230-DL12-PREP-WIN v2.0 için Windows doğrulaması geçti. Yerel commit, otomatik onay incelemesinde engellendi.**

Tam API süiti çıkış `0` ile **395/395 başarılı, 0 başarısız, 0 atlanan** sonuç verdi. Üç hedef test ve 35 Mongo testi geçti. CT kanıt üreticisi `acceptance_passed=true` döndürdü. TRX SHA-256 `259115c0c5503edf40a3088f9d8a26bd1d70f8f249f7e9ba1646ba567d72ead6`. [Komut ve araç sürümleri](C:/Users/epenc/AppData/Local/Temp/pvg-dl12-prep-win-r2-evidence/normal/command.json) · [TRX](C:/Users/epenc/AppData/Local/Temp/pvg-dl12-prep-win-r2-evidence/normal/pvg-prep-win-r2-api.trx) · [CT üretici çıktısı](C:/Users/epenc/AppData/Local/Temp/pvg-dl12-prep-win-r2-evidence/normal-capture.json)

Yedi mutantın her biri ayrı kopyada derlendi. Her derleme çıkışı `0`, her hedef test çıkışı `1` oldu. Aynı testlerin özgün sürümleri tam süitte geçti. [Assertion, kaynak hash’i ve TRX çiftleri](C:/Users/epenc/AppData/Local/Temp/pvg-dl12-prep-win-r2-evidence/mutation-results.json)

| Mutant | Kırılan kontrol |
|---|---|
| G1 / G2 | Windows’ta `--nounixsocket` bulunmaması / Unix argümanlarında bulunması |
| G3a / G3b | Göreli ikili yolun / mevcut olmayan mutlak yolun reddi |
| G4 | `27017` adresinin reddi |
| G5 | Özel fixture dizininde listelemenin `UnauthorizedAccessException` vermesi |
| G6 | Gerçek scanner’ın aynı erişim reddini bildirmesi |

Windows ACL testi, kilitleme öncesi ve geri yükleme sonrası erişimi ve özgün DACL eşitliğini doğruladı. İki ACL mutantında da beklenen assertion hatasından sonra fixture temizlendi. Dört replica set ve bir standalone Mongo süreci beş ayrı loopback portunda çalıştı. Beş PID kapandı ve her sürecin özel veri dizini kaldırıldı. [PID, port ve dizin kayıtları](C:/Users/epenc/AppData/Local/Temp/pvg-dl12-prep-win-r2-evidence/normal/processes.json)

`feature/pvg/mod-0230-dl12-prep-win-r2` dalında başlangıç ve son HEAD `3b5cf01df7def7cc3f4eb00a664dba04f46d3e0a`. Üç izinli dosya staged durumda. İlk ikisi donmuş yamayla eşleşiyor. Üçüncü dosyada yalnız onaylı test gövdesi değişti. Tam staged fark incelendi, `git diff --cached --check` çıkışı `0`. Staged tree `addfabdae2d2a0e415b4ce77383bbc806ca9f59a`. Model `gpt-6.2-astra`, gerçek effort görünmüyor. Bağımsız E2 kararı CT’de, Unix çalışma kanıtı son PR’da bekliyor. [Kanıt manifesti](C:/Users/epenc/AppData/Local/Temp/pvg-dl12-prep-win-r2-evidence/manifest.json)

Otomatik inceleme, commit’in yerel dal geçmişini değiştireceğini ve CT iş emrindeki yetkiyi doğrudan kullanıcı onayı olarak doğrulayamadığını bildirdi. **Bu üç staged dosyayla, yalnız belirtilen işçi dalında tek yerel commit oluşturmamı onaylıyor musunuz?**
