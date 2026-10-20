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
| S-5 | İnceleme kaydı olmayan pakete iş dağıtılabiliyor (prompt kapısı durdurmuyor) | Setup (politika) | 🟡 | sahip + Codex | açık — Codex görüşü bekleniyor |
| S-6 | SOP §20.4 inceleyenin depoya erişimini söylemiyor; CT'nin prompt'u Codex'in yerel çalışma ağacını görebildiğini varsaydı (Codex yerel depoyu görmüyor: ZIP ya da push edilmiş dal) | Model + setup | 🟢 | alt ajan (metin) | yapıldı — SOP §20.4/2: inceleyicinin erişimi (itilmiş dal ya da ZIP) |
| S-7 | Ana klasör main gerisindeyken açılan oturum skill'in eski kopyasını yükler; yeni kural ancak yeni oturumda görünür | Setup (işletim) | 🟢 | — | kabul: CT-START-03 bunu durdurur; testten önce ana klasör güncellenir |
| S-8 | Kuraldan önce açılmış CT oturumları yeni kuralı bilmez; main'i alsalar da oturum yenilenmeden skill değişmez; kapı durdurunca nedenini anlamayabilirler | Setup (işletim) | 🟡 | S-5 ile | açık — S-5'in her ENGEL mesajı kuralı, nedeni, sonraki adımı ve "main'i al, yeni oturum aç" ipucunu kendisi söyler |
| S-9 | CT'ye bağımsız inceleme sonucu getirildiğinde körü körüne kabul etmemesini söyleyen kural yok | Setup | 🟡 | alt ajan (metin) | yapıldı — skill §5 + SOP §20.4/4; çapa silinince --check FAIL |
| S-10 | İş merge edildikten sonra worktree'yi kaldırma kuralı SOP / kart / skill'de yok; 17 worktree birikmiş (7'si merge edilmiş ve temiz; 2'sinde push edilmemiş 10 commit) | Setup | 🟡 | alt ajan (metin) + sahip (temizlik) | yapıldı — SOP §16.5 + kart §6; temizlik sahibin onayında |
| S-11 | Kural paketi `module-pack-author` ajanına hazırlatır ama ajan Claude Code / Codex'e aktarılmamıştı; CT paketi kendisi düzeltti. CT'nin incelemeye itirazı inceleyene geri dönmüyordu | Setup | 🟡 | alt ajan (yapılandırma + metin) | yapıldı — ajan `.claude/agents/` ve `.codex/agents/`'a aktarıldı (model: inherit, CT model/effort yazar); skill §5 + SOP §20.4/2; skill bütçesi 7500 → 8000; çapalar silinince --check FAIL |
| S-12 | Kabul edilmiş bir iş (ABB listesi, BL-553: kabul `9020e844f`) main'e hiç girmemiş, yalnız yerel bir dalda duruyordu; kabul kaydı işin main'de olduğunu doğrulamıyor | Setup (işletim) | 🔴 | sahip + sonraki paket | açık — dal yedek olarak push edildi (PR yok); kural önerisi: kabul ya da kapanış, işin commit'inin main'de olduğunu ölçsün |
| S-13 | CT'nin rolü (karar verir, ölçer, denetler; işi devreder; kod işi için bağlı alt oturum açar, prompt'u sahibe taşıtmaz; yalnız küçük kayıt işlerini kendisi yapar) skill'de açık değil | Setup | 🟡 | sahip onayı (2026-10-21) | yapıldı — skill §5 + çapa; cümle silinince --check FAIL |

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
| K-2 | S-12 kabul edilmiş ama main'e girmemiş iş | SOP "kabul ≠ merge" der; kabulü merge'e bağlamak yerine "kabul edilmiş ama main'de olmayan" işleri listeleyen bir uyarı (ct_queue özeti ya da durum raporu) |
| K-3 | `abb-accept` (ABB listesi, 8 commit) yedek olarak push edildi; main'e alınıp alınmayacağı | MDM işinin sahibi karar verir |
| K-4 | Kaydedilmemiş iş içeren worktree'ler: platform-owned-mongo (12 dosya, BL-595), mdm+product-takeover (4 dosya), live-main (yerel ayar) | Sahipleriyle bakılmalı; silinmedi |
| K-5 | S-5 (dağıtım kapısı) Codex kod denetimi | Bağımsız inceleme düzeltmeleri bitince ZIP + prompt hazır olur; Codex'e sahip gönderir |
| K-6 | PR #176 (PR-1) merge | CODEOWNERS: CEO ya da kıdemli geliştirici |
