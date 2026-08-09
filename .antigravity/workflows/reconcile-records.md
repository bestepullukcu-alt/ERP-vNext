---
description: "[Kayıt Mutabakatı — governance kayıtları koda karşı ölçülür, SALT OKUNUR]"
---
# Workflow: Reconcile Records (Kayıt Mutabakatı)

Governance kayıtlarının koddan geri kalıp kalmadığını **ölçer**. Düzeltmez.

Bu workflow, yazma-anı kurallarının kapatamadığı bir boşluk için var: demir kural #10 iş
bitince **kapanış kaydı** yazılmasını sağlar, ama var olan bir kaydın **gövdesinin**
zamanla yanlışa dönüşmesini engelleyemez. Kayıt yerinde durur, kod ilerler, ve kimse o
cümleyi yeniden okumaz.

**Ölçülmüş gerekçe (ilk koşu, 2026-07-31):** 7 backlog maddesi bayat/yanlış · MOD-0024
pack'inde **20 kutu** işaretsiz ama yapılmış · seam register'ın **5** satırı "yapılmıyor"
derken beşi de shipped · 37 pack'ten **30**'unun API dokümanı yok · bir kod yorumu
tamamlanmış işi "follow-up" diye anlatıyor.

> **Otorite:** `AGENTS.md §1` yetki hiyerarşisine tabidir.
> İlgili: [orchestrator demir kural #10](../agents/orchestrator.md) ·
> [read-only-audit](./read-only-audit.md)

---

## ⛔ 0. Bu workflow HİÇBİR ŞEYİ DÜZELTMEZ

Ajan yalnız ölçer ve raporlar. Backlog'a madde eklemez/düzeltmez, pack kutusu
işaretlemez, eksik dokümanı yazmaz, kod yorumu güncellemez.

**Gerekçe:** ajan kendi okumasına göre kaydı düzenlerse **ikinci bir doğruluk kaynağı**
doğar — kayıt ile kod arasındaki farkı kapatmak yerine, kayıt ile kaydın kendisi arasında
yeni bir fark açılır. Düzeltme kararını CONTROL TOWER verir; ölçüm ajanın, karar sahibin.

Tavsiyeli ajan: `@read-only-auditor`.

---

## 🔍 1. Backlog gövdesi ↔ kod

`docs/product-backlog.md` içindeki her `BL-xxx` ve `WC-x` maddesini oku.

**Madde adı ezberleme — DESENE bak.** Bir maddeyi şu üç işaretten biri taşıyorsa
doğrulanması gerekir:

- **Gövdesinde SAYI geçiyorsa** (ör. "7 aksiyon üretiyor", "3576 satır", "12 eksik") →
  o sayıyı bugün yeniden ölç. Sayı kodla birlikte kayar; kaydın en çürüyen parçası budur.
- **Gövdesinde dosya:satır referansı geçiyorsa** → o satır hâlâ o şeyi mi söylüyor?
- **"yok / yapılmadı / hiç" gibi bir yokluk iddiası varsa** → gerçekten yok mu?
  `grep` / `git log -S` ile ölç.

Ayrıca "YAPILDI" diyen her maddede verilen commit hash'ini `git show --stat` ile teyit et.

- **✅ ama doğrulanmamış** → demir kural #10 bir maddenin **canlıda doğrulandığında** kapanmasını
  şart koşar. `✅` taşıyan her kayıtta doğrulama izi ara: canlı ölçüm tablosu, ekran/uç nokta
  çıktısı, ya da CT doğrulama başlığı. Yalnız "testler yeşil" diyen bir ✅, **kapanmamış madde
  olarak raporlanır.** Ölçülmüş gerekçe: BL-043 ve BL-042 aynı gün ✅ kapatıldı, ikisinde de kod
  doğru ve testler yeşildi, ikisinin de akışı canlıda çalışmıyordu (BL-050, BL-051).

**Çıktı — Tablo 1:** `BL/WC no | maddenin iddiası | koddaki gerçek | BAYAT/YANLIŞ | kanıt`
Yalnız uyuşmayanları listele; uyuşanları tek satırda say.

---

## 📋 2. Pack kabul kriterleri ↔ kod (İKİ YÖNLÜ)

`execution/domains/**/module-packs/*.md` içindeki `## Acceptance Criteria` bölümlerinin
`- [ ]` / `- [x]` kutularını denetle.

İki yön de ölçülür ve **ayrı raporlanır**:

- **İşaretsiz ama YAPILMIŞ** — kayıt geride; düzeltmesi ucuz.
- **İşaretli ama YAPILMAMIŞ** — ⛔ **tehlikeli yön.** Bitmemiş iş bitmiş görünüyor
  demektir. Bu satır bulunursa raporun en üstüne taşınır.

**Çıktı — Tablo 2:** `pack | kutu metni | gerçek | yön | kanıt`

---

## 🔗 3. Seam register ↔ kod

`docs/product-backlog.md` sonundaki "WorkCenter ön-koşulları (seam register)" bölümü
ve benzeri "bu branch'te yapılmıyor" ifadeleri taşıyan her kayıt.

Her seam için: arayüz/uygulama kodda var mı? Varsa hangi commit'te?

**Çıktı — Tablo 2'ye eklenir.**

---

## 📄 4. Doküman kapsamı

`status: approved` veya `ready-for-dev` olan her module pack için:

1. Pack'in **beyan ettiği** kod klasörü gerçekten var mı? (Kendi "eksik kod" ölçütünü
   uydurma; yalnız pack'in yazdığı yola bak.)
2. API dokümanı var mı?
3. Kullanıcı kılavuzu var mı?
4. Varsa son değişiklik tarihi.

**Ad kalıbını varsayma** — önce `docs/` altındaki fiili kalıpları tara, sonra ona göre ara.
İlk koşuda **8 farklı kalıp** bulundu; kalıp sayısı da raporlanır (artıyorsa düzen bozuluyor).

**Çıktı — Tablo 3:** `MOD no | status | kod | API dok | kullanıcı kılavuzu | son tarih`

---

## 💬 5. Bayat kod yorumları

Kod yorumlarında geleceğe atıf yapan ifadeleri ara: `TODO`, `follow-up`, `not yet`,
`deferred`, `henüz`, `sonra`, `Phase N'de yapılacak`.

Her biri için: o iş **hâlâ** yapılmamış mı, yoksa yapılmış da yorum mu kalmış?

**Çıktı — Tablo 4:** `dosya:satır | yorum ne diyor | gerçek | kanıt`

---

## ✅ 6. Kabul edilmiş sapmalar — GÜRÜLTÜYÜ ÖNLEYEN PARÇA

Aşağıdakiler **bilinçli kararlardır, ihlal değildir.** Denetim bunları ayrı bir bölümde
sayar; ihlal listesine **karıştırmaz**.

| Sapma | Gerekçe | Nerede kayıtlı |
|---|---|---|
| `submitReview` kebab-case değil | Aksiyon kodu = URL segmenti; sözleşme dağarcığı camelCase | MOD-0024 pack §14 |
| `ArgumentNullException.ThrowIfNull` yok (`Features/Tasks/Handlers`) | Modül geneli desen, bu turda başlamadı; ayrı dilim | BL kaydı bekliyor |
| Dosya başına birden çok public sınıf (`Features/Tasks`) | Aynı; modül geneli | BL kaydı bekliyor |
| `Response<T>` zarfı, ProblemDetails değil | `response-envelope.md` ile `api-conventions.md` çelişiyor; repo geneli pratik | iki kural dosyası |
| SLA "yaklaşıyor" sınırının yarım gün kayması | Gerçek takvim gelince "gün başı" anlamını yitirir | BL-041 |
| `docs/platform/` altında tenant modülü | İsim tarihsel; taşıma 21 dosyanın linkini kırar | CT kararı bekliyor |

> ⛔ **Bu listeye ekleme yalnız CONTROL TOWER kararıyla yapılır.** Ajan kendi kararıyla
> bir bulguyu "kabul edilmiş" sayamaz.
>
> **Neden bu liste var:** sürekli aynı şeyi "ihlal" diye raporlayan bir kapı gürültüye
> dönüşür ve okunmaz olur. Bu depoda ölçülmüş örneği var: `add-module.md:166`'daki
> dokümantasyon ⛔ blocker'ı **72 gündür** atlanıyor.

---

## 📊 7. Rapor biçimi

Dört tablo + sonda **dört sayı**:

1. kaç bayat/yanlış kayıt
2. kaç uyuşmayan pack kutusu (iki yön ayrı)
3. kaç kabul edilmiş sapma
4. kaç modülün dokümanı eksik

Bu dört sayı **zaman içinde izlenir.** Artıyorsa disiplin bozuluyor demektir; ilk koşunun
temel değerleri: **7 · 26 · 6 · 30**.

Her bulgu üç şey taşır: **nerede yazılı** (dosya:satır) · **ne diyor** · **koddaki gerçek**
(ölçüm komutuyla). Böylece CONTROL TOWER raporu okuyup doğrudan düzeltebilir, yeniden
ölçmek zorunda kalmaz.

**Emin olunmayan satır "kesin" diye yazılmaz** — "emin değilim" sütunu kullanılır ve nedeni
tek cümleyle yazılır. Tahmin, ölçümden kötüdür.

---

## ⏱️ 8. Ne zaman koşar

1. **Modül kapanışında** — orchestrator teslim raporundan önce. Bayat kayıt varsa modül
   kapanmaz.
2. **Periyodik** — ayda bir, ya da CONTROL TOWER istediğinde.

Sıklık, birikmeyi engelleyen şeydir: aradaki fark küçük olduğunda rapor da küçük olur.

---

## ⚠️ 9. Bu workflow'un kendi bakımı

Buradaki ölçüm talimatları kod yapısı değiştiğinde kırılabilir ve denetim **sessizce yanlış
ölçer**. Bunu azaltmak için talimatlar bilinçli olarak **spesifik madde adına değil,
desene** bağlanmıştır (§1: "gövdesinde sayı geçen her madde", "yokluk iddiası taşıyan her
madde"). Yeni maddeler böylece otomatik kapsama girer.

Yine de: bir kontrol **hiç bulgu üretmiyorsa**, kontrolün kendisinin kırılmış olma
ihtimalini raporda belirt. Sıfır bulgu, ya temizlik ya körlüktür — ikisi ayırt edilmelidir.
