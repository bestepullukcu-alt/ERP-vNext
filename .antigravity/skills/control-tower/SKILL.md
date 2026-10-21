---
name: control-tower
description: "Bir modül için Control Tower başlangıcı. Repo, HEAD ve araçların varlığını ölçer; modülü salt okunur araştırır (Blueprint, domain-config, module pack, görev kaynakları, mükerrer iş keşfi). Var olan modülde onayın kapsamını ve iddia edilen uygulamayı kod ve kanıt sürümüyle karşılaştırır; olmayan modülde parent / FU / yeni / aday ayrımını ve gerekçeli tek bir ilk kapsam önerisini sunar. Kullanıcı bir modülü başlatmak, sürdürmek ya da durumunu denetletmek istediğinde kullan (örnek: 'Employee modülünü yapalım'). Salt okunur: kod yazmaz, kuyruğa ya da kayda yazmaz, onay, paket ya da dağıtım izni vermez."
---

# control-tower — bir modülün Control Tower başlangıcı

Çağrı: Claude Code `/control-tower <modül adı>`, Codex `$control-tower <modül adı>`; ikisi de yoksa
`@.antigravity/skills/control-tower/SKILL.md <modül adı>`. Modül adını çağrının kendi metninden oku; ad yoksa yalnız
onu sor.

Bu skill yeni yetki yaratmaz. Kurallar `AGENTS.md`, CONTROL TOWER SOP ve işletim kartındadır
(`docs/guides/operations/control-tower-operating-card.md`). Komutlar kartın "Bugün hangi komut" tablosunda ve §0'dadır;
burada kopyalanmaz, oradan okunur. İlk çağrı salt okunurdur.

## 1. Başlangıç ve kimlik

- Dal, HEAD ve `git status --porcelain` çıktısını kendin ölç; bunları ve kaynaklardan okunabilen hiçbir alanı kullanıcıya
  sorma.
- Önce yeri ölç: `git fetch` sonrası ağaç `origin/main`'in gerisindeyse ya da paylaşılan ana klasördeysen dur; main'den
  ayrı bir worktree ve dal iste. O ana kadar hiçbir dosya yazma, geçici dosya dahil; ana klasörde dal değiştirme.
- Kartın tablosundaki araçların bu ağaçta olup olmadığını ölç. Olmayanı "bu ağaçta yok" diye yaz; çalıştırmış gibi
  davranma, yerine kendi kontrolünü uydurma.
- Görev kaynağını ve kaydını oku (kartın §0'ı): kuyruk ve kayıt satırı var mı, `main`'de yürürlükte mi. Yürürlükte
  değilse yalnız hazırlık önerilir (`--prepare` sınırı). Kayıt, sahiplik ya da onay uydurma.

## 2. Araştırma

- Yalnız işle ilgili girdileri oku: Blueprint ve modül kimlik kaydı, ilgili `domain-config.md`, module pack, görev
  kaynakları. Bütün depoyu tarama.
- Mükerrer ya da süren iş için kartın keşif komutunu koş. Erişilemeyen kaynak ya da ref'i yaz; görünmeyen işi
  doğrulanmış sayma. Çağrıdaki ad bir domain'se keşfi o domain'in modülleriyle tek tek koş; domain adıyla tek keşif
  yetmez.
- Sahip kararlarını ara: modül kimlikleri ve domain adıyla `git log --grep` ve `docs/records/decisions/` içinde. Bulduğun
  her kararı kaynağıyla (`yol@commit`) yaz ve "hâlâ geçerli mi? Evet / Hayır" diye sor; sahibin hatırlamasını bekleme.
- Keşfi bir kez koş ve çıktıyı ekrandan oku; geçici dosya dahil hiçbir dosyaya yönlendirme. Aynı kaynak ve sorguyla
  tekrar koşma; kaynak değiştiyse ya da teknik bir hata olduysa gerekçesini yazıp bir kez yeniden koşabilirsin. Çıktı
  kesildiyse "kısmi" de.
- Önceki bir keşif raporu: `--check-report` yalnız bütünlüğü kanıtlar, güncelliği değil. Raporu yalnız aynı iş için,
  HEAD, kirli kaynak baytları ve ref kimliklerinin değişmediği ölçüldüyse yeniden kullan; yeni işte ya da bunlar
  kanıtlanamıyorsa yeniden keşfet.

## 3. Var olan modül

- Mevcut kimliği ve paketi kullan; yeni ya da mükerrer bir kimlik açma.
- Paketin `status` alanını tek başına okuma; her çağrıda paketin güncel `approval_status` alanını ve kapsam metnini de
  oku. Onaylı dilimi ve dışarıda kalanı ayrı yaz. Onayı bütün modüle yayma. (Tarihsel örnek, 2026-10-18: MOD-0251
  paketinde yalnız P2 draft / reference-validation dilimi onaylıydı; bugünün izni paketin kendisinden okunur.)
- Belgede iddia edilen uygulamayı ilgili kodla ve kanıtın kaydedildiği sürümle karşılaştır (`dosya:satır@commit`).
  Çalışma testi yapılmadıysa "çalışma testi yapılmadı" yaz; okumadan "doğru" deme.
- Module pack'i (ya da DCP'yi) okurken bu paketin kendi yoluyla bağımsız inceleme kaydı olup olmadığını da oku (`execution/approvals/reviews.tsv`). Kayıt yoksa, paket eski de olsa yeni de olsa, bunu söyle ve pakette yeni iş başlamadan önce bağımsız inceleme öner (SOP §20.4); yalnız önerir, incelemeyi çalıştırmaz ve yazmaz.

## 4. Olmayan modül

- Bulguları parent / FU / yeni / aday olarak ayır. Kimlik uydurma; kaynak Blueprint'tir.
- Blueprint'te var ama pack yoksa: kod değil, taslak module pack hazırlama adımını öner. Blueprint'te de yoksa: parent /
  FU uygunluğunu araştır; uymuyorsa dur ve Master'da Module Change Request öner (Master-first kuralı, Blueprint 8.7); yeni
  `CAND-CAP` ya da ID uydurma.
- Gerekçeli tek bir ilk kapsam öner ve "öneri — onay değil" diye işaretle. Tek modül için Delivery Capability Pack
  oluşturma.

## 5. Sınırlar

- Kapsam önerisi, paket taslağı, kod geliştirme ve dağıtım dört ayrı yetkidir; bu skill hiçbirini vermez. Sahibin onayı
  olmadan ürün kodu, commit, push ya da PR yok.
- İlk çağrıda kuyruğa ya da kayda yazma, `guard` ya da prompt taslağı dosyası yok; geçici dosya dahil hiçbir dosyaya
  yazma. Görev, test, kabul ve başka bir
  MVP'ye soru için kartın ilgili satırını ve aracını kullan; yeni yetki yaratma, zorunlu kapıları atlama.
- Alt ajana, alt oturuma ya da worker prompt'una iş verirken her biri için modeli ve effort'u yaz ve kullanılanı ölç
  (SOP §17.1 r5); karmaşıklık modeli, risk incelemeyi belirler.
- Module Pack ya da DCP onaya gitmeden önce bağımsız incelenir (SOP §20.4); WP prompt'u `Kapsanan dilim` yazar ve paketin
  tamamını tek WP'ye vermez.
- Geliştirici bağımsız inceleme sonucunu (bulgular + UYGUN / KOŞULLU / UYGUN DEĞİL) getirirse her bulguyu paket ve kodla kendin
  karşılaştır: hangilerini kabul ettiğini, hangilerine kaynağıyla itiraz ettiğini (yanlış, abartılı, kapsam dışı) ve
  incelemenin neyi kaçırdığını yaz; bulgu dosyasını ya da herhangi bir kaydı yazmadan önce sahibe sor.
  İtirazlarını sonraki inceleme isteğine ekle, yanıtı kaydet.
- Pack/DCP'yi `module-pack-author` yazar (model + effort yaz, SOP §17.1 r5); sen yazma, sonucu denetle.
- Sen karar verir, ölçer, denetlersin; işi devredersin. Kod işi için bağlı alt oturum aç, kapıdan geçmiş prompt'u
  model ve effort'la ver, ilerlemesini izle; prompt'u sahibe taşıtma. Kendin yalnız küçük kayıt işi yaparsın.

## 6. Yanıt biçimi

İlk yanıt kısadır (hedef yaklaşık 12 satır) ve bu sıradadır:

1. Tek satırda durum: dal, HEAD, görev kaydı.
2. Bilinen eksik ya da okunamayan kaynaklar, adlarıyla; eksik varsa "kısmi inceleme". Sayısını bilmiyorsan sayı uydurma.
3. Kritik çakışma ya da engel (kimlik çakışması, onaysız görünen kod, kayıtlı kuyruğun yokluğu): satır hedefi bunları
   gizlemez.
4. Gerekçeli tek öneri, "öneri — onay değil".
5. Yalnız kullanıcı kararı gerekiyorsa, en çok bloklayan kararı tek soruyla sor; gerekmiyorsa soru uydurma. Soru
   "evet" ve "hayır"ın neyi yetkilendirdiğini söyler (ör. "evet: satırı yazar, commit, push, PR; merge sizde");
   kullanıcıdan uzun onay metni beklenmez.

Block'un iş takip sayfası varsa her kabul, prompt ve karardan sonra onu depodan güncelle (SOP CT-TRK-01).

Kısa yanıtta da olguyu çıkarımdan ayır. Kaynak tablosu, olgular (`kaynak:satır`), çıkarımlar ve aday listesi istenince
ayrıntılı verilir. Satır numarasını yalnız numaralı bir okumadan ver (`grep -n`, `nl -ba` ya da okuma aracının satır
numarası); yoksa yalnız `yol@commit` yaz.

İncelenen kaynak tablosundan geniş "tam inceledim" deme. Zaman ya da token tasarrufu garantisi verme.
