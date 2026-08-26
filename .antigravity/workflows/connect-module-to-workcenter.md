---
description: "WORKFLOW-022 — Bir modülün işini Görev Merkezi'ne bağlama (sağlayıcı yazma) akışı"
---

# Workflow: Modülü Görev Merkezi'ne Bağla

Bir modülün işi, **bitirildiği için** Görev Merkezi'ne ulaşmaz. **Yansıtıldığı için** ulaşır.

Bu akış, o yansıtmanın nasıl kurulacağını değil, **kurmadan önce neyin ölçüleceğini** tanımlar.

> ⚠ **BU DOSYADA BİLEREK RAKAM VE DURUM TESPİTİ YOKTUR.**
>
> "Bugün iki sağlayıcı var", "şu fiil yok", "şu bağlı değil" gibi cümleler bir hafta içinde
> yalan olur ve kimseyi uyarmadan yalan kalır. Bu depoda üç günde üç kez yaşandı: bir manifest
> yorumu iki tur sonra altındaki satırla çelişti, bir metot açıklaması araya kod girince
> başka bir metodun üstünde kaldı, yazılı bir sayım yeni bir çağıran gelince testi kırdı.
>
> Bu yüzden aşağıdaki her madde **bir ölçüm talimatıdır**, bir bilgi değil. Cevabı sen
> koddan alacaksın. Böylece bu dosya yanılamaz.
>
> Günün durumu (bugün neyin eksik olduğu) `docs/product-backlog.md` içindedir — orada
> tarihi vardır ve kapandığında kapandığı yazılır.

---

## 📖 0. Önce Bunu Oku

`execution/portfolio/delivery-capability-packs/DCP-004-provider-onboarding-note.md`

Alan eşleme tablosu, izin tuzağı ve aksiyon listesi oradadır. **Bu dosya onu tekrar etmez** —
içerik iki yerde durursa biri eskir ve hangisinin eskidiği belli olmaz.

---

## 🔍 1. Kod Yazmadan Önce Ölçülecekler

Her maddenin cevabını **koddan** al. Tahmin etme, hatırlama, bu dosyaya sorma.

### Ö1 — Bağlantı noktası ne yapıyor, ne yapmıyor?
`Features/WorkAggregation/Providers/IWorkItemProvider.cs` dosyasını **aç ve oku**.
- Kaç metot var? Yazma (write) metodu var mı?
- Arayüzün kendi yorumu ne diyor?

⚠ Bu arayüz salt okumadır ve öyle kalması bir tasarım kararıdır. Sağlayıcı iş durumu yazmaz;
modül kendi yazma yollarını ve kendi ekranlarını tutar.

### Ö2 — Bugün kaç sağlayıcı var, nerede yaşıyorlar?
`AddScoped<IWorkItemProvider` satırlarını **say** ve hangi projede olduklarına bak.
- Hepsi aynı derlemede mi?
- Senin modülün o derlemenin içinde mi, dışında mı?

⚠ Bu **aynı süreç içi bir DI bağlantısıdır, ağ bağlantısı değil.** Kendi servisi olarak koşan
bir modül o container'a kayıt olamaz — Platform'un içinde onu çağıran ince bir sağlayıcı gerekir.
Böyle bir örnek var mı, **ölç**: yoksa deseni sen kuruyorsun.

### Ö3 — Senin aksiyonların gerçekten çağrılacak mı?
`wwwroot/assets/js/WorkCenterNext/app.js` içinde `isRealTaskItem` tanımını **aç**.
- Hangi koşul gerçek bir sunucu çağrısına yönlendiriyor?
- **Senin sağlayıcı kodun o koşuldan geçiyor mu?**

⚠ Geçmiyorsa işlerin ekranda görünür ama düğmelerin hiçbir yere gitmez. Kod bunu tarayıcı
konsoluna yazar — **kod yazmaya başlamadan önce bir kez tıklayıp konsola bak.**

### Ö4 — Yaşam döngüsünün sahibi kim olacak?
`WorkItemProjectionDto` içindeki `LifecycleOwner` alanını ve mevcut sağlayıcıların ona ne
yazdığını **oku**.

⚠ Kendi yaşam döngün varsa **kendi sağlayıcı kodunu yaz** ve döngüyü sahiplen. Görev
sözlüğüne düzleştirme. Görev Merkezi işi gösterir, otorite sende kalır.

### Ö5 — Kiracı bilgisini nereden alacaksın?
`WorkItemActor` tipini **aç ve alanlarını say**.
- Kiracı bilgisi orada mı?
- Değilse üründe hangi seam'den geliyor, servisler arası hangi başlıkla taşınıyor?

⚠ Actor'da bulamazsan bu bir eksik değildir — yanlış yere bakıyorsundur.

### Ö6 — Sonuç kime bildirilecek?
Bir geçiş (transition) handler'ını **baştan sona oku**. İşini bitirdikten sonra dışarı bir
şey gönderiyor mu?

⚠ Cevap "hayır" ise panik yapma: kullanıcı düğmeye bastığında **senin kendi ucun** çağrılıyorsa
karar zaten sende verilir ve bildirilecek bir şey kalmaz. Önce Ö3'ü çöz, sonra bu soruyu
tekrar sor — çoğu zaman kendiliğinden düşer.

### Ö7 — Kullanacağın fiiller gerçekten var mı?
Kullanmayı düşündüğün her fiil için `WorkflowTransitionAction` enum'unu **oku**.

⚠ Enum'da olmayan bir fiili varsayma. Yoksa ya kendi yaşam döngünde tutacaksın ya da
eklenmesi ayrı bir iştir. Bu depoda "sunucusu olmayan bir fiil" ekranda haftalarca durdu.

### Ö8 — İzin anahtarları
`RequiredActionPermissions` listesi ile sağlayıcının **fiilen sorguladığı** anahtarları
yan yana koy ve karşılaştır.

⚠ Beyan edilmeyen bir anahtar hiç değerlendirilmez; izni gerçekten olan bir kullanıcı için
sessizce "reddedildi" olarak yansır. Bu varsayımsal değil, bir kez yaşandı.

---

## 📋 2. Kod Yazmadan Önce Teslim Edilecekler

`DCP-004` §5'in istediği beş şey. Sırası önemli — beşincisi diğer dördünün yalanını çıkarır.

1. Alan eşleme tablosu — **üç sütun**: modüldeki değer · burada ne olur · **yokken ne olur**
2. Yetenek listesi — her birinin arkasındaki veriyle birlikte
3. Aksiyon listesi — uç · hangi durumlarda sunulur · izin · reddedilince ne denir
4. İzin anahtarları — `RequiredActionPermissions` ile karşılaştırılmış
5. **Bir gerçek iş öğesi, elle, uçtan uca yansıtılmış**

⚠ Üçüncü sütun ("yokken ne olur") atlanan sütundur ve acıtan sütundur. Boş bırakılan bir alan
ekrandan kaybolmaz — kendinden emin bir sıfır, boş bir kart veya gerekçesiz kapalı bir düğme
olarak çizilir.

⚠ 5. madde bir alanın kaynağının olmadığını öğrenmenin en ucuz yoludur. Bir saat sürer ve
bu depoda atlanan tam olarak o adımdır.

---

## ⚖️ 3. Teknik Mühürler (Guards)

| Kural | Nasıl korunur |
|---|---|
| Sağlayıcı iş durumu **yazmaz** | Cümle olarak değil, **muhafız testi** olarak. Bir cümle test edilemez |
| Yetenek, **verisi varken** beyan edilir | Verisi olmayan yetenek her öğede boş çizen bir kart üretir — "kimse yapmamış" diye okunur, "sistem tutmuyor" diye değil |
| Aksiyon ikonu **tek haritadan** gelir | Çağrı yerinde seçilen ikon, aynı aksiyona iki yüzeyde iki farklı görünüm verir |
| Sözleşme sürümü **el sıkışmadır** | Tanınmayan sürümdeki sağlayıcı sessizce atlanır; hata vermez. Sürümünü ölç |

---

## 🚦 4. Bağlandıktan Sonra Doğrulama

Ekranı aç ve **say** — koda bakarak değil, ekrana bakarak:

1. Sağlayıcının döndürdüğü sayı · sekmedeki rozet · listedeki satır — üçü aynı mı?
2. Bir öğenin detayını aç: beyan ettiğin **her** yetenek için kart çiziliyor mu?
   Boş çizen kart = verisi olmayan yetenek beyan edilmiş.
3. Sunduğun **her** aksiyona bas — başarısız olması beklenenler dahil. Kullanıcıya ne deniyor?
4. Her yazmadan sonra **sayfayı yenile** ve değişikliğin kaldığını gör.

⚠ Bu modülde beş yol yalnızca tarayıcıya yazdı. Beşi de yenileme ile bulundu, testle değil.

⚠ **İDDİA ETTİĞİN YÜZEYİ ÖLÇ.** Bir sıralama kontrolü, tabloya ait bir nitelik üzerinden
"çalışıyor" diye raporlandı; sıralaması gereken liste hiç yeniden dizilmedi. Kullanıcının
gördüğü şeyi doğrula, okuması kolay olanı değil.

---

## 🔗 İlgili

- `execution/portfolio/delivery-capability-packs/DCP-004-provider-onboarding-note.md` — sözleşme
- `docs/product-backlog.md` — **günün** açık maddeleri (tarihli; bu dosya tarihsizdir)
- `.antigravity/rules/module-self-registration-standard.md` — manifest tarafı
