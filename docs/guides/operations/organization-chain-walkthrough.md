# Sıfırdan Organizasyon Zinciri — Test ve Kullanım Turu

> Boş bir sistemden başlayıp görev atanabilir hale gelene kadar, ekran ekran.
>
> İki işe birden yarar: **kullanmayı öğrenmek** ve **canlıda yeni kiracı açılınca
> çıkacak sorunları önceden bulmak**. Her adımda "hiç veri yokken ne görünüyor"
> sorusu da sorulur — boş-durum hataları ancak böyle yakalanır.
>
> Ortam: **yerel** (`localhost:5001`). Canlıya dokunulmaz.

---

## 0 · Önce yedek, sonra silme

⚠ **Yedek almadan silme.** 178 görev, 215 atama, 48 yorum var.

```bash
mongodump --db diten_personalization_dev --out ~/Desktop/diten-yedek-$(date +%Y%m%d-%H%M)
mongodump --db DitenERP_Dev --out ~/Desktop/diten-yedek-$(date +%Y%m%d-%H%M)
```

⚠ **Servisleri yeniden başlatma.** `PositionSeed` ve `PositionAssignmentSeed`
geliştirme ortamında hâlâ çalışıyor ve **yalnız açılışta** koşuyor. Silip
yeniden başlatırsan `HEADQUARTERS` birimi ve beş sahte pozisyon (CEO, CTO,
HR_MGR, DEV_LEAD, DEV_ENG) geri gelir, temiz başlangıç bozulur.

**Görev ve organizasyon zinciri:**

```bash
mongosh --quiet diten_personalization_dev --eval 'var c=["task_personal_overlays","task_dependencies","task_watchers","task_comments","task_assignments","task_recurrence_rules","task_items","approval_tasks","workflow_transition_logs","workflow_runtime_assignment_snapshots","workflow_instances","organization_field_values","organization_field_definitions","position_assignments","positions","organization_units","organization_structure_tokens"];c.forEach(function(n){if(db.getCollectionNames().indexOf(n)>-1){print(n+" : "+db.getCollection(n).deleteMany({}).deletedCount)}});'
```

**Tüzel kişilikler:**

```bash
mongosh --quiet DitenERP_Dev --eval 'print("mdm_legal_entities : "+db.mdm_legal_entities.deleteMany({}).deletedCount)'
```

Silinmeyenler ve sebebi: `task_types`, `task_templates`, `workflow_templates`,
`task_field_definitions` **şablondur, veri değildir** — silinirse görev
oluşturulamaz. Tüzel kişiliğin seed'i yoktur, silinen geri gelmez; bağımlı CRM
veya HCM kaydı da yoktur (ölçüldü: o koleksiyonlar hiç mevcut değil).

---

## Zincir neden bu sırada

```
Tüzel kişilik → Birim → Alan tanımı → Pozisyon → Atama → Görev
```

Her adım bir öncekine dayanır. **Alan tanımı üçüncü sırada**, çünkü tanım
yoksa birim formunda "Özel alanlar" bölümü hiç görünmez ve test edilemez.

Veri, yöneticinin kendi defterinden (`GMG-CGV-LOG-0005`) alınmıştır — uydurma
değil, gerçek yapının küçültülmüş hali.

---

## 1 · Tüzel Kişilik → `/LegalEntities`

**Önce boş ekrana bak.** Hiç kayıt yokken liste ne diyor? "Kayıt yok" gibi bir
mesaj mı, boş bir tablo mu, yoksa hata mı? Yeni kiracı bu ekranı ilk böyle
görecek.

İki kayıt gir:

| Kod | Yasal ad | Görünen ad |
|---|---|---|
| `LE-003` | Miquel y Garriga S.A. | Miquel y Garriga |
| `LE-004` | Grand Medical Poland Sp. z o.o. | Grand Medical Poland |

İkisi de üretim tesisi — birazdan aynı işlevin iki sahada nasıl ayrıştığını
göreceğiz.

**Bak:** kod benzersizliği çalışıyor mu (aynı kodu ikinci kez dene) · zorunlu
alanlar işaretli mi · kaydettikten sonra listede görünüyor mu.

---

## 2 · Organizasyon Birimi → `/OrganizationUnits`

**Boş ekran kontrolü:** tüzel kişilik var ama birim yok. Liste ne diyor?

Beş birim, bu sırayla (üst birim olanlar önce):

| Kod | Ad | Tip | Tüzel kişilik | İşlevsel üst | İdari üst |
|---|---|---|---|---|---|
| `GRP-Q` | Grup Kalite | **Grup fonksiyonu** | LE-003 | *(boş)* | *(boş)* |
| `MTO-MYG` | Üretim ve Teknik Operasyonlar — MYG | Departman | LE-003 | *(boş)* | *(boş)* |
| `MTO-PL` | Üretim ve Teknik Operasyonlar — Polonya | Departman | LE-004 | *(boş)* | *(boş)* |
| `QC-MYG` | Kalite Kontrol Laboratuvarı — MYG | Departman | LE-003 | **Grup Kalite** | **MTO-MYG** |
| `QC-PL` | Kalite Kontrol Laboratuvarı — Polonya | Departman | LE-004 | **Grup Kalite** | **MTO-PL** |

⚠ **Son iki satır bu turun kalbi.** İki kalite laboratuvarı *işlevsel olarak*
aynı Grup Kalite'ye, *idari olarak* kendi tesislerine bağlı. Matris raporlama
tam olarak budur ve tek hatlı bir sistemde ifade edilemez.

**Bak:**
- `Grup fonksiyonu` tip listesinde çıkıyor mu
- `QC-MYG`'yi kaydet, tekrar aç — **tipi ve iki hattı korunmuş mu**
- İdari üstü **temizle**, kaydet, tekrar aç — **boş kalmalı**. İşlevsel üstün
  adı oraya yazılıyorsa bu bir hatadır (bkz. MOD-0288-FU02 karar 2)
- İki hatta **aynı** birimi seç — kabul edilmeli, engellenmemeli
- Bir birimi kendine üst yapmayı dene — reddedilmeli

---

## 3 · Özel Alan Tanımı → `/Organization/FieldDefinitions`

**Boş ekran kontrolü:** hiç tanım yokken ekran ne diyor, "+ Yeni" görünüyor mu.

Üç tanım — yöneticinin yedi yönetişim alanından seçilmiş, üç farklı tip:

| Kod | Ad | Tip | Zorunlu | Sorgulanabilir |
|---|---|---|---|---|
| `permanent.ou.id` | Kalıcı birim no | Metin | Hayır | Evet |
| `regulatory.role` | Düzenleyici rol | **Tek seçim** — `GMP`, `GDP`, `PV`, `Yok` | Evet | Evet |
| `it.directory.mapped` | BT dizinine eşlendi | **Evet / Hayır** | Hayır | Hayır |

**Bak:**
- Sekiz tip listede çıkıyor mu, adları onaylanan kelimeler mi
- `Tek seçim` seçince seçenek editörü açılıyor mu
- Kaydettikten sonra **kodu değiştirmeyi dene** — düzenlemede kapalı olmalı
- `Düzenleyici rol`'ü pasifleştir, sonra geri aç — davranışı gör

---

## 4 · Birime dön ve alanları doldur → `/OrganizationUnits`

`QC-MYG`'yi düzenle. Formun altında **Özel alanlar** bölümü artık görünmeli.

| Alan | Değer |
|---|---|
| Kalıcı birim no | `OU-0037` |
| Düzenleyici rol | `GMP` |
| BT dizinine eşlendi | Evet |

**Bak:**
- Bölüm 3. adımdan **önce** yoktu, şimdi var — yeniden başlatma gerekmedi
- Seçim kutularında "Seçiniz…" ipucu var mı, metin kutularında ipucu var mı
- Zorunlu alanı boş bırakıp kaydetmeyi dene
- Kaydet, tekrar aç — değerler duruyor mu

---

## 5 · Pozisyon → `/Positions`

| Kod | Ad | Birim |
|---|---|---|
| `QC-MGR-MYG` | Kalite Kontrol Müdürü — MYG | QC-MYG |
| `QC-ANALYST-MYG` | Kalite Kontrol Analisti — MYG | QC-MYG |
| `GRP-Q-DIR` | Grup Kalite Direktörü | GRP-Q |

⚠ **Durumu kontrol et.** Pozisyon `Taslak` durumunda kalırsa atama ekranında
görünmez — canlıda tam bu yüzden "kimseye atayamıyorum" yaşandı. Kaydettikten
sonra listede durum sütununa bak.

---

## 6 · Pozisyon Ataması → `/PositionAssignments`

| Kişi | Pozisyon | Tip | Başlangıç |
|---|---|---|---|
| *(kendi kullanıcın)* | `QC-MGR-MYG` | Birincil | bugün |
| *(ikinci kullanıcı)* | `QC-ANALYST-MYG` | Birincil | bugün |

**Bak:** kişi arama çalışıyor mu · pozisyon listesi yalnız aktifleri mi
gösteriyor · geçerlilik tarihi zorunlu mu.

⚠ Atama yoksa **görev atanamaz** — zincirin en sık kopan halkası burasıdır.

---

## 7 · Görev → Görev Merkezi

Bir görev oluştur ve `QC-ANALYST-MYG` pozisyonundaki kişiye ata.

**Bak:**
- Atanacak kişi listesinde çıkıyor mu (çıkmıyorsa 5. veya 6. adımda kopukluk var)
- Görev karşı tarafta görünüyor mu
- Onaya gönder — akış çalışıyor mu

---

## 8 · İş Raporu → `/Tasks/WorkReport` — indirilen dosya ekranla aynı mı

Bu adım, İş Raporu dışa aktarmasının (BL-346) canlıda **ölçülememiş tek kriterini**
kapatır: dosyadaki sayı ekrandaki sayıyla aynı mı. Kodda iki taraftan sabotajla
korunuyor, ama gerçek veriyle hiç ölçülmedi — çünkü o gün veritabanında tek görev yoktu.

Önce **en az üç görev** olsun, en az biri **farklı bir tüzel kişilikte** (yoksa filtre
hiçbir şeyi daraltmaz ve 4. adım bir şey kanıtlamaz).

1. Filtresiz raporu aç, **Açılan** kartındaki sayıyı not et.
2. **İndir → CSV.** Dosyayı aç, `Opened` sütununu topla. **Kartla aynı olmalı.**
3. Tüzel kişilik filtresi uygula (ör. `LE-003`). Kart değişir — yeni sayıyı not et.
4. Tekrar indir, `Opened`'ı topla. **Yeni kartla aynı olmalı** ve 2. adımdakinden küçük.

⚠ 4. adımda dosyadaki sayı karttan **büyükse**, dışa aktarma kapsamı genişletiyor
demektir — ekranda 12 görüp 13 indirmek. Yanlış bir sayıdan daha kötüdür, çünkü
fark edilmez. Hemen bildir.

---

## Turun sonunda

Bu zincir baştan sona yürüdüyse **canlıda yeni bir kiracı açıldığında da
yürür**. Yürümediyse, kırıldığı adım canlıda da kırılacaktı — ve onu bulmak
turun asıl amacıdır.

Bulduğun her sorunu adımıyla birlikte not et: hangi ekran, hangi alan, ne
bekliyordun, ne oldu.

⚠ Ekran görüntüsü alırsan bu tur aynı zamanda **kullanım kılavuzunun kaynağı**
olur — kılavuz biçimi `.antigravity/agents/user-manual-generator.md` içindedir
(resimli, tek dosya HTML, `docs/guides/<modül>/`).
