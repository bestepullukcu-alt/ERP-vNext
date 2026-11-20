# PVG Control Tower — devir ve DL-12 hazırlığı

Tarih: 2026-11-20. Kanıt düzeyi: E1; CT kararı: BLOCKED (uygulama dağıtımı).
Ölçüm ağacı: `bee69c93c9eb8bb3f2cc5e06fa8730abe9600fcc`. Ana dal: `2de950edcaeb9d8a153465e5f29d7c71eeb31144`.
Bu kayıt bir kabul raporu değildir; WP Done / Module Done / Capability Done / Release Done verilmedi.

## Yapılanlar

1. Sohbetin ilk proje klasörünün Git checkout olmadığı ölçüldü; o kopya değiştirilmedi.
2. Gerçek repo kaynağı `ERP-vNext-git` bulundu. GitHub CT ucu `01771794038151250f3659b1087f62247a9a177c` devirdeki uçla eşleşti; main ve CT referansları fetch edildi.
3. Codex'in `create_worktree` aracıyla bu sohbet için izole `pvg-ct-handoff` alanı kuruldu; CT dalı `chore/ct/pvg-ct-01` izlendi. Main bu CT dalına doğru yönde, çakışmasız merge edildi. Ağaç yazım öncesi temizdi. `git rev-list --left-right --count HEAD...origin/main`: 53 ileri, 0 geri.
4. Revizyon 16 Git blob özeti onay sorusundaki özetle eşleşti. Kullanıcının doğrudan ikinci onayı `docs/records/decisions/2026-11/pvg-mod0230-rev16-beste-onayi-01.md` dosyasına kaydedildi.
5. Strict salt okunur bağımsız hazırlık denetimi `pvg_dl12_readiness` alt ajanıyla yapıldı; rapor E1, test veya kod mutasyonu yok. Başlangıç ve son HEAD/status aynı. Kaynakta istenen Claude 5.5 modelleri bu araçta yok; bu inceleme Codex oturum modelini devraldı ve Claude kullanıldığı iddia edilmedi.
6. Resmi GitHub CLI v2.105.0 geçici araç klasörüne indirildi; resmi varlık digest'iyle ZIP SHA-256 eşleşti (`8813a72c899900afaa10e3fec8fbc64b908c89dbfe837e95ed2fa500fc140627`). CLI oturumu yok; resmi tarayıcı giriş akışı başlatıldı, kullanıcının tamamlaması bekleniyor.
7. `ct_queue.py guard --queue pvg`: 35 satır, yazım öncesi dosya özeti `456246b42d6f6fda646b47bf55fb8250ef5ef3930e8421fdf5da7ad52290975d`.

## Dağıtım hazırlığı ve kapılar

| Kapı | Ölçüm / kanıt | Sonraki işlem |
|---|---|---|
| GitHub oturumu ve yazar hesabı | `gh api user` ve `gh api rate_limit` giriş gerektiğini bildirdi. Yerel git yazarı farklı bir hesaba ayarlı; o değer GitHub kimlik kanıtı değildir. | Resmi gh girişini tamamla; hesap adını ve kotayı ölç; yazar/PR hesabını mevcut sözleşmeyle tutarlı belirle. |
| Onay ve inceleme satırları | Onaylı tam paket hash'i için approvals/reviews satırı yok; `pack_approval.py:1254,1660-1678` eski raporda olmayan makine biçimini ister. | Gerçek bağımsız inceleme kaydını üret ve commit et; yalnız araçtan üretilen satırları append et. Uydurma oturum/model/yazar yazma. |
| Windows Mongo test başlangıcı | `PvgMongoTestHarness.cs:159-176` koşulsuz `--nounixsocket`; yerel Windows mongod 7.0.43 `--nounixsocket --version` çağrısında `unrecognised option '--nounixsocket'` ile exit 2 döndü. | Test yardımcısında yalnız Unix'te bu seçeneği ekleyen dar değişiklik önerilir. Dosya DL-12 izinli yollarında değil: kapsam/onay kapısı tamamlanmadan değiştirme. Alternatif: ölçülmüş uygun Linux test ortamı. |
| Python araç bağımlılığı | İlk ölçümde openpyxl yoktu. Sonra requirements-tools.txt sürümleri resmi PyPI'dan geçici, izole araç klasörüne kuruldu; openpyxl 3.1.5 ve et_xmlfile 2.0.0 import ile doğrulandı. | Tamamlandı; kapılarda bu araç klasörünü PYTHONPATH ile ekle, Windows'ta python kullan. |
| Prompt ortamı ve sürüm bağı | v1.1 satır 3 Mac yolları + EXPECTED_HEAD_TBD; sabit Mac mongod ve uptime komutları; prompt incelemesi `prompt_gate.py:1176-1206` başlık/hash/kaynak paketi bağı ister. | Kaynak v1.1'i koru; ölçülen Windows yolları, komutları ve şeffaf model seçimiyle yeni dağıtım sürümü hazırla; bağımsız incelet. |

## Koşuların sınırı

`ct_queue.py validate --queue pvg` tek sefer başlatıldı; süreçten tamamlanmış sonuç alınamadı, sonraki oturum sorgusu sürecin artık bulunmadığını bildirdi. Çıkış kodu, tamamlanma ya da PASS iddiası yoktur. Aynı shell'de arkasına konulan `pack_approval.py card` için tamamlanmış çıktı yoktur. Kota ölçülemediği için `prompt_gate.py check` başlatılmadı. Ürün süitleri, mimari süit ve vitest bu turda çalıştırılmadı.

Windows çalışma kopyasında CRLF Git blob'undan farklı dosya özeti üretir. Sürüm kimliği için Git blob özeti kullanıldı. Onaylı paket ve kaynak prompt checkout'un özgün CRLF baytlarıyla korunur; Git blob'larıyla içerik farkı yoktur. Onaylı paket metni veya kaynak prompt sözleşmesi değiştirilmedi.

## Güvenlik ve kapsam

Otomatik güvenlik incelemesi, Git kimlik yardımcısından token alıp gh API çağrısında kullanma komutunu reddetti: bu kimlik kaynağından token alınması özel olarak yetkilendirilmemişti. Komut çalışmadı; aynı yöntem yeniden denenmedi, token dosyaya/rapora yazılmadı. Daha güvenli resmi `gh auth login --web` yolu başlatıldı; mevcut Git kimlik kurulumu değiştirilmedi.

Ürün kodu, test yardımcısı, onay/inceleme defterleri ve protected paths değiştirilmedi. Push/PR, işçi dağıtımı, main'e yazma, canlı kapıyı açma, izin verme, eski dal/worktree silme yok.

## Sıra

GitHub oturumu → gerçek inceleme ve onay defteri → Windows test ortamı kapsam kararı → yeni prompt ve bağımsız inceleme → kota >=1000 → tek dağıtım kapısı → izole DEV → ayrı VER → CT entegrasyonu. DL-12'nin E2 entegrasyonu E4 canlı kabul yerine geçmez; KK-8 ve G-23 korunur.

Kuyruğa DL-12 yalnız PLANNED olarak eklenir. Bir işi iki yazıcıya dağıtmak veya bağımlılıkları atlamak yerine hazırlık sıralıdır.

## Yerel kayıt durumu

İlk hazırlıkta staging komutu otomatik güvenlik incelemesinde reddedildi: bu oturumda açık kullanıcı onayı bulunmadığı belirtildi. O komut çalışmadı; onay gelene kadar yeniden denenmedi. Ana dalı alma merge commit'i yukarıda ayrıca belirtilmiştir.

2026-11-20'de kullanıcı bu sohbetin kendi yanıtında «Dört CT dosyasını stage et ve yerel commit oluştur» seçeneğini onayladı. İzin yalnız bu devir raporu, EVIDENCE.json, Beste'nin doğrudan onay kaydı ve CT-QUEUE.tsv dosyasındaki DL-12 eklemesini kapsar. CT bu dört dosyayı içerik ve kapsam denetiminden sonra kendi dalında yerel commit olarak kaydeder; push, PR ve ürün kodu değişikliği bu iznin dışında kalır. Commit sonucu Git geçmişinden ve teslim notundan doğrulanır.
