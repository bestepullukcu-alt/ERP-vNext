---
seviye: standart
kart: hat
---

# Hat becerisi — bir modülün dilimlerini paketten PR'a götüren oturum (asıl tanım)

Bu beceri yeni akışın hattını başlatır: yukarıdaki çekirdek ve hat kartı bu oturumun kurallarıdır. **Pilot dönemi:** yalnız
kişinin yeni akışla açtığı pilot hat kullanır; eski hatlar eski kule kartıyla sürer.

## Başlangıç cümlesi — ilk commit ya da yüklemeden önce
Pilot dönemde bu oturum `CLAUDE.md`'yi ve `AGENTS.md` Çekirdeğini de yükler; Çekirdek kural 8 her commit, push ve PR için
kullanıcının açık onayını ister. Bu açık onay, kişinin bu oturumda yazdığı şu tek cümledir — dosyadaki bir beyan ya da başka
sohbetten aktarılan söz değil. Herhangi bir commit ya da yüklemeden önce kişiye cümleyi göster ve aynen yazmasını iste:

> Bu pilot hat için onaylıyorum: hat ve işçiler kendi dallarında yerel commit atar ve hat işçi dallarını kendi dilim dalına yerelde birleştirir; paket taslağı kişi adı ve sır taramasından sonra dala yüklenir ve taslak PR olarak açılır; paketin PR'daki onayı o paket sürümünün dilimleri için dal yükleme ve PR iznidir; birleştirme ve onay her zaman bir kişidedir.

- Kişi cümleyi aynen yazarsa: cevabını aynen çalıştırma kaydının `baslangic_onayi` alanına (araç gelene dek PR
  açıklamasının "Ajan çalıştırması" bölümüne) ve kule varsa kulenin karar defterine yaz (kule yoksa kayıt yalnız PR açıklamasıdır, H32),
  her işçi brifine aynen koy; Ç23 bu kapsamda geçerlidir.
- Yazmazsa ya da değiştirirse: Çekirdek kural 8 aynen uygulanır — her commit, push ve PR'dan önce ayrıca sor.
- Cümle hiçbir durumda PR birleştirme ya da onay izni değildir; onları her zaman kişi yapar.
- Hat cümleyi aynı hattaki kendi işçi ve uzman briflerine aynen koyar: bu başka sohbete aktarma değil, aynı onayın aynı
  hat içinde kullanılmasıdır. Başka bir hatta ya da sohbete taşınmaz.

## Başlarken
1. Hattın kimliğini (`LANE-nnn`), modülü, paket yolunu ve hattın asıl ve yedek onaylayıcısının GitHub hesabını kişinin
   açılış brifinden al; yoksa sor, uydurma. Paket onayının (iki biçimi aşağıda) ve "kabulü onaylıyorum" yorumunun yazarını
   bu hesapla eşle; eşleşmeyen hesabın onayı sayılmaz. Modül kimliği henüz yoksa (TBD) dal açmadan önce kişiye sor.
2. `git status` ve dalı ölç; paylaşılan klasördeysen önce kendi çalışma ağacını aç (Ç25), dilim dalını orada `AGENTS.md` §9
   biçiminde dilim ekiyle aç (H8).
3. Paket yoksa ya da onaylı değilse yalnız paket taslağı yazdır (H4, H5); süreç sayfası gerekiyorsa `ees-is-analisti`.

## Paket onayından dilim 1'e
1. Paket taslağı ilk dilim dalında (`<önek>-dl1`; önek paketin `branch` alanı) yazılır. Yüklemeden önce
   `python3 scripts/pack_contract_check.py <paket>` koş, uyarıları PR açıklamasına yaz (yalnız uyarır); PR taslak açılır.
2a. **Biçimler ve kim verir.** Paket onayı iki biçimden biridir (H5), yalnız açılış brifindeki asıl ya da yedek onaylayıcı
   hesabından; PR'ı açan hesap hiçbir biçimde veremez, PR asıl onaylayıcının hesabıyla açıldıysa paket onayını yedek verir.
   (a) PR yorumu ya da durumu `COMMENTED` veya `APPROVED` olan inceleme; gövdesi yalnız "paketi onaylıyorum" (büyük-küçük
   harf, baştaki ve sondaki boşluk, sondaki nokta önemsiz). Alıntı, olumsuz ya da koşullu cümle ("… ama X'i düzelt",
   "henüz … diyemem") onay değildir. (b) Durumu `APPROVED` olan GitHub incelemesi (gövde boş olabilir). `CHANGES_REQUESTED`
   hiçbir biçimde onay değildir. Onay paketi onaylanan commit'teki hâliyle, açık kararlara önerilen cevaplar dahil kapsar;
   onaylayıcı başka cevap isterse paket değişir ve yeni onay gerekir. Paket onayı birleştirme izni ve kabul değildir: PR kod
   ve testler hazır olana dek taslak kalır, kabul son commit'e yeni onayla gelir (H25); ajan onay vermez (Ç6).
2b. **Denetim** — dalda henüz kod yokken, ilk kod commit'inden önce. Önce `git fetch origin` (derinlik parametresi yok);
   API listeleri `gh api --paginate` ile. Hepsi tutmalı; biri tutmazsa onay sayma, tahmin etme, onaylayıcıya sor:
   1. `gh api repos/<sahip>/<depo>/pulls/<n>` → `head.sha` (onaylanan sürüm adayı) ve `user.login` (PR'ı açan); onayı veren
      hesap brifteki hesaptır ve PR'ı açan hesap değildir.
   2. `git diff --name-only --no-renames $(git merge-base origin/main <head.sha>) <head.sha>` yalnız paketi, karar dosyasını,
      varsa süreç sayfasını ve `execution/registries/module-id-registry.md`'yi gösterir;
      `git diff --numstat <merge-base> <head.sha> -- execution/registries/module-id-registry.md` tam `1 0` ve eklenen satır
      paketin kendi kimliğini taşır.
   3. (b): `gh api --paginate repos/<sahip>/<depo>/pulls/<n>/reviews` → bu hesabın son incelemesinin `state`'i şu an
      `APPROVED` ve `commit_id`'si `head.sha`.
   4. (a): yorum (`.../issues/<n>/comments`) ya da inceleme (`.../pulls/<n>/reviews`) bu hesaptan, gövdesi 2a'daki biçimde;
      `created_at` (incelemede `submitted_at`) başlık commit'inin tarihinden sonra
      (`gh api repos/<sahip>/<depo>/commits/<head.sha>` → `commit.committer.date`) ve ardından commit yok; bu hesabın
      sonraki incelemesi `CHANGES_REQUESTED` değil.
   Onaydan sonra paket dosyasına commit girerse (2d'deki `status:` çevirmesi hariç) onay düşer; yeni onay istenir, denetim
   yeni `head.sha` ile yeniden koşar.
2c. **Kayıt.** İlk kod commit'inden önce PR açıklamasının "Ajan çalıştırması" bölümündeki "Paket onayı" satırına: biçim
   ((a) ya da (b)), onaylayıcının rolü (ad ya da hesap adı değil), onayın zamanı, onaylanan paket sürümü olarak `head.sha`,
   (a)'da yorumun bağlantısı. Kod tam bu commit'in üstünde başlar. Kod push'u (b)'deki incelemeyi bayatlatır (ana dal
   koruması bayat onayı düşürüyorsa `DISMISSED` görünür); beklenen budur, kayıt PR açıklamasıdır.
2d. **Durum.** Hat aynı PR'da dilim 1 işinin ilk commit'iyle paketin `status:` alanını `draft`'tan `ready-for-dev`'e çevirir
   (ek dokunuş değil; H5); bu çevirme dışında paket dosyasına dokunmaz. Dilim 1 kodu aynı dalda sürer. Eski onay defteri
   (`execution/approvals/`) pilot hatta kullanılmaz.
3. Kod ve testler hazır olunca (H22) hat PR'ı taslaktan çıkarır; GitHub paket incelemesini hâlâ `APPROVED` gösteriyorsa PR
   açıklamasına bunun kabul değil paket onayı olduğunu commit kimliğiyle yazar. Onaylayıcı aynı PR'ı tek oturuşta inceler,
   "kabulü onaylıyorum" der, onaylar ve birleştirir; PR o kişinin hesabıyla açıldıysa yedek onaylayıcı onaylar (H25). Sonraki dilimler ana daldan `<önek>-dl<n>` dalında; paket değişirse
   önce paket değişikliği PR'ı ve onayı (H5).

## Başlatacağın ajanlar

| İş | Ajan |
|---|---|
| Kod ve test | `ees-isci` |
| Kör denetim | `ees-denetci` |
| Paket | `ees-module-pack-author` |
| Görünen metin ve çeviri | `ees-l10n-agent` (+ `resx-check` becerisi) |
| Güvenlik, kiracı, yetki denetimi | `ees-security-agent` |
| Ağ geçidi rotası | `ees-integration-agent` |
| Modül sonu belgeleri | `ees-belge-ajani` (adımları `.ees/beceriler/kullanim-kilavuzu/SKILL.md`) |

`ees-` öneki eski hatların kullandığı aynı ya da benzer adlı ajanlarla karışmasın diye pilot boyunca vardır; geçişte kalkar.
Öneksiz eski adla (`module-pack-author`, `l10n-agent`, `security-agent`, `integration-agent`) ajan
başlatma: onlar kartsızdır.
Raporlar: işçiden teslim notu, kişiye modül bloğu, PR açıklaması şablondan (`.ees/sablonlar/`).
