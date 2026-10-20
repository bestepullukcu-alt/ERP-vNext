# CT-APR-02 — onaydan önce bağımsız inceleme ve dilimli uygulamanın başlangıç kaydı

Bu dosyanın main'e girdiği commit, kuralın başladığı yerdir (yöneticinin isteği ve sahip onayı, 2026-10-20). Tarih değil
commit sırası ölçülür. Tasarım ve Codex görüşleri: `docs/roadmap/plans/ct-apr-02-pack-review/DESIGN.md`.

- **Kapsam:** Module Pack'ler ve Delivery Capability Pack'ler (`execution/portfolio/delivery-capability-packs/`). DCP'ler
  sürüme bağlı onaya (CT-APR-01) bu commit'ten itibaren girer.
- **İnceleme:** bu commit'te ya da sonrasında eklenen bir onay satırı, paketin aynı sürümünün bağımsız incelemesini ister.
  İstisna: değişen her satır muafsa (tarih alanı; `owner` / `reviewer` alanının TBD'den doldurulması; yalnız boşluk) ve
  hiçbir risk HIGH değilse. Hiç onay almamış eski bir paket, kuralın başladığı andaki sürümüyle karşılaştırılır; eski
  paketlere geriye dönük inceleme yoktur.
- **Kayıt:** `execution/approvals/reviews.tsv` (yalnız ekleme). Satırı `scripts/pack_approval.py review-row` üretir.
  Bulgu dosyası `docs/records/reviews/<kimlik>/<dosya>.md`; inceleme satırından önce commit edilir.
- **Sıra:** bir onayı yalnız onay satırının kendi geçmişinde (aynı commit dahil) bulunan incelemeler ve o andaki bulgu
  dosyaları geçerli kılar; onaydan sonra gelen olumlu inceleme ya da düzeltilen bulgu dosyası yeni bir onay satırı ister.
  Aynı commit'te gelen inceleme ve onay "aynı sürümün birlikte kabulü"dür; gerçek zaman sırasının kanıtı değildir.
- **Geçerlilik:** o sürümün bütün inceleme satırları birlikte okunur. Herhangi birinde `UYGUN DEĞİL` ya da açık bir ENGEL
  varsa onay geçmez; sonradan eklenen bir `UYGUN` önceki sonucu ezmez. ENGEL'i yalnız onaylayan üstlenebilir; o zaman onay
  HIGH sayılır. İnceleme satırı yalnız main'e, satırı ekleyenden başka birinin merge ettiği bir merge commit'iyle gelince
  sayılır.
- **Bağımsızlık:** inceleyen araç, model ve oturum kimlikleri beyandır. Kanıt, kaydın CODEOWNERS incelemesiyle main'e
  girmesidir; bu, bir yapay zekâ incelemesinin gerçekten ayrı bir oturumda yapıldığını kanıtlamaz.
- **Dilim:** inceleme isteyen paket bir dilim tablosu taşır: Module Pack'te `## Teslim dilimleri` (`DL-n`), DCP'de
  `## 8.1 Delivery slices`. Tek dilimli paket ve bütün dilimleri tek WP'ye veren prompt, yalnız onay satırının `note`
  alanındaki `whole_pack=<WP>` ile geçer.
- **Bu dosya silinemez:** eklendikten sonra yokluğu kuralı kapatmaz; onay geçmez.

## Bulgu dosyası biçimi

```text
# <hedef kimliği> — bağımsız inceleme

hedef: <paket yolu> @ <paketin SHA-256'sı>

F-1 | ENGEL | <bulgu> | açık | - | -
F-2 | ORTA | <bulgu> | açık | <sahip> | <YYYY-AA-GG>
F-3 | DÜŞÜK | <bulgu> | kapandı | - | -
F-1@<önceki bulgu dosyasının özetinin ilk 8 hanesi> | ENGEL | <bulgu> | kabul — onaylayan | <onaylayan kişi anahtarı> | -
```

Seviyenin adı bulgu satırı dışında büyük harfle yazılmaz; araç bu tür satırları bozuk sayar. Açık ORTA ve DÜŞÜK koşulların
sahibi ve son tarihi yazılır.
