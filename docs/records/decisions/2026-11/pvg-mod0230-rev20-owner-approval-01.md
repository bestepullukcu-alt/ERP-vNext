---
module: MOD-0230
work_package: WP-0230-DL12
status: decided
decision_date: 2026-11-20
authorization_scope: revision20-scope-policy-shared-paths-build-test-and-delegated-development
operational_runtime_authorized: false
publication_authorized: false
---

# MOD-0230 revizyon 20 — doğrudan sahip onayı

Kullanıcı CT sohbetinde (`01a1f7d8-bf54-7703-ae91-a8ba29312b0d`) aşağıdaki somut kapsam/onay sorusuna kendi turunda **“onaylıyorum”** yanıtını verdi:

> Rev20’yi bu kapsamla onaylıyor musun?

Sorunun kapsamı `docs/roadmap/plans/pvg-mod0230-rev20-approval-request-2026-11-20.md` dosyası ve önceki CT yanıtındaki ortak test/CI ekleri, kayıt varlığını da gizleyen alan politikası ve kapılar sonrasında ayrı işçilerle geliştirme/testtir. Bu, başka bir belge/ajan üzerinden gelen talimat değildir.

## Bağlanan sürüm

- Onaya sunulan kaynak commit: `486521d11c5ddaec2294b94e9114b4c3875a4fd6`.
- Pack SHA-256: `f5a028c54ced21480a94d0db45b99a81ab9a982a56f3934818955aa2f44d35b1`.
- Domain SHA-256: `b201a180309d5a0e8e04de8b845cc16bd55912085fcc345b4a51f19999c27099`.
- Revizyon: 20, DL-12; başka modül veya yeni ürün kapsamı açılmaz.

## Benimsenen kararlar

Alan politikası geçerli bağlam/izin/istek doğrulamasından sonra ve depo okumasından önce değerlendirilir. Ret halinde mevcut/yok/başka kiracı kimlikleri aynı güvenli ret, sıfır okuma/yazma ile karşılanır. Politika izin verirse mevcut kiracı sınırları ve not-found sözleşmesi korunur.

Pack'in kesin dar controller/DTO/JS/test/resx/Windows-helper yollarına ek olarak ortak Web test projesinde tek aliased PVG referansı, mevcut Phase1 workflow'unda yalnız pinned Node22/npm-ci hazırlığı, Edit formunda iki optional alan düzeltmesi ve mevcut RegPvBase testinin yalnız Update beklenti hizalaması onaylandı. Bu dosyalarda tek yazıcı uygulanır; tüm ilgili yollar kullanıcıya sunulan kapsam dosyasında bellidir.

Onay, aynı kapsamın hazır statüsüne geçirilmesini ve bununla uyumlu güncel yetki/durum metinlerinin paket yazarı tarafından düzeltilmesini kapsar. Bu idari geçiş yeni iş kuralı veya dosya yolu açamaz. Yazar son baytları dondurur; resmî bağımsız inceleme ve onay/dağıtım araçları bu SON hash'i kullanır. Eski sürüm incelemesi yeni hash için UYGUN diye kullanılamaz.

Önce sınırlı build/test uygulanabilirliği ve Windows hazırlığı ölçülür; geçerli kesin-sürüm inceleme/HIGH onay ve dağıtım kapısı olmadan DL-12 ürün işçisi başlatılmaz. CT ürün kodu yazmaz. Onay bu kapıların yerine geçmez veya başarı varsaymaz.

## Kimlik ve kapanış sınırları

Kullanıcı daha önce bu aynı sohbette kendisini Beste olarak tanımladı; bu doğrudan beyan ilgili önceki kayıtta korunur. Bu yeni onay da doğrudan kullanıcı yanıtıdır; doğrulanmış GitHub hesap kimliği iddiası değildir. Kayıt/PR yazarı gerçek CLI hesabı ElikPenco olarak ölçülür; HIGH onaylayan ile yazar farklı kişiler olmalıdır. Deferred kayıt ve modül PR'ındaki bağımsız insan merge kapısı korunur.

Tek sonuç PR'ı hedeflenir. Bu onay push, PR açma, main'e merge, operasyonel kullanım veya bitmemiş worktree silme izni değildir; bunların mevcut kapanış kapıları korunur. Biten worktree'ler yalnız güvenli entegrasyon ve gerekli main erişilebilirlik kontrollerinden sonra temizlenir.
