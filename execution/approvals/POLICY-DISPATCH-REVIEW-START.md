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
- **Yeniden inceleme çıkışı:** bu sürümün main'de kendi inceleme satırları varsa ve geçiyorsa, yalnız gövdesi aynı (künye
  değişikliği muaf olmayan) sürümlerden ya da geçmişte bulunamayan sürümlerden gelen olumsuzlar BİLGİ olur, durdurmaz.
  Muaf uzaklıktaki bir sürümün olumsuzu her zaman durdurur: tarih ya da TBD doldurma bir bulguyu düzeltemez; paket esaslı
  değişmelidir. Önce `reviews.tsv`'nin main geçmişi bozuksa (satır silindi ya da yeri değişti) her şey durur; çıkış,
  silinen satırları aynı sırayla geri ekleyen, CODEOWNERS onaylı bir PR'dır.
- **Olumlu kapsama (dar küme):** yalnız bu sürümün ya da bu sürümün muaf uzaklıkta olduğu bir sürümün (tarih alanı;
  `owner` / `reviewer` alanının TBD'den doldurulması; yalnız satır sonu boşluğu) geçerli incelemesi dağıtımı açar. Önceki
  olumlu bir inceleme hiçbir olumsuzu gizlemez.
- **Yürüyen iş:** koruma yalnız "inceleme yok" şartını kaldırır; olumsuz bir inceleme korunan işi de durdurur. Korunmak
  için bu commit'te okunan kuyruk kaydı ve durum sözlüğüne (`execution/delivery/ct-queue-registry.tsv`,
  `ct-queue-states.tsv`) göre WP'nin kuyruktaki tek satırının durum sınıfı `active` olmalı (sop7: `DISPATCHED` …
  `INTEGRATED`; sce-mvp6: `IN-PROGRESS`) ve satırın `module` alanı bu paketin modül kimliğini göstermelidir. Ayrıca kuyruk
  dosyasının yolu, satırın modülü ve paket dosyası o andan beri aynı kalmalı; satır bugün yeniden açılmış (`READY`,
  `REWORK`) olmamalıdır. Bunlardan biri tutmazsa iş kapıdan geçer ve ENGEL nedenini söyler. Eski kuyruk istisna değildir.
  `module` sütunu olmayan bir kuyruğun (bugün sce-mvp6) satırları hiç korunmaz; bu bilinen bir sonuçtur ve gerçek bir
  dağıtımı etkilemez, çünkü dağıtım yalnız `READY` ya da `REWORK` satırından başlar ve yeniden açılmış satır korunmaz.
- **Kabul:** `acceptance_record.py check`, bu dosya main'de ya da kaydın commit'inde varsa, kaydın bağladığı paket
  sürümüne (`module_pack`) dağıtım kapısının aynı olumsuz kümesini uygular; olumsuz varsa kabulü reddeder (çıkış 1),
  korunan yürüyen iş dahil. Kaydın commit'i başlangıçtan kesin olarak önce değilse (başlangıçtan önce ayrılmış bir dalda
  sonradan yazılan kayıt da kural altındadır) ürün işi kaydı (`module` alanında MOD kimliği) `module_pack` bağlamalıdır;
  bağlanan commit doğrulanan kod commit'inin atası olmalı ve paketin kod commit'indeki baytları bağlanan sürümle aynı
  olmalıdır. Daha eski kayıtlar değişmez.
- **CT-APR-01 onayı değişmez:** bu kural onayın yerine geçmez; inceleme var diye yeni bir ilk HIGH onay istenmez.
- **Kendini açıklayan ENGEL:** her ENGEL satırı kuralı ve başlangıç commit'inin kısa kimliğini, durma nedenini (koruma
  yoksa nedeni dahil) ve sonraki adımı (`python3 scripts/pack_approval.py review-row …`, SOP §20.4; silinmiş başlangıç
  kaydında "geri getir") söyler ve şunu ekler: "Bu kuralı tanımıyorsan Control Tower oturumun kuraldan önce açılmıştır:
  main'i al ve yeni oturum aç."
- **Sığ depo reddedilir:** sığ (shallow) bir klonda başlangıç, inceleme geçmişi ve kuyruk anı ölçülemez.
- **Bu dosya silinemez:** eklendikten sonra yokluğu kuralı kapatmaz; dağıtım ve kabul geçmez.
