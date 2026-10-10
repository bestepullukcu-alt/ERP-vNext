# UI acceptance — bütün runtime satırları NOT RUN / dispatch HELD

| ID | Exact kontrol | Beklenen sonuç / kanıt |
|---|---|---|
| U01 | Gerçek Auth login→MVC→Gateway→Loads list | HTTP200 unwrapped items/total/v1; request/response UUID, redacted server-hop kanıtı; diagnostic JWT gerçek Auth kabulü sayılmaz |
| U02 | No session; session var read yok; read var create yok | 401 kabuksuz açıklama;403 tenant shell içinde tek açıklama/remediation, başlık/table/buttons/skeleton yok ve redirect yok; read-only listede create yok. Doğrudan POST ayrıca server403 |
| U03 | Missing/duplicate signed LE, tenant/sub veya permission; foreign tenant/LE header | Published annex önceliği: authenticated unusable claim403, cross-scope404; client scope fallback yok; body/query scope injection400; gerçek Auth claim eksikliği OPEN kalır |
| U04 | Geçerli Carrier+Shipment IDs, mode/date, iki ordered stop ile create |201 Draft, loadId/number; reload GET gerçek satır. Formdaki inputlar API'ye exact aktarılır, yeni field yok |
| U05 | Kayıp response / çift tıklama / aynı key+body+root retry | Tek load/assignment grubu/receipt/audit/Pending event; replay201 idempotentReplay=true; success sonrası reload, original snapshot ile current state overwrite yok |
| U06 | Farklı root+aynı key; aynı root+değişen valid body |409 CORRELATION_ROOT_MISMATCH önce; sonra409 IDEMPOTENCY_KEY_REUSED. UI teknik root tahmin etmez/otomatik key çevirmez |
| U07 | Unknown carrier/shipment; assignment conflict; duplicate shipment/stops/mode/eligibility |404 CARRIER_NOT_FOUND/SHIPMENT_NOT_FOUND,409 SHIPMENT_ALREADY_ASSIGNED,422 annex code'ları ayrı localized remediation; form inputları kaybolmaz |
| U08 | Malformed dependency; insufficient reference; dependency outage; unknown commit |502 DEPENDENCY_RESPONSE_INVALID;503 REFERENCE_STATE_UNAVAILABLE/DEPENDENCY_UNAVAILABLE/PERSISTENCE_UNAVAILABLE ayrımı. Unknown commit “rollback” diye sunulmaz; aynı-intent retry, kör yeni create yok |
| U09 | Summary alanı absent, null/invalid list envelope, empty success, delayed response | Absence “sağlanmadı”; malformed response açık hata; yalnız gerçek items=[] empty state; loading/error/empty ayrı; sonsuz skeleton yok |
| U10 | Status filter, UUID carrier filter, local search/sort/paging, SaveView/Reset/ColReorder | API'ye yalnız status/carrierId; çoklu status/page/search/sort uydurulmaz. Factory Reset tüm tablo state'ini resetler; shared personalization kullanılır |
| U11 | 7 dil en,tr,fr,es,zh,ar,ru | Anahtar/İngilizce placeholder/fallback yok; enum+error labels, validation, denial, dates, toolbar ve modal çevrilir. Module GetAllStrings(true) JSON bridge + required SharedResource keys, PascalCase loader |
| U12 | ar RTL; 1440/1024/768/390 CSS px, DPR kayıtlı | Sayfa horizontal overflow yok; nested stop editor, popup z-index, keyboard focus/escape, offcanvas, UUID LTR izolasyonu ve uzun çeviriler okunabilir |
| U13 | Required parity ve lexical sınırlar | carrier UUID, shipment≥1, stops≥2/sequence; empty locationReferenceId string kabulü korunur. Offset instant aynı; nil/lexical/business ayrımı annex'e uygun, yeni maxlength/nonempty/date yasağı yok |
| U14 | Persist/restart/replay | İzole DB'de beş owned collection before/after, process PID/binary hash değişimi, durable aynı-key retry tek result, outbox Pending; diğer domain dataları değişmez |
| U15 | Existing row lifecycle button/root acquisition | Bu dilimde transition düğmesi/route yok. Root read seam kapanmadan next-slice PASS yok; manual root entry/local cache workaround yok |
| U16 | Gerçek producer ve multi-Shipment flow | LIVE-185/root owner disposition ve bağımsız composed evidence ayrı. Mock-based U04 PASS yalnız SIMULATED; E5/G5/live acceptance sayılmaz |
| U17 | Regression/protected paths | Shipment/Carrier sources byte unchanged; ilgili UI/API regression sonuçları ayrı. Canonical/guard/pack/gateway UI writer tarafından değişmez |
| U18 | Fresh source→binary→process→browser | Exact owned+shared hashes, build command/output, DLL/process/cwd/port, browser URL ve timestamp; resx rebuild sonrası yalnız lane-owned restart ve hard reload |
| U19 | Screenshot/evidence/cleanup | Yalnız desteklenen PNG export/save; yoksa OPEN. Token/cookie/secret redacted; test-owned DB/process cleanup, operasyonel27017 yok, tarihsel kanıt rerun diye gösterilmez |

Future doğrulama: `verify_datatable_page.py . --area SupplyChain --module Loads --reference slim`; module-specific Web controller/form/JS testleri, real-browser her görünür control click; bağımsız VER ayrı snapshot/DB/port. Bu hazırlıkta build, browser veya persistence testinin geçtiği iddia edilmez.
