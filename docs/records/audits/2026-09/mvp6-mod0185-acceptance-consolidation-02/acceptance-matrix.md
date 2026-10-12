# A01–A12 konsolide kabul

|AC|Önceki karar|Erişilebilir fresh destek|
|---|---|---|
|A01|CT01 PASS|runtime.json + runtime-http/processes; üç Loads operasyonu ve Loads TRX|
|A02|CT01 PASS|Fresh Loads contract TRX; geniş132 sonucu tarihsel|
|A03|CT01 PASS|LoadIsolationTests + scoped HTTP/mock kayıtları; gateway yok|
|A04|CT03 CLOSED korunur|failure-paths + independent-query + process/binary:2 senaryo,5 koleksiyon,20 negatif|
|A05|CT01 PASS|Lifecycle testi ilk FAIL, hedefli recheck PASS; iki kayıt birlikte|
|A06|CT01 PASS|runtime/restart + replay recheck; orijinal sonuç ve kaynak tekrar okumama|
|A07|CT01/CT03 CLOSED korunur|atomicity recheck +3 startup fail-closed ve recovery health200|
|A08|CT01 PASS|reference/concurrency/contract Loads testleri; yeni iş kuralı yok|
|A09|CT01 PASS|restart.json + iki farklı PID + scoped Mongo gözlemi; Pending outbox|
|A10|CT01 bounded PASS|GET-only dependency mock capture; live Producer entegrasyonu değil|
|A11|CT01 PASS sınırı korunur|Fresh restore/build +33benzersiz testin son sonucu;132/architecture tarihsel|
|A12|CT01/CT03 CLOSED korunur|runtime-http294/0/294;18verifiercheck +5fresh negatif|

Her satır onaylı backend Loads iş paketiyle sınırlıdır. VER-03 sonuçları bu turda yeniden çalıştırılmadı; raw kayıtları ve hash bağları incelendi.
