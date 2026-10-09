# Mobil notu — ziyaret durumu, rapor kuralları, haftalık varsayılan, seçili hafta (2026-10-09)

> **Kimden:** CRM (CT). **Kime:** Android ekibi.
>
> **Kapsam:** test dalında hazır olan ve canlı denenmiş değişiklikler:
> - WP-VW-W1 (4K'nın yerini alır);
> - WP-VP-4L;
> - WP-VP-4M.
>
> 2026-10-08 yanıt notundaki "WP-VP-4K ile gelecek" maddeleri bu notla **teslim edilmiştir**.
>
> **Kural:** yalnız **ek alan, yeni hata kodu ve isteğe bağlı parametre** eklendi; mevcut alan adları değişmedi. Tek istisna §1.5'te: bir hata kodu değişti.

## 1. Ziyaret raporu kuralları (W1)
1. **Rapor son tarihi = ziyaret günü sonu (UTC) + 48 saat.**
   - Örnek: Perşembe ziyareti Cumartesi 23:59:59Z'ye kadar açık. Bu, TR'de Pazar 02:59 demek.
   - Süre geçince sonuç kaydı (yapıldı / yapılamadı / ertelendi) ve ilk gönderim → `409 visit_report_deadline_passed`.
   - Gönderilmiş raporun düzeltmesi (`amend`) sınırsız. 60 dk yeniden gönderme penceresi aynen.
   - `crm.planned-visit.read-all` sahibi (yönetici) muaf.
2. **İptal edilmiş ziyaret:**
   - güncelleme → `409 planned_visit_invalid_transition`;
   - sonuç kaydı ve gönderim → `409 visit_report_plan_cancelled`;
   - iptalden önce gönderilmiş raporun düzeltmesi serbest.
3. **Raporu yazan = giriş yapan kullanıcı.** İstekteki `reportedByResourceId` artık güvenilmiyor: çağırandan farklıysa ve read-all yoksa → `403 resource_not_caller`. Alanı göndermeyin ya da kendi kaynak kimliğinizi gönderin.
4. **Yetkiler:** rapor uçları artık gerçek anahtarları istiyor:
   - okuma → `crm.visit-report.read`;
   - sonuç ve gönderim → `crm.visit-report.record` **+** `crm.planned-visit.manage`;
   - düzeltme → `crm.visit-report.amend`.

   Eski `crm.territory.*` yedeği kalktı.
5. **Değişen hata kodu:** takvimde eksik ya da bozuk tarih aralığı artık `400 visit_report_calendar_range_invalid`. Eskiden `visit_report_reschedule_date_invalid` dönüyordu; takvim ucu artık onu döndürmez.

## 2. Takvim ve ayrıntı — yeni alanlar (W1)
**`GET api/crm/visit-report/calendar` öğesi:**

| Alan | Tür | Not |
|---|---|---|
| `workStatus` | string | aşağıdaki listeden; sunucu hesaplar, mobil yeniden hesaplamaz |
| `reportDeadline` | ISO zaman (UTC) | son geçerli saniye; geri sayım ve kilit için |
| `managerAttention` | bool | `workStatus = expired` ise true (yöneticiye bildirim W5'te gelecek) |
| `cancellationReason` | string? | `week_reopened` ya da elle iptal nedeni |
| `plannedContent[].productName` | string? | ürün adı; önce onayda dondurulan ad, yoksa MDM; MDM yoksa null (yanıt yine 200) |

- **Planlanan ziyaret ayrıntısı:** `workStatus`, `reportDeadline`, `managerAttention`.
- **İşyerindeki doktor hedefi** (`account-contact-link`): `targetDisplayName` artık doktorun adı (T-1 düzeltildi).
- **Takvim süzgeci:** `?workStatus=missed,expired` (virgüllü, isteğe bağlı). Bilinmeyen değer → `400 visit_report_work_status_invalid`.
- **`GET api/crm/visit-report/contract`:** `reportDeadlineHours = 48`, `workStatuses[]` (öncelik sırasıyla). `supportedFilters` listesinde `workStatus` var.

**`workStatus` değerleri (öncelik sırası, ilk tutan kazanır):**

| Kod | Anlamı | Temsilci ne yapabilir |
|---|---|---|
| `cancelled` | iptal | hiçbir şey |
| `not_done` | gönderilmiş rapor, sonuç yapılamadı | düzelt |
| `rescheduled` | gönderilmiş rapor, sonuç ertelendi | düzelt |
| `reported` | gönderilmiş rapor, sonuç yapıldı | 60 dk içinde yeniden gönder, sonra düzelt |
| `expired` | süre doldu, gönderilmiş rapor yok | kilitli |
| `report_missing` | sonuç "yapıldı" taslağı var, gönderilmedi | gönder |
| `missed` | gün geçti, sonuç yok, süre dolmadı | yapılamadı / ertele (48 sa içinde) |
| `today` | bugün (UTC) | sonuç gir |
| `planned` | gelecekte | (erken giriş `visit_not_yet_due`) |

**İleride gelecek:**
- `in_progress` ("devam ediyor") W3'te eklenecek; `today`'den önce yer alacak. Bilinmeyen bir kod gelirse uygulama çökmemeli, genel bir görünüm göstermeli.
- "Ertele" yakında **yeni bir planlı ziyaret** oluşturacak (W2, ürün sahibi kararı K-W1). Bugün yalnız rapor sonucu.

## 3. Haftalık varsayılan ve ek ziyaret (4L)
- **M10 güncellemesi:** sıklığı tanımsız doktor artık **haftada 1 (varsayılan)** sayılır. Dönemde gereken ziyaret = dönemin hafta sayısı.
  - 3D doktor durumu alanı `frequencyDefault: "weekly"`; tanımlı sıklıkta null.
- **Haftalık ek ziyaret:**
  - Planlama oturumunun mevcut güncelleme (PUT) isteğine `weekExtras: [{ weekStart, targets[] }]` eklendi. Yalnız taslak haftada ve plandaki hedefler için geçerli.
  - Hatalar:
    - `400 extra_target_not_in_plan`;
    - onaylı ya da geçmiş hafta → `409`.
  - Önizleme uyarıları: `extra_no_room`, `week_full_skipped`, `extra_already_planned`.
  - Önizleme ziyaretinde `isExtra`. Oturumda `extraTargets`.
  - 3D durumunda `extraThisWeek` ve `overFrequency` (gereğin üstündeki ziyaret sayısı; kalan 0'ın altına inmez).

## 4. Seçili hafta (4M)
- `GET .../my-accounts/{id}/doctors` ve `GET .../sessions/{id}/targets` uçlarına isteğe bağlı `?weekStart=yyyy-MM-dd` (dönemin bir Pazartesisi) eklendi. Geçersizse → `400 invalid_week`.
  - Verilirse `dueThisWeek` o haftaya göre hesaplanır.
  - Verilmezse bugünün haftası (eski davranış).
- Doktor durumunda `plannedThisWeek: bool?`:
  - seçili haftada yazılmış (planlı / onaylı) ziyaret var mı;
  - parametresiz istekte null;
  - taslak haftanın ziyaretleri sayılmaz (onlar önizlemede).
- `my-accounts/{id}/doctors` yanıtında `quickCounts { due, never, all }`: hızlı süzgeç sayıları, istenen süzgeçten bağımsız.

## 5. Saha temsilcisi rolü — önerilen yetkiler
Rolü ürün sahibi açar ve yetkileri ekrandan verir. Liste, uçlardaki gerçek yetki kontrolleri taranarak doğrulandı:
- `crm.planned-visit.read` / `.manage` / `.confirm`;
- `crm.visit-plan.read` / `.generate` / `.apply`;
- `crm.visit-report.read` / `.record` / `.amend`;
- `crm.account.read`, `crm.contact.read`, `crm.account-contact.read`, `crm.account-relationship.read`;
- `mdm.global-products.read` (ürün adları);
- `crm.territory.read`: geçici. Dönem listesi ve yolculuk aşamaları hâlâ bu eski yedeği istiyor; düzeltilince kaldırılacak.
