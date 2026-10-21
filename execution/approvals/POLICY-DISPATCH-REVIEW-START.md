# S-5 — CT-APR-02 dağıtım kapısının başlangıç kaydı

Bu dosyanın main'e girdiği commit, kuralın başladığı yerdir (sahip kararı S-5; model Codex tarafından incelendi; koruma
ölçütü sahip kararı 2026-10-21). Başlangıç, main'in ilk-üst (first-parent) çizgisinde dosyayı ekleyen en eski commit'tir
— PR ile geldiyse merge commit'i. Tarih değil commit sırası ölçülür. Main'de henüz yoksa yalnız çalışma dalının kendi
ekleme commit'i önizleme olarak sayılır. Üst kural: `execution/approvals/POLICY-REVIEW-START.md` (CT-APR-02). Kılavuz:
SOP §20.4 madde 8. Araçlar: `scripts/prompt_gate.py`, `scripts/acceptance_record.py`.

- **Kural:** ürün sınıfındaki bir WP'nin her yeni dağıtımı, Expected HEAD'deki paket sürümünün geçerli bağımsız
  incelemesini ister — katı ya da eski (strict olmayan) her kuyrukta. `prompt_gate.py check` durdurur; `--prepare` yalnız
  raporlar.
- **Olumsuzlar (geniş küme):** önce main'deki `execution/approvals/reviews.tsv`'nin yalnız-ekleme geçmişi denetlenir;
  silinmiş ya da değiştirilmiş bir satır durdurur. Sonra şu sürümlerin bütün inceleme satırları okunur: bu sürüm, gövdesi
  (künye dışı) bu sürümle aynı olan her incelenmiş sürüm ve bu sürümün muaf uzaklıkta olduğu her incelenmiş sürüm. Herhangi
  birinde `UYGUN DEĞİL`, açık bir ENGEL, bozuk bir kayıt ya da main'e başka birinin merge'iyle gelmemiş bir satır
  dağıtımı durdurur. Geçmişte bulunamayan bir incelenmiş sürümün olumsuz görünen satırı da durdurur.
- **Yeniden inceleme çıkışı (koşullu; sahip onayı 2026-10-21):** başka bir sürümden gelen olumsuzlar yalnız şu durumda
  BİLGİ olur: bu sürümün main'de geçen bir kendi inceleme satırı vardır, bu satırın commit'i bütün o olumsuz inceleme
  satırlarının commit'lerinden sonra gelir (onların torunudur) ve bulgu dosyası her olumsuz incelemeyi bulgu özetinin
  (`findings_sha256`) ilk 8 hanesiyle — özeti 64 hane olmayan satırı `reviews.tsv satır N` ile — adıyla anar. Ad
  yalnız `değerlendirilen olumsuz incelemeler: a, b` satırından ve tam olarak okunur (`satır 20`, `satır 2`'yi anmaz). Olumsuz satır, açık olumsuzlar (`UYGUN DEĞİL`, açık ENGEL) ile
  bozuk ya da sayılmayan her satırdır; bozuk bir satır açık olumsuzun arkasına saklanmaz, sonradan eklenirse önceki
  değerlendirme onu kapatmaz. Olumsuzdan önce yapılmış bir kendi `UYGUN` hiçbir şeyi kapatmaz. Aksi
  halde hepsi durdurur; sonraki adım "olumsuzdan sonra yapılan ve <sha8> incelemesini adıyla değerlendiren yeni bir
  inceleme gerekir"dir.
- **Bozuk inceleme dosyası:** `reviews.tsv`'nin main geçmişi bozuksa (satır silindi ya da yeri değişti) her şey durur.
  Satırı silen commit geçmişte kalır; satırları sonradan geri eklemek bunu temizlemez — çözüm sahip kararı ister (ör.
  yeni bir kayıtlı başlangıç). Bir satırın biçimi bozuksa da her şey durur; çıkış, o satırı düzelten CODEOWNERS onaylı
  bir PR'dır.
- **Kabul edilmiş ENGEL:** bu sürüm için okunan bir incelemede (kendi ya da kapatılmış başka sürüm) `kabul — onaylayan`
  ile kabul edilmiş bir ENGEL varsa, dağıtım ve kabul main'de tam bu SHA için son satır olan, geçerli, HIGH sayılan ve
  onaylayanının kişi anahtarı (merge'den önceki `approvers.tsv`) ENGEL'i kabul edenle aynı olan bir CT-APR-01 onay satırı
  ister. Onay satırı olmayan eski bir paketin "değişmedi" onayı yetmez.
- **Olumlu kapsama (dar küme):** yalnız bu sürümün ya da bu sürümün muaf uzaklıkta olduğu bir sürümün (tarih alanı;
  `owner` / `reviewer` alanının TBD'den doldurulması; yalnız satır sonu boşluğu) geçerli incelemesi dağıtımı açar. Önceki
  olumlu bir inceleme hiçbir olumsuzu gizlemez.
- **Yürüyen iş:** koruma yalnız "inceleme yok" şartını kaldırır; olumsuz bir inceleme korunan işi de durdurur. Korunmak
  için bu commit'te okunan kuyruk kaydı ve durum sözlüğüne (`execution/delivery/ct-queue-registry.tsv`,
  `ct-queue-states.tsv`) göre WP'nin kuyruktaki tek satırının durum sınıfı `active` olmalı (sop7: `DISPATCHED` …
  `INTEGRATED`; sce-mvp6: `IN-PROGRESS`) ve satırın `module` alanı bu paketin modül kimliğini göstermelidir. Ayrıca kuyruk
  dosyasının yolu, satırın kapsamı (`item` ve `module`; `owner`, `since`, `record` gibi kayıt sütunları olağan işte
  değişir, karşılaştırılmaz) ve paket dosyası o andan beri aynı kalmalı; satır bugün yeniden açılmış (`READY`,
  `REWORK`) olmamalı ve başlangıçtan bu yana kuyruk dosyasının hiçbir sürümünde (birleşen dallar dahil) `active` dışı bir
  sınıfa düşmemiş olmalıdır: bir kez `REWORK` (ya da `BLOCKED` gibi `active` dışı bir durum) olan iş korumayı geri
  kazanmaz. Geçmiş, dağıtılan commit'ten ve main'den birlikte, yan dallar dahil (`--full-history`) okunur. Kabulde
  olağan kapanış (`ACCEPTED`, `DONE` — `closed` sınıfı) ayrılmak sayılmaz; kapandıktan sonra yeniden çalışılan duruma
  dönmek yeniden açılmadır. Yeniden açılma sırayla değil ebeveyn kenarıyla ölçülür: satırın kapanmış olmadığı ve
  ebeveynlerinden birinde kapanmış olduğu her commit (paralel dal ya da commit tarihi sonucu değiştirmez). Kabulde
  geçmiş main'den, kabul kaydının commit'inden ve doğrulanan kod commit'inden okunur. Ölçü git'e kaydedilmiş geçmiştir: aynı commit içindeki gidiş-dönüş ya da squash ile silinen
  ara durum görünmez; bu yüzden kuyruk değişiklikleri squash edilmez (depo yalnız merge commit kabul eder). Kapsamın kaynağı kuyruk satırıdır; kapsamı
  ayrıca bir WP belgesinde yazılı iş için bu koruma o belgenin sürümünü ölçmez. Bunlardan biri tutmazsa iş kapıdan geçer ve ENGEL nedenini söyler. Eski kuyruk istisna değildir.
  `module` sütunu olmayan bir kuyruğun (bugün sce-mvp6) satırları hiç korunmaz; bu bilinen bir sonuçtur ve gerçek bir
  dağıtımı etkilemez, çünkü dağıtım yalnız `READY` ya da `REWORK` satırından başlar ve yeniden açılmış satır korunmaz.
- **Kabul:** `acceptance_record.py check`, bu dosya main'de ya da kaydın commit'inde varsa, kaydın bağladığı paket
  sürümüne (`module_pack`) dağıtım kapısının aynı olumsuz kümesini uygular; olumsuz varsa kabulü reddeder (çıkış 1),
  korunan yürüyen iş dahil. Kaydın commit'i başlangıçtan kesin olarak önce değilse (başlangıçtan önce ayrılmış bir dalda
  sonradan yazılan kayıt da kural altındadır) ürün işi kaydı (`module` alanında MOD kimliği) `module_pack` bağlamalıdır;
  bağlanan commit doğrulanan kod commit'inin atası olmalı ve paketin kod commit'indeki baytları bağlanan sürümle aynı
  olmalıdır. Bu sürüm İKİ şartı birlikte sağlar: main'deki sürümdür ve `pack_approval.py check` onu onaylı sayar
  (CT-APR-01; "değişmedi" eski onayı da sayılır) VE ya main'de geçerli bir incelemeyle kapsanır ya da bu WP'nin kural
  başlangıcında korunan başlangıç sürümüdür. Eski onay inceleme şartının, koruma da onayın yerine geçmez ("paket iş
  dalında değiştiyse yeni sürümü inceletin"). Daha eski kayıtlar değişmez.
- **CT-APR-01 onayı değişmez:** bu kural onayın yerine geçmez; inceleme var diye yeni bir ilk HIGH onay istenmez.
- **Kendini açıklayan ENGEL:** her ENGEL satırı kuralı ve başlangıç commit'inin kısa kimliğini, durma nedenini (koruma
  yoksa nedeni dahil) ve sonraki adımı (`python3 scripts/pack_approval.py review-row …`, SOP §20.4; silinmiş başlangıç
  kaydında "geri getir") söyler ve şunu ekler: "Bu kuralı tanımıyorsan Control Tower oturumun kuraldan önce açılmıştır:
  main'i al ve yeni oturum aç."
- **Sığ depo reddedilir:** sığ (shallow) bir klonda başlangıç, inceleme geçmişi ve kuyruk anı ölçülemez.
- **Bu dosya silinemez:** eklendikten sonra yokluğu kuralı kapatmaz; dağıtım ve kabul geçmez.
