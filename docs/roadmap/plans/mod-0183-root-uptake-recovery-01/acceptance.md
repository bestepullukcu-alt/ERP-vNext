# Acceptance — tamamı future/unexecuted

| ID | Test / exact oracle |
|---|---|
| R01 | Raw missing vs explicitnull vs malformed string vs Int32 vs nil UUID vs valid UUID ayrı fixtures. Presence typed materialization öncesi ölçülür. İlk4 explicit null; nil/valid kendi UUID değerleri. Geçerli fixture provenance kontrollüdür. |
| R02 | Missing raw alan yeniden serialization ile storage'a eklenmez; null/invalid raw değerler değişmez. İlgisiz corrupt field mevcut error davranışını korur. |
| R03 | Normal create R commit; raw aggregate + create history/audit/outbox/receipt aynı root/identity zinciri. GET A/B body root R; response trace A/B. |
| R04 | Başarılı detail alanı her zaman içerir; list/mutation/POD alan kazanmaz. Optional nullable contract ile schema check; property omission negatif kontrolü başarısız. |
| R05 | Valid headers: anonymous/invalid JWT401; missing read grant403; consumer-create-only403; read200. Invalid correlation mevcut400 precedence; nil header policy değişmez. |
| R06 |2tenant×2LE, foreign/deleted/missing404; scope header/claim ve query override mevcut davranışları; root sızıntısı yok. |
| R07 | Tek scoped raw read; concurrent activity ile root/detail version splice yok. Filter mapping registered serializer/_id ile eşleşir. |
| R08 | Repeated GET sonrası scoped aggregate/root presence, receipt/history/audit/outbox/source state eşit; query hatası sıfır sayı sayılmaz. |
| R09 | Aynı isolated data ve binary ile iki ayrı API OSprocess restart; missing/null/nil/valid/invalid sonuçları ve raw storage korunur. |
| R10 | Existing same-root lifecycle/POD/replay/atomicity/SourceIntake regression changed-build üzerinde geçer; root/write/serializer yollarına diff yok. |
| R11 | Aynı raw UUID fakat farklı fixture geçmişi: parser iki geçmişi ayırt etmiş sayılmaz. Bilinmeyen/disputed rollout disposition yokluğu deployment kapısını açık bırakır; isolated test başarısını etkilemez. |
| R12 | Source/binary/process hash zinciri, sent/received bytes, exact owned diff, ilgili architecture sonuçları ve bağımsız VER manifesti. |

Henüz test yürütülmedi. Eski73 kontrol veya altı BSON koşusu yeni kanıt olarak devralınmadı.
İzole test: explicit ayrı Mongo replica set, DB-010 uyumlu sabit test DB adı, scoped tenant/LE cleanup; operasyonel DB/credential yok.
Restart mock re-instantiation değildir. Tüm hata/eksik ölçümler raporlanır; historical external failures waiver sayılmaz.
Target dataset rollout review separately proves origin or records unresolved unknown/conflict. No automatic production scan.
