# FE capture — bağımsız ön kaynak incelemesi

Sonuç: UYGUN DEĞİL — aşağıdaki üç mekanik açık dondurulmadan önce giderilmeli. Bu, unfrozen taslak için SOURCE REVIEW sonucudur; actual producer pozitif/negatif çalıştırma, ürün kabulü veya dağıtım kararı değildir.

İnceleyen /root/pvg_dl12_be_prompt_review_01; CT yazarı ayrı root oturumudur. İstenen inherited gpt-6.2-astra/high; gerçek model/effort verilemiyor. İncelenen capture 12.454 bayt, raw/LF SHA256 8c13290cc878866615724572bf300888b720f7e35b55bfdf25962de2bec72716. Baseline runner v3 raw/LF SHA256 6ec9b196cee59a394b115285128db686b3b1ce8926118b427e3994ae592410dc. Repo okuması H ecbcecd9342759c5b3fc4ae0ea48ba2c44cd2aa2 üzerindedir.

Şema ve korunan doğru taraflar

Baseline v3:240 çıktısı capture:45–86 ile yapısal uyumludur: kind/schema_version/source_ref/acceptance_passed, frozen_paths, üç suites girdisi ve compiled_sources listeleri; javascript json_path/json_lf_sha256/case_names/case_count alanları eşleşiyor. Closure girdileri path/raw_sha256/lf_sha256/mtime_ns içeriyor. FE6+BE3+RES7 olmak üzere16 frozen yol, gerekli compile minimumları ve source_ref'in base atası olması kontrol ediliyor.

Altı yazma yolu FE-READINESS ve rev21 sınırlarıyla aynıdır. Korumalı baseline hash'i, committed artefaktların LF hashleri ve case multisetleri, Web62'nin korunması, API/Reg exact baseline kimlikleri, JS34'ün korunması, actual altı CLI ve tüm sonuçların PASS olması iyi mekanik kapılardır. Capture sayım/adın gerçek no-loss, token, shape, corpus, metot veya cleanup kanıtı olmadığını açıkça söylüyor; bu sınır korunmalı.

BASELINE_LF_SHA256=None bilinçli unfrozen kapıdır, bu incelemenin bulgusu değildir. Baseline ölçümü bitmeden gerçek pozitif producer sonucu beklenmez. Doğru pre-dispatch kanıtı mevcut gerçek GREEN run'ların absent yeni adlar/altı-diff nedeniyle producer acceptance RED vermesi, ardından açık yapısal bozulma negatifleridir. Actual feature sonrası gerçek pozitif0 ayrıca ölçülür. Sentetik guard fixture pozitifleri ancak sentetik diye adlandırılabilir; no-loss/E2 sayılmaz.

Engelleyici bulgular

F-1 — Assertion kontrolleri optimize Python'da kapanır. Capture:42,47–86,96,100–106 yalnız assert kullanıyor; baseline v3:10'daki __debug__ kontrolü yok. PYTHONOPTIMIZE veya -O ile pin/schema/ancestry/frozen/input/assembly sınırlamalarının önemli bölümü yürütülmez. Sondaki passed ifadesi bu kontrollerin yerine geçmez. Öneri: argparse/artefakt okumasından önce optimize modu açık RuntimeError/başarısız çıkışla reddet; kritik doğrulamalar en azından bu fail-closed girişe bağlı olsun. Sonraki actual negatif: PYTHONOPTIMIZE=1 ve -O, çıktı PASS/acceptance artefaktı üretmemeli.

F-2 — Zorunlu test adı eşlemesi tam ad değil. Capture:125 C# için contains-prefix, :131 JS için substring kullanıyor. Örneğin canonical C# adının sonuna _not_the_required_test eklemek veya JS başlığına disabled example eklemek required test olmadan True verir. Bunlar yalnız Python saf ifade örnekleriyle ölçüldü; producer veya tam suite çalıştırılmadı. Öneri: C# metot bölümünü parametrelerden ayırıp class+method adını tam eşleştir; JS assertionResults.title'ı tam beklenen title veya önceden belgelenmiş, sınırı belirli parametre suffix'i ile eşleştir (fullName baseline multiset'i ayrıca kalsın). Sonraki negatif: yalnız bogus prefix/suffix taşıyan bir testi gerçek gerekli vaka saymamalı. Ad eşitliği davranış kanıtı değildir, fakat named coverage bayrağı doğru olmalıdır.

F-3 — Pratik dondurma engeli: tekrarlanan Git süreçleri

Capture:60–62 frozen yollarda32 git-show spawn; :79–81 compile closure için2×ΣN spawn çalıştırıyor. Web SDK projesi varsayılan girdilerinde, bin/obj/node_modules hariç, 678 .cs+1075 .resx+1590 .cshtml+1 csproj=3344 statik aday; Web.Tests263 .cs+1 csproj=264 aday ölçüldü. Yalnız bu3608 aday closure'a girerse7216 tekrar Git süreci doğar; bağımlılıklar ve diğer suite'ler buna eklenir. Bu actual evaluated closure sayısı veya ölçülmüş duvar saati değildir; actual closure henüz bu incelemede yayınlanmış görülmedi. Materyal performans riski nedeniyle dondurmadan önce batching gerekir; bu, tek başına yanlış kabul veya ölçülmüş timeout bulgusu değildir. Öneri: (ref,path) ve blob kimliğiyle cache; her ref için ls-tree ve cat-file --batch üzerinden ham blobları toplu oku, canonical LF hashini bellekte hesapla. Aynı yolu iki ref/suite döngüsünde ayrı subprocess ile tekrar okuma. Raw blob kimliği ile normalize LF kimliği ayrımı korunmalı.

Kanıt sınırı — same-run bağının gücü

Capture:87–90 güncel kaynak hashlerini kaydediyor; :102–110/:133–137 ilişkiyi mtime/storage/DLL hashleriyle sınırlıyor. Bunlar actual run sırasında ölçülmüş kaynak before/after hashleri değildir. Ancak bu incelemede gerçekçi normal mtime'larla sınırları belli bir eski-artefakt karşıörneği çalıştırılmadı; yalnız kasıtlı backdate varsayımı capture'a yeni engelleyici requirement koymak için yeterli kabul edilmedi. Bu konu yukarıdaki üç blocker'a dahil değildir. Independent fresh E2 ve işçinin gerçek kaynak-before/after/DLL/TRX hash/actual argv kaydı ayrı zorunlu kanıttır; capture tek başına bunların yerine geçmez.

İstenirse en küçük run-receipt şeması: command argv/cwd/start/end/exit; aynı komut öncesi/sonrası source closure raw+LF hashleri; produced/runtime DLL hashleri; TRX/JS ham hashleri. Capture bunları mevcut dosyalar ve kayıtlı zaman aralığıyla karşılaştırabilir. İşçinin yazdığı imzasız receipt kötü niyetli uydurmaya karşı kriptografik kanıt değildir; yalnız tutarlılık bağıdır. Yeni bir receipt kapısı eklenirse gerçek koşudan türetilmeli, eski kanıtın üzerine sonradan başarı varsayımıyla üretilmemeli.

CT'nin son bildirdiği baseline attempt API456 PASS sonrasında helper child-cleanup nedeniyle altyapı exit1 vermiş ve BASELINE üretmemiştir. Bu bilgi incelemede yeni bir actual run olarak ölçülmedi; şema uyumu yukarıda kaynak düzeyindedir. Gerçek baseline/pin ve actual producer validation hâlâ beklenir.

Sonraki gerçek producer kontrolleri

Baseline/pin tamamlanınca baseline gövdesi ve bağlı TRX/JS/hash/source_ref/frozen path bozulmaları, eksik veya yinelenen case, non-PASS/skip, yanlış storage, eski assembly/run, kapsam dışı dosya, frozen BE/RESX değişimi ve altı CLI'dan birinin gerçek başarısızlığı ayrı RED olmalı. İlk gerçek baseline acceptance RED'i ile bozuk girdiye RED ve ileride gerçek feature positive0 farklı kayıtlar olarak tutulmalı. Bu incelemede kaynak/runner/metadata/test dosyası değiştirilmedi; tam suite veya capture yürütülmedi. Yalnız bu dış inceleme kaydı ve kaynak manifesti yazıldı.
