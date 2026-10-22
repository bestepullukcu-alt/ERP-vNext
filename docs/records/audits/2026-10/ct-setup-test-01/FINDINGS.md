# CT setup testi — bulgu defteri (CT-SETUP-TEST-01)

Amaç: yeni Control Tower sistemini gerçek bir modülde (Pharmacovigilance, 2026-10-20 →) çalıştırırken çıkan **setup**
sorunlarını tek yerde toplamak. Düzeltmeler `chore/agents/ct-start-05` dalında birikir ve testin sonunda tek PR olur;
sonraki yeni modülde aynı liste yeniden ölçülür.

Alt ajanlar: metin düzeltmeleri (S-1, S-2, S-6) ve kod düzeltmesi (S-3) Sonnet ile; effort araç kuralı gereği varsayılan.

Kurallar (sahip, 2026-10-21):
- Denetlenen CT oturumuna müdahale edilmez; yalnız okunur. Bulgular sahibe raporlanır.
- Her bulgu: neden (setup / model / ürün), seviye (🔴 yön ya da güvenlik · 🟡 öncelik ya da eksik karar · 🟢 kozmetik),
  sahibi, durum.
- **Ürün** bulguları (modülün kendi kodu) burada yalnız not edilir; düzeltmesi o modülün CT kuyruğundadır.
- Setup düzeltmesini tek bir alt ajan yazar; düzeltme denetlenir ve bozma kanıtı ister. Politika, kapı ve CODEOWNERS
  dosyalarında Codex görüşü alınır.

Ölçülen kaynak: PVG CT oturumu ("CT - PVG", 2026-10-20/21), worktree `.claude/worktrees/pvg-ct-01`; denetim notları sahibe
iletildi.

## Geçen maddeler (canlıda ölçüldü)

| Kural | Kaynak | Sonuç |
|---|---|---|
| CT-START-03 — önce yeri ölç; ana klasör ya da main gerisi → dur, yazma | skill §1 | ✅ iki oturumda da durdu, dosya yazmadı |
| CT-START-04 #1 — sahip kararını ara, kaynağıyla "hâlâ geçerli mi?" diye sor | skill §2 | ✅ d0460541c'yi satırıyla buldu |
| CT-START-04 #2 — soru "evet"in neye izin verdiğini söyler | skill §6 | ✅ her soruda |
| CT-START-04 #4 — alt işe model ve effort yazar | SOP §17.1 r5 | ✅ bağımsız inceleme düzeninde |
| CT-APR-02 — paket incelemesini CT yapmaz, düzenler; incelemenin bayatlamasını önceden görür | SOP §20.4 | ✅ |
| Kişisel veri — yeni metinde ad ve e-posta yok | public depo | ✅ karar kaydında ve notta 0 |

## Setup bulguları

| # | Bulgu | Neden | Seviye | Sahibi | Durum |
|---|---|---|---|---|---|
| S-1 | İşletim kartında "hiç onayı olmayan pakette ilk onay her zaman HIGH; var olan sahibi değiştirmek bağımsız inceleme ister" yok; CT LOW sandı | Setup | 🟡 | alt ajan (metin) | yapıldı — kart notu; card testleri yeşil |
| S-2 | Skill, mevcut bir paketi okurken inceleme kaydına bakmayı ve kayıt yoksa — eski ya da yeni — işe başlamadan önce bağımsız inceleme önermeyi istemiyor | Setup | 🟡 | alt ajan (metin) | yapıldı — skill §3 maddesi + çapa; bozma: çapa silinince --check FAIL |
| S-3 | İnceleme metni kuralı ile araç uyuşmuyor: politika "seviye adını bulgu satırı dışında büyük harfle yazma" diyor; `parse_findings` "engel" kelimesini küçük harfle de bozuk sayıyor, düz Türkçe cümle inceleme dosyasını reddettirebilir | Setup | 🟡 | alt ajan (kod) + Codex | yapıldı — seviye kelimeleri yalnız BÜYÜK harfle bozuk sayılır, `|` kuralı korundu; self-test 101/101; iki bozma kırmızı |
| S-4 | Port ataması eksik ve tutarsız: gateway 5004 (Kurumsal Strateji, mikroservis bandı dışında) ve 5011 (PV) portlarına yönlendiriyor ama ikisi ne `AGENTS.md` §3 tablosunda ne de altındaki "kullanılan ama tabloda olmayan" notunda var; 5062 (PPM + DataKnowledge çift talep), 5063, 5064 notta "EA kararı bekliyor" diye bekliyor. Tabloya satır eklemek bir port tahsisi kararıdır (EA), setup düzeltmesi değil. Ayrıca `AGENTS.md` 21.993 bayt, 22.000 sınırında | Setup (yönetişim) | 🟡 | sahip (seçenek 2) + EA | belgeleme yapıldı — 5004 ve 5011 `AGENTS.md` §3 notunun "kullanılan ama tabloda olmayan (EA kararı bekliyor)" listesine eklendi (21.991 bayt, bütçe testi yeşil); tahsis kararı (özellikle 5062 çift talep, 5004 bant dışı) EA'da açık |
| S-5 | İnceleme kaydı olmayan pakete iş dağıtılabiliyor (prompt kapısı durdurmuyor) | Setup (politika) | 🟡 | sahip + Codex | dal `chore/agents/ct-apr-03` — Codex 2. tur: Q1′/Q3′/Q4′/Q5′ UYGUN, Q6 KOŞULLU, Q2′ ve Q7 ENGEL → düzeltildi; bağımsız alt ajan F-1…F-5 → düzeltildi; Codex 3. tur (d3098a3a): Q2″/Q7′/Q8 UYGUN DEĞİL, Q7-sınır ve Q9 KOŞULLU — F-6 (satır 2 ↔ satır 20 alt metin eşleşmesi), F-7 (normal ACCEPTED kapanışı korumalı kabulü bozuyor), F-8 (iki commit satırlı künye temiz sayılıyor) → düzeltildi; bağımsız alt ajan F-9…F-13 (kapanış sırayla okunuyordu → ebeveyn kenarı; kabulde kayıt commit'i; ad satırında backtick; künye belirsizliği daraltıldı) → düzeltildi; Codex 4. tur (01b82ab1): R1/R2/R5 UYGUN, R3 UYGUN DEĞİL (F-14: işçi dalındaki REWORK, kabul main'de yazılınca görünmüyor), R4 KOŞULLU (F-15: bozuk commit değeri 'ölçülmedi') → düzeltildi (kabulde kod commit'i de okunur; bozuk değer BELİRSİZ); bağımsız alt ajan UYGUN + F-16 (mesaj) / F-17 (açılamayan künye) DÜŞÜK → düzeltildi; Codex 5. tur (45b74f43): KOŞULLU — PR'a gidebilir, ENGEL yok; F-18 DÜŞÜK (açılamayan künyede girintili/tırnaklı commit anahtarı) → düzeltildi; eski kuyrukta otomatik yeniden denetim yok (politikada yazılı) |
| S-6 | SOP §20.4 inceleyenin depoya erişimini söylemiyor; CT'nin prompt'u Codex'in yerel çalışma ağacını görebildiğini varsaydı (Codex yerel depoyu görmüyor: ZIP ya da push edilmiş dal) | Model + setup | 🟢 | alt ajan (metin) | yapıldı — SOP §20.4/2: inceleyicinin erişimi (itilmiş dal ya da ZIP) |
| S-7 | Ana klasör main gerisindeyken açılan oturum skill'in eski kopyasını yükler; yeni kural ancak yeni oturumda görünür | Setup (işletim) | 🟢 | — | kabul: CT-START-03 bunu durdurur; testten önce ana klasör güncellenir |
| S-8 | Kuraldan önce açılmış CT oturumları yeni kuralı bilmez; main'i alsalar da oturum yenilenmeden skill değişmez; kapı durdurunca nedenini anlamayabilirler | Setup (işletim) | 🟡 | S-5 ile | açık — S-5'in her ENGEL mesajı kuralı, nedeni, sonraki adımı ve "main'i al, yeni oturum aç" ipucunu kendisi söyler |
| S-9 | CT'ye bağımsız inceleme sonucu getirildiğinde körü körüne kabul etmemesini söyleyen kural yok | Setup | 🟡 | alt ajan (metin) | yapıldı — skill §5 + SOP §20.4/4; çapa silinince --check FAIL |
| S-10 | İş merge edildikten sonra worktree'yi kaldırma kuralı SOP / kart / skill'de yok; 17 worktree birikmiş (7'si merge edilmiş ve temiz; 2'sinde push edilmemiş 10 commit) | Setup | 🟡 | alt ajan (metin) + sahip (temizlik) | yapıldı — SOP §16.5 + kart §6; temizlik sahibin onayında |
| S-11 | Kural paketi `module-pack-author` ajanına hazırlatır ama ajan Claude Code / Codex'e aktarılmamıştı; CT paketi kendisi düzeltti. CT'nin incelemeye itirazı inceleyene geri dönmüyordu | Setup | 🟡 | alt ajan (yapılandırma + metin) | yapıldı — ajan `.claude/agents/` ve `.codex/agents/`'a aktarıldı (model: inherit, CT model/effort yazar); skill §5 + SOP §20.4/2; skill bütçesi 7500 → 8000; çapalar silinince --check FAIL |
| S-12 | Kabul edilmiş bir iş (ABB listesi, BL-553: kabul `9020e844f`) main'e hiç girmemiş, yalnız yerel bir dalda duruyordu; kabul kaydı işin main'de olduğunu doğrulamıyor | Setup (işletim) | 🔴 | sahip (K-2: uyarı, 2026-10-21) + Codex | dal `chore/agents/ct-apr-03` — `ct_queue.py summary`, kapanmış satırın kabul kaydı ya da kabul edilen kod commit'i main'de değilse UYARI yazar (kapı değil); kabul künyesi olmayan satır 'ölçülmedi' sayılır; dal yedek olarak push edildi (PR yok) |
| S-13 | CT'nin rolü (karar verir, ölçer, denetler; işi devreder; kod işi için bağlı alt oturum açar, prompt'u sahibe taşıtmaz; yalnız küçük kayıt işlerini kendisi yapar) skill'de açık değil | Setup | 🟡 | sahip onayı (2026-10-21) | yapıldı — skill §5 + çapa; cümle silinince --check FAIL |
| S-15 | PR #176 merge edildikten sonra aynı dala iki commit itildi (CT-TRK-01, K-7); main'e girmediler, #182 ile geldiler. İtişten önce PR'ın açık olduğu ölçülmedi | Model + setup | 🟡 | ben (metin) | dal `chore/agents/ct-apr-03` — SOP §20.4/7: itişten önce PR durumu, merge'den sonra dal başının main'de olduğu ölçülür |
| S-18 | Yeni PVG CT oturumu ana klasörde açıldı (geliştirici gizli `.claude/worktrees` klasörünü bilmez); CT durmadı ve bunu bildirmedi; "evet" sonrası commit main'e gidebilirdi | Setup (skill tasarımı) + işletim | 🔴 | ben (metin) + Codex tasarım görüşü | dal `chore/agents/ct-start-06` — kart §0 "Yer": geliştirici hep ana klasörde açar, CT modülün `chore/ct/<q>-ct-NN` worktree'sine kendisi geçer ya da açar (Codex KOŞULLU, 5 şart işlendi: kuyruk kimliği doğrulama, başkasının işini devralmama, yerel dal, geçemezse devam etmeme, ilk satır biçimi ve yetki sınırı); bağımsız alt ajan UYGUN DEĞİL → F-1 (modül → kuyruk adımı yoktu; kayıtta modül sütunu yok → `queue_id` / domain klasörü / MOD sırası) ENGEL ve F-2…F-11 düzeltildi (salt okunur istisnası, eski CT'de ikinci CT açmama, yeni CT, açık oturum sezgisi, origin gerisi, komutlar, ilk satır); skill §1 + §5 + §6; çapa silinince --check FAIL |
| S-19 | MOD-0230 bağımsız inceleme sonucu yalnız kapanan oturumun geçici klasöründe kaldı; bulgu dosyası onay beklenirken yazılmadı | Setup (işletim) | 🟡 | ben (metin) | dal `chore/agents/ct-start-07` — SOP §20.4/4 + skill §5: sonuç gelir gelmez ham metin worktree'ye commit'siz taslak; kayıt ve commit sahip onayıyla |
| S-20 | Kart §0 "Yer" 5. adımındaki `find -newermt '-60 minutes'` macOS'ta hata veriyor (yeni PVG CT yakaladı; ben komutu denemeden yazmıştım) | Setup (benim hatam) | 🟡 | ben | dal `chore/agents/ct-start-07` — `find <wt> -mmin -60 …` (macOS'ta denendi) |
| S-21 | Keşif raporu ~620 satır (587'si `ref:`), ekrana sığmıyor; kural "yalnız ekrandan oku" | Setup | 🟢 | ben | dal `chore/agents/ct-start-07` — kart §0.1: `grep -v '^ref:'` ile süz (32 satır), ref satırları sayılır; skill §2 |
| S-22 | CT soruyu yalnız butonlu soru kutusunda sordu, metinde "soru üstteki kutuda" yazdı; sahip soruyu bulamadı | Setup (skill §6) | 🟡 | ben (metin) | dal `chore/agents/ct-start-07` — kart "Soru biçimi" (sahip kararı: her CT aynı biçim — ❓ SORU, en çok 3 seçenek, ÖNERİM + tek cümle neden, her seçeneğin izni) + skill §6/5; kutu yalnız ek kolaylık; çapa silinince --check FAIL |
| S-23 | Yeni PVG CT okumak için hedef worktree'ye `cd` yaptı; uygulama oturumu oraya taşıdı, kart §0 "Yer" 5. adımının sorusu geçişten sonra soruldu (yazma yok, CT kendisi bildirdi) | Setup (araç davranışı) | 🟡 | ben (metin) | dal `chore/agents/ct-start-08` — kart §0 "Yer" 2: geçişten önce `cd` yok, `git -C <wt>` |
| S-24 | Soru bir olguyu soruyordu ("başka oturum açık mı?") ama "1" cevabı merge ve taslak yazma izni de veriyordu | Setup (soru biçimi) | 🟢 | ben (metin) | dal `chore/agents/ct-start-08` — kart "Soru biçimi": soru izni sorar; olgu sorusu izin vermez |
| S-25 | PVG CT onay sorusunda ve raporda K3, GMG-ERP, `approvers.tsv:5` gibi kodları açıklamasız kullandı; sahip neyi onayladığını bilemedi | Setup (soru biçimi) | 🟡 | ben (metin) | dal `chore/agents/ct-start-09` — kart "Soru biçimi": her kod / anahtar / hesap yanında düz açıklama ve türetildiği yer |
| S-26 | Art arda iki soruda "2" farklı anlam taşıdı (önce "hepsine evet", sonra "önce düzelt"); sahibin yalnız "2" yazması commit yerine "neyi düzelteyim?" durumuna götürdü | Setup (soru biçimi) | 🟡 | ben (metin) | dal `chore/agents/ct-start-10` — kart "Soru biçimi": her seçeneğe soruya özgü cevap kelimesi; yalnız numara gelir ve önerilen değilse ya da yazma başlatıyorsa CT teyit ister. Ek: SOP §20.4/6a kural değişince oturum (kart/SOP → main'i al + yeniden oku; skill → yeni oturum), hesap adı değişirse approvers.tsv |
| S-27 | PVG CT dalında gün boyu 18+ commit yalnız bu bilgisayarda kaldı (sabah da aynı durum) | Setup (işletim) | 🟡 | sahip kararı ("orta yol") | dal `chore/agents/ct-start-10` — kart §7: CT gün sonu ya da büyük adım sonrası yedek push'u kendisi önerir, tek kelime onay (`yedek`), push öncesi tarama; GIT-002 §5 değişmedi (kendiliğinden push için korunan kural değişikliği ve Codex görüşü gerekir) |
| S-33 | module-pack-author alt ajanı istenen high yerine düşük effort (beyan 15) ile çalıştı; ajan tanımında `effort` alanı yoktu (yalnız `model: inherit`). PVG CT kendisi yakaladı ve inceleme satırlarına "istenen high, beyan 15" yazdı | Setup (ajan tanımı) | 🟡 | ben (yapılandırma) | dal `chore/agents/ct-start-13` — `adapters.json` module-pack-author (claude-code) frontmatter'ına `effort: high`; `.claude/agents/module-pack-author.md` yeniden üretildi. Canlı ölçüm: merge sonrası ilk ajan çağrısında beyan edilen effort |
| S-34 | PVG CT, eski bir sürüme verilmiş (yerel) onay satırını `git revert` ile geri aldı; bu `approvals.tsv`'den satır sildi. `pack_approval.py check` "yalnız ekleme değil — onay dosyası güvenilmez" diyerek yakaladı; main'e girseydi SCE'nin geçerli onayları da tanınmaz olurdu. Kural POLICY-START'ta yazılıydı ("iptal `revoke` satırıyla") ama kartta yoktu; `pack_approval.py row` revoke satırı üretemiyor | Setup (kart + araç eksiği) + model | 🔴 | ben (kart) + sahip (yerel geçmişin yeniden kurulması) | dal `chore/agents/ct-start-13` — kart §7: geri alma yalnız `revoke` satırıyla; araçta revoke satırı üretme seçeneği yok (ayrı iş, CODEOWNERS) |
| S-28 | PVG CT, Codex'e gidecek inceleme 02 prompt'unu sohbette vermedi ("dosyadan iletin"); dosyada da kod bloğu dışında, başlık ve `\|` satırları markdown olarak bozuluyor | Setup (yanıt biçimi) | 🟡 | ben (metin) | dal `chore/agents/ct-start-11` — kart "Soru biçimi": başkasına gidecek her prompt sohbette tek kopyalanabilir kod bloğunda, dosyada da kod bloğunda; ZIP yolu + SHA-256 altında |
| S-29 | Codex ZIP'i yine oturumun geçici klasöründe (S-19 ailesi) | Setup (işletim) | 🟢 | ben (metin) | dal `chore/agents/ct-start-11` — kart: teslim dosyaları worktree'de git'in izlemediği `.ct-teslim/<iş>/` |
| S-30 | CRM'in PV isteklerinde CT sınıflandırıp itiraz etti; önerilen cevaplar yalnız dosyada kaldı, CRM'e gidecek mesaj hazırlanmadı | Setup (işletim) | 🟡 | ben (metin) | dal `chore/agents/ct-start-12` — kart: dış istek → sınıflandır, cevabı sohbette göster, isteyene mesajı kod bloğunda hazırla, göndermez |
| S-31 | CT-TRK-01 iş takip sayfası PVG'de hiç açılmadı; CT her seferinde "sırada" dedi. Neden: uyarlamada kaynak önerinin "ne zaman açılır" maddesi atlanmıştı (benim hatam); skill yalnız "varsa güncelle" diyordu | Setup (uyarlama hatası) | 🟡 | ben (metin) | dal `chore/agents/ct-start-12` — SOP §22.2/2a: pilot block'ta ilk durum değerlendirmesinden ya da ilk paket/karar commit'inden sonra CT önerir, `sayfa-ac` ile açar, ertelemez |
| CRM istek alımı canlı test | CRM'in PV istekleri çerçevesiz iletildi; CT iş başlatmadı, kuyruk satırı açmadı, 4 isteği modüllere ayırdı, gerekçeli itiraz etti, mevzuat ifadelerini QPPV doğrulamasına bağladı | — | 🟢 geçti | — | 2026-10-21 |
| S-25 canlı test | Kısa mesajla ("main'i al, son soruyu yeniden sor") CT kartı kendisi yeniden okudu ve kodları açıkladı | — | 🟢 geçti | — | 2026-10-21 |
| S-9 canlı test | PVG CT, Codex'in 7 bulgusunu paket ve kodla tek tek doğruladı; F-7 seviyesine kısmi itiraz, F-1 vurgusunu düzeltti; 5 kendi + 11 alt ajan maddesi (kodla yeniden ölçüldü); kendi notundaki 2 hatayı bildirdi; taslaklar commit'siz | — | 🟢 geçti | — | 2026-10-21 |
| S-18 + S-22 canlı test | Yeni PVG CT (#187 sonrası): Codex sonucunu dalda buldu, F-2 ve F-5'i kodla kendisi doğruladı, module-pack-author'u model + effort ile önerdi, soruyu "Soru biçimi"yle sordu, ana klasöre yazmadı | — | 🟢 geçti | — | 2026-10-21 |
| S-18 canlı test | Yeni PVG CT ana klasörde açıldı, `/control-tower Pharmacovigilance` → `pvg` buldu, "60 dakika" sorusunu sordu, "hayır" sonrası `pvg-ct-01`'e geçti, main'i merge commit ile aldı (d010c17d6), ana klasöre yazmadı | — | 🟢 geçti | — | 2026-10-21 |
| S-14 | Sahip için block başına tek bir iş takip görünümü yok (CRM ekibinin önerisi CT-TRK-01) | Setup (yeni özellik) | 🟡 | alt ajan (metin) | yapıldı — uyarlanmış hali SOP §22.2 + skill §6 (pilot PVG; depo kaynak, sayfa görünüm; sayfa özel); çapa silinince --check FAIL |

## Model bulguları (düzeltme gerekmez, sonraki modülde yeniden ölçülür)

| # | Bulgu | Seviye |
|---|---|---|
| M-1 | İlk yanıt hedeften uzun (~20 satır; hedef ~12) | 🟢 |

## Ürün bulguları (PVG CT kuyruğunda; burada yalnız not)

| # | Bulgu | Kaynak |
|---|---|---|
| P-1 | MOD-0230: üretim-benzeri + yetkili + adaptörsüz host açılıyor, iş ucu 500 boş gövde (paket "eksik sözleşmede açılış başarısız" ister) | `pvg-m1-02-inspection-01/INSPECTION-NOTE.md` B3 |
| P-2 | MOD-0230: Gateway PV route'larında kimlik doğrulaması yok; anonim istek API'ye ulaşıyor | aynı not, G1/G4 |
| P-3 | MOD-0230 paketinde ve DCP-004 pvg'de kişisel e-posta | PVG CT, 2026-10-21 |
| P-4 | MOD-0040: kimlik kaydı ile Blueprint 8.8 çatışıyor | PVG karar kaydı D2 |

## Sahip dönüşünde karar bekleyenler (2026-10-21)

| # | Konu | Öneri |
|---|---|---|
| K-1 | S-4 port tahsisi: 5004 (bant dışı), 5011, 5062 (çift talep), 5063, 5064 | EA kararı; sonra `AGENTS.md` §3 tablosu (yol sütunu kısaltılarak sığar) |
| K-2 | S-12 kabul edilmiş ama main'e girmemiş iş | **Karar (sahip, 2026-10-21): uyarı** — `ct_queue.py summary` listeler; kabulü merge'e bağlamaz |
| K-3 | `abb-accept` (ABB listesi, 8 commit) yedek olarak push edildi; main'e alınıp alınmayacağı | MDM işinin sahibi karar verir |
| K-4 | Kaydedilmemiş iş içeren worktree'ler: platform-owned-mongo (12 dosya, BL-595), mdm+product-takeover (4 dosya), live-main (yerel ayar) | Sahipleriyle bakılmalı; silinmedi |
| K-5 | S-5 (dağıtım kapısı) Codex kod denetimi | Hazır: dal `chore/agents/ct-apr-03` (yerel, push yok; 3 commit, 719788b7e), ZIP `s5-codex-01.zip` (sha256 b38ad7b1…) ve prompt; bağımsız alt ajan 3 turdan sonra KOŞULLU→düzeltildi. Codex'e sahip gönderir |
| K-7 | S-5 D2: önceki sürümün olumsuzu ne zaman bilgiye düşer | **Karar (sahip, 2026-10-21): seçenek 2, koşullu** — yalnız olumsuzdan sonra yapılmış ve o incelemeyi adıyla (bulgu dosyası özetinin ilk 8 hanesi) değerlendiren kendi geçerli incelemesi olan sürümde. Codex S-5 denetimi (Q2) bu biçimi önerdi |
| K-8 | S-5 yürüyen iş koruması pratikte hiçbir gerçek dağıtımda devreye girmez (dağıtım yalnız READY/REWORK'ten; yeniden açılan iş korunmaz); yürüyen iş zaten yeniden dağıtılmaz, kabulde yalnız olumsuzlar durdurur; legacy sce-mvp6 kuyruğunda module sütunu olmadığı için koruma yok | Bilgi; sahip kabul eder ya da değiştirir |
| K-9 | S-5 Q6 (Codex KOŞULLU): yürüyen iş korumasında kapsam = kuyruk satırının `item` + `module`'ü; kapsamı ayrıca bir WP belgesinde yazılı işte o belgenin sürümü ölçülmez | Bilgi (politikada yazılı); WP belgesi kapsam kaynağı olan bir kuyruk çıkarsa o belgenin sürüm bağı eklenir |
| K-6 | PR #176 (PR-1) merge | **Yapıldı** — #176 (2026-10-21 08:26) ve #182 (11:24) kıdemli geliştirici merge etti |
