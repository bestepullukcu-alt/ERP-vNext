**WP-0230-DL12-BE v1.1 kısmi RED aşamasında.** Eski ürünle 3 test metodu derlendi ve her biri InMemory ile MongoDB üzerinde çalıştı. **6 satırın tamamı** yetkili GET yanıtında `intakeChannel` bulunmadığı için assertion’da durdu. Atlanan test sayısı 0. Alan reddi testinde pozitif kontrol, geri yazma testinde ise PUT gövdesini hazırlayan kontrol durdu. Bu koşum alan reddini veya kayıpsız güncellemeyi doğrulamıyor. [Ham TRX](C:/Users/epenc/AppData/Local/Temp/pvg-dl12-be-evidence/794e3ff01cc14fe2b32ef2f81a9aa102/red/old-contract.trx)

Testin yerel sınırını ve temizliğini doğruladım. MongoDB 7.0.43, PID `27484` ile `127.0.0.1:59362` üzerinde özel veri dizininde çalıştı. Başlatılan sürecin kapandığı ve dizinin kaldırıldığı ölçüldü. İlk TRX’in `9d26cc1e…` özeti korundu. Yeni TRX SHA-256 değeri `63008fd63606de0e1fd196c96a69990434501082a20047266c887d659fc3c3b6`. [Komut, süreç, kaynak, DLL ve TRX manifesti](C:/Users/epenc/AppData/Local/Temp/pvg-dl12-be-evidence/794e3ff01cc14fe2b32ef2f81a9aa102/manifest.json)

Yerel ve sentetik kapsam doğrulandı. Yetki kontrolünü kaldırıp korunan kayıtların açığa çıkmasını yeniden üreten çalıştırılabilir mutantları hazırlayamıyorum. v1.1’in zorunlu kanıt zinciri bu nedenle açık. Ürün değişikliği, tam GREEN, CT kabulü ve yerel commit aşamalarına geçilmedi.

| Son ölçüm | Değer |
|---|---|
| Dal | `feature/pvg/mod-0230-dl12-be` |
| Başlangıç ve son HEAD | `ab039c2c53d82edf8413bae4e3304d21b57e04e4` |
| HEAD tree | `47c42a86d940992fca0f063f18c3667a3b23342f` |
| Çalışma ağacı | Yalnız [test dosyası](C:/Users/epenc/.codex/worktrees/pvg-dl12-prep-win-r2/ERP-vNext-git/services/Diten.PvgService/tests/Diten.PvgService.Api.Tests/PvgCaseIntakeEditKeepsStoredDataTests.cs) `??`; indeks boş |
| Gerçek model / effort | Çalışma zamanı ayarı doğrulanamıyor |
