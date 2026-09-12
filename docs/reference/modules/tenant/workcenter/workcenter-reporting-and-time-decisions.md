# İş Raporu ve Zaman Takibi — Karar Kaydı

MOD-0024 (Görev Merkezi motoru) · 2026-09-03 · CONTROL TOWER

Bu belge bir plan değil, bir **karar kaydıdır**. Ekranın ne olduğu, sayıların
nasıl hesaplandığı, neyin bilerek yapılmadığı ve sıradaki dilimlerin hangi
sırayla geleceği burada. Her teknik iddianın yanında ölçüldüğü dosya ve satır
var; iddiayı yeniden ölçmeden değiştirme.

---

## 1. Ekran ne işe yarar

`/Tasks/WorkReport` bir **dönem aletidir**. Girdisi bir tarih aralığıdır, bu
yüzden *"şu an ne oluyor"* sorusuna yapısal olarak cevap veremez. Cevapladığı
soru şudur: **bir dönemde iş nasıl aktı, süreç nerede tıkandı.**

Ölçtüğü şey **süreçtir, kişi değildir.**

| kim | ne arar |
| --- | --- |
| Departman müdürü | tıkanma nerede |
| Süreç sahibi | görev türü tanımı düzeltme gerektiriyor mu |
| Kalite | denetim kanıtı ve düzeltme izi |
| Üst yönetim | şirketler arası kıyas |

### Yüzey ayrımı — bu ayrımı bozma

| soru | yer |
| --- | --- |
| Ahmet **şu an** ne üzerinde çalışıyor? | Görev Merkezi → Ekip kapsamı (BL-023) |
| Ahmet **geçen ay** kaç iş kapattı? | İş Raporu → kırılım = Atanan |

Rapor canlı liste değildir. Görev Merkezi rapor değildir. SAP'te de ayrım
aynıdır: Task Center canlı yüzeydir, raporlar ayrıdır.

---

## 2. Sayılar nasıl hesaplanıyor

Kaynak: `WorkReportTally.cs:148-186`

```
süre = (CompletedAt ?? CancelledAt) − CreatedAt        → TotalDays
```

| kural | davranış |
| --- | --- |
| Başlangıç | görevin **oluşturulduğu** an — atandığı ya da başlandığı an değil |
| Bitiş | tamamlandı **ya da iptal edildi** |
| Kapsanan işler | **kapanışı** dönem aralığına düşenler (açılışı değil) |
| Ortalama | aritmetik ortalama, 2 haneye yuvarlanmış (`:185`) |
| Negatif süreler | **atılıyor**, sıfıra çekilmiyor (`:159`) — sıfıra çekmek ortalamayı düşürür ve "çok verimli ekip" gibi okunur |

Sözleşmenin tamamı `WorkReportModels.cs`:

```
Flow        Opened · Closed · Completed · Cancelled · Unattended
CycleTime   AverageDays · ClosedCount
Timeliness  OnTime · Late · WithoutDueDate
Effort      EstimatedHours · SpentHours · TaskCount
Outcomes    Code → Count
Rework      TasksReturned · TotalReturns
```

### Bilinen iki kusur — karar bekliyor

**K-1 · İptaller ortalamanın içinde.** 90 gün bekleyip iptal edilen iş, "90
günlük kapanış süresi" olarak sayılıyor. Oracle bunu yalnız tamamlananlar
üzerinden ölçer. İptal süresi de bilgidir ("karar vermek 90 gün sürdü") ama
**aynı sayının içinde olmamalı.** Önerilen: ayrı göster.

**K-2 · Payda tutarsız.** Ekrandaki "N kapanan iş üzerinden" değeri
`closed.Count`, ortalama ise negatifler atıldıktan sonraki listeden
(`cycleDays`). Bozuk tek kayıt varsa ekran yanlış paydayı yazar.

---

## 3. Bilerek yapılmayanlar — bunları geri getirme

**Verimlilik oranı / puanı yasak.** Referans ekranlarda görev kapatılırken
kişiye `%0 verimlilik`, `0x çarpan` gösteriliyordu. Reddedildi (pack §8) ve
**testle kilitlendi**. İki gerekçe:

1. İşe yaramaz — kimse işi bitirirken kendi ortalamasını okumak istemez.
2. Zararlı — puan gösterilen insan puanı iyi görünsün diye davranır
   (tahminleri şişirir).

⚠ Timesheet geldiğinde bu yasak **aynen sürer**. "4 saat planlandı, 6 saat
harcandı → %150 → bu kişi verimsiz" aynı yasak şeyin başka kapıdan hâlidir.

**Kapatırken ayrı bir rapor ekranı yok.** Kapatırken **form** vardır (kişi
cevap verir); tek görevin hikâyesi **görev detayındadır**; desen **rapordadır**.
Üç ayrı soru, üç ayrı yüzey — aynı şeyin üç kopyası değil.

---

## 4. Eksikler — ölçülmüş, sıralanmış

### Ciddi

| # | eksik | neden önemli |
| --- | --- | --- |
| E-1 | **Tıklanamıyor** | cevap yalnız sayı taşıyor, görev kimliği taşımıyor. "10 geciken" der, *hangi 10* diyemez. Rapor bir yol değil, çıkmaz sokak |
| E-2 | **Açık işin yaşı yok** | termin ölçüsü yalnız tarihi olanları ölçüyor; terminsizler ölçünün tamamen dışında. Yaşlandırma olmadan yığın ölçülemez |
| E-3 | **Kıyas yok** | tek başına bir sayı karar verdirmez, yön verdirir. "3,99 gün" iyi mi kötü mü belli değil |

### Orta

| # | eksik | ölçüm |
| --- | --- | --- |
| E-4 | Medyan yok | tek uzun iş ortalamayı taşır; Oracle tam bu yüzden medyan raporlar |
| E-5 | Filtre yok | uç yalnız `from`, `to`, `groupBy` alıyor — tek birim/kişi/tür süzülemiyor |
| E-6 | Şirket boyutu yok | görev `OrganizationUnitId` taşıyor, şirket taşımıyor. Zincir ana veride var: `OrganizationUnit.LegalEntityId` — rapor bu join'i atmıyor |
| E-7 | Etiketler GUID | kırılım ekseninde ham kimlik görünüyor |
| E-8 | Grup tavanı/sıralaması yok | `Take` yok, `OrderBy` yok → "Atanan" kırılımında 500 kişi = 500 grup, sırasız |

### Sonra

Dışa aktarma (denetim kanıtı) · zamanın nerede geçtiği (bekleme vs çalışma) ·
kayıtlı görünüm ve otomatik e-posta.

---

## 5. Yetki ve kapsam

| anahtar | davranış |
| --- | --- |
| `platform.tasks.work-report.read` | raporu açar, **kendi kapsamını** görür |
| `platform.tasks.work-report.read-tenant-wide` | kapsamı aşar, **her şeyi** görür |

Kapsam `IDataScopeResolver` (MOD-0018-FU15) üzerinden çözülür; çözülemezse
**boş sonuç** döner, süzgeçsiz sonuç değil. Ekrandaki `scopeApplied` rozeti
bunu okunur kılar — boş bir grafiğin "iş yok" mu "benim göremediğim iş" mi
olduğu ayırt edilebilsin diye.

⚠ Bu bir **şirket duvarı değil, birim duvarıdır.** Org ağacı şirketlere göre
kurulduğu sürece şirket duvarı gibi davranır; ağaç karışırsa duvar da karışır.

`Company`/`LegalEntity` kapsamlarının görevde karşılığı olmadığı için bunlar
**daraltır, genişletmez** — hata yönü güvenli tarafta.

### Kişi bazlı kırılım — üç koruma

`Kırılım = Atanan` bugün mevcuttur. Sayım meşrudur, puanlama değildir.

1. Kişi kırılımında **medyan zorunlu** — tek uzun iş kimseyi yavaş göstermesin
2. **Hiçbir oran/puan yok** — yalnız adet ve süre
3. **Atanan kırılımı için ayrı yetki** — diğer kırılımlarla aynı anahtara bağlı olmasın

⚠ Üçüncüsü yalnız tasarım değil: şirket İsviçre'dedir ve ArG'ye bağlı OLT 3
Art. 26 çalışan davranışını izlemeye yönelik sistemleri kısıtlar. Bu bir hukuk
görüşü değil, **kişi bazlı ölçüm eklenmeden önce hukuka sorulacak bir sorudur.**
Ayrı yetki "bunu kim açtı" sorusunu da kayda geçirir.

---

## 6. Zaman takibi — bugünkü gerçek durum

```
EstimateHours                    elle giriliyor            ✓ var
SpentHours                       ELLE giriliyor            ⚠ sayaçtan gelmiyor
Kalan                            türetiliyor, saklanmıyor  ✓
TimeEntry / TimeSheet / WorkLog  ✗ HİÇ YOK
sayaç/timesheet API ucu          ✗ HİÇ YOK
```

⚠ **Ekrandaki sayaç bir vitrindir.** `foldTimer` (`app.js:690`) geçen süreyi
tarayıcı belleğindeki `item.timesheet.loggedMinutes`'a ekler. Sunucuya hiç
gitmez; sayfa yenilenince kaybolur.

⚠ **Bugünkü "plan vs gerçekleşen" kartı bir tahmini başka bir tahminle
karşılaştırıyor.** Ölçüm değildir — ve ölçüm gibi göründüğü için ölçüm
olmamasından daha kötüdür.

### Kararlar

**Z-1 · Tahmin elle girilir, öyle kalır.** SAP (PS/CATS'te planlanan `Arbeit`)
ve Oracle da elle girer. Tahmin bir ölçüm değil, **taahhüttür**; geleceği ölçen
alet yoktur. Eksik olan tahmin değil, etrafındaki iki şey:

- **İz** — "4 saatti, 15 Eylül'de 12'ye çıkarıldı, sebep: kapsam büyüdü".
  SAP orijinal plan sürümünü saklar. İz olmazsa tahmin sessizce *sayı iyi
  görünsün diye* değişir.
- **Geçmiş** — "bu türün son 30 işi ortalama 9 saat sürdü", **öneri olarak**.

**Z-2 · Sayaç doğrudan `SpentHours` yazmaz.** Akış:

```
sayaç (ham) → kişi onaylar (gerçek) → SpentHours
```

SAP CATS'te akış kaydet → serbest bırak → onayla → aktar; ham sayaç hiçbir zaman
doğruluk kaydı sayılmaz. Gerekçe tek cümle: **gece açık unutulan sayaç 14
saatlik iş değildir.**

**Z-3 · Timesheet ayrı modüldür.** MOD-0024 yalnız iki şey yapar: kancayı verir
(görev → zaman kayıtları) ve toplamı okur. Görev modülünün içine yarım bir
timesheet yapılırsa sonra baştan yapılır.

⚠ **Şimdi planlanması ucuz, sonra sökmesi pahalı:** timesheet gelince
`SpentHours` **saklanan alan olmaktan çıkıp türetilen sayıya** dönmeli —
onaylanmış zaman kayıtlarının toplamı. Bu bir göç işidir.

**Z-4 · Timesheet'in meşru kullanımları**

| ✓ meşru | ✗ değil |
| --- | --- |
| **Tür bazında sapma** — "bu türe hep 4 saat diyoruz, hep 9 sürüyor" → tahmini düzelt | kişi bazında verimlilik oranı |
| **Kapasite** — "ekipte 160 saat var, 240 saatlik iş atanmış" → aşırı yükleme | kişi sıralaması |
| **Maliyet** — saat × oran (SAP'te CATS → CO) | "hedef tutturma yüzdesi" |

Kapasite ve maliyet **planlama** sayılarıdır, **yargılama** sayıları değil.

**Z-5 · Geçmişe dayalı tahmin önerisi — timesheet'ten SONRA.**

Şekli: tür seçildikten sonra tahmin alanının altında bir cümle —
*"Bu türdeki son 30 iş ortalama 9,2 saat sürdü (medyan 7,0)."*

Üç kural:

1. **Alanı doldurmaz, yalnız yazar.** Doldurursa herkes kabul eder, tahmin bir
   yargı olmaktan çıkar ve sayı kendi kendini doğrular.
2. **Yeterli geçmiş yoksa hiç görünmez** (eşik: ≥5 kapanmış iş). 2 işten öneri
   çıkmaz, gürültü çıkar.
3. **Önce yalnız tür.** *Tür + birim* daha doğrudur ama geçmiş çabuk seyrelir;
   veri birikince eklenir.

⚠ **Sırası budur ve önemlidir:** öneri `SpentHours`'a dayanır, o da bugün elle
giriliyor. Timesheet'ten önce yapılırsa öneri *tahminlerden üretilmiş bir
tahmin* olur — üstelik sistem söylediği için **ölçülmüş gibi görünür.** En kötü
kombinasyon.

---

## 7. Sıra

```
1 · Rapor eksikleri
    okunur etiket (E-7) · filtre [kişi dahil] (E-5) · şirket boyutu (E-6)
    medyan (E-4) · kıyas (E-3) · yaşlandırma (E-2)
    tıklanabilirlik (E-1) · dışa aktarma
    + K-1 ve K-2 kararları
    + Görev Merkezi'ne kişi filtresi (canlı taraf, doğru yer orası)

2 · Timesheet modülü
    sayaç → onay → SpentHours türetilir
    SpentHours saklanan alandan türetilen sayıya göçer

3 · Geçmişe dayalı tahmin önerisi (Z-5)
```

Dilim 1 kendi içinde de sıralıdır: sonrakiler öncekine dayanır.

---

## 8. Açık kararlar

| konu | durum |
| --- | --- |
| K-1 iptaller ortalamada mı | sahip kararı bekliyor |
| K-2 payda tutarsızlığı | düzeltilecek, karar gerekmiyor |
| Kişi kırılımı için ayrı yetki + İsviçre OLT 3 Art. 26 | hukuka sorulacak |
| Oturum bazlı zaman takibi (pack §9) | timesheet moduluyla birlikte |
| `OnHold` / `Deferred` yaşam döngüsü | açık |
| Bekleme süresi dağılımı | açık |
