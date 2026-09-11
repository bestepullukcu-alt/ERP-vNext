# Görev Merkezi ve bağlı sayfalar — yeniden kullanılabilir ön yüz parçaları envanteri · 2026-09-11

> **Kayıt türü:** salt-okunur envanter (CT alt ajanı, Sonnet 5; `app.js` 9.755 satır, Tasks/Shared/Governance görünümleri ve Meetings S3 kodu okundu).
> **Neden:** sahip talimatı (2026-09-11): "liste, kart gibi şeyleri tekrar tekrar yapmayalım; partial'a alıp diğer sayfalarda da kullanalım."
> **CT doğrulaması:** paylaşımlı bileşen yolları ve Meetings S3'teki kopyalar CT tarafından yol düzeyinde teyit edildi; satır aralıkları okuma anına ait.
> Sonuç: backlog BL-365 (çıkarma planı) ve BL-367 (diyalog standardı).

## 1. Paylaşımlı zaten var
| bileşen | yol | kullanım | kullananlar |
|---|---|---|---|
| `window.showConfirm` + `DitenDialogAppearance` | `Views/Shared/_GlobalConfirmation.cshtml:54-397` | `showConfirm(keyOrTitle, callback, options)`; `showInput` + `inputType text/textarea/number/select`, `inputValidator`, `didOpen` | 16 dosya; WCN `sharedConfirm` (`app.js:7788-7904`); **Meetings kullanmıyor** |
| `window.showToast` | `Views/Shared/_GlobalNotification.cshtml:100-227` | `showToast(key\|msg, type)` | 26 dosya; **Meetings kullanmıyor** |
| `DitenModal` (error/success/warning/info) | `wwwroot/assets/js/shared/premium-modal.js:30-126` | `DitenModal.error({title,message})` | `Tasks/form-page.js`, `WorkCenterNext/quick-create.js`, `Meetings/form.js` — ⚠ `_GlobalConfirmation` yanında **ikinci** görünüm tanımı (BL-367) |
| `_AccessDenied` | `Views/Shared/_AccessDenied.cshtml` | UAS-001 kapısı arkasında partial | Governance, Meetings (4 sayfa) |
| `DitenCheckItem.row` | `wwwroot/assets/js/shared/diten-checkitem.js` | kontrol listesi satırı (authoring / working) | Tasks form, WCN `renderChecklist` — S6 kararlar/aksiyonlar için uygun |
| `DitenDateField.enhance` | `wwwroot/assets/js/shared/diten-datefield.js:26-80` | flatpickr; `enableTime:true` ile tarih-saat | Tasks, WCN; **Meetings kullanmıyor** (ham `datetime-local`) |
| `TaskForm` kişi seçici ailesi | `wwwroot/assets/js/Tasks/form.js:487-813, 1337-1605` | `renderPersonOptions`, `personOptionNode`, `fillGroupedOptions` (avatar + ad + pozisyon + birim, birime göre gruplu) | Tasks, WCN; **Meetings kullanmıyor** (düz `new Option`) |
| Hızlı görev offcanvas | `Views/Tasks/_QuickCreateOffcanvas.cshtml` + `WorkCenterNext/quick-create.js` | partial + `WcnQuickCreate.open()` | fiilen WCN'nin |
| Golden alan deseni (`backbone-preview-*`) | `backbone-custom.css` | etiket/değer çifti | WCN `renderSummary`, Meetings Details (doğru kullanılmış) |
| `DitenTree` | `Views/Shared/_Tree.cshtml` + `shared/diten-tree.js` | hiyerarşi | organizasyon ekranları |

## 2. WorkCenterNext içinde gömülü, çıkarılabilir
| parça | app.js | bağımlılık | boyut | kullanacak dilim |
|---|---|---|---|---|
| Diyalog görünüm adaptörü `bindDialogSelect2`, `dialogLook`, `dialogIcon` | 7727-7787 | `DitenDialogAppearance`, select2, `.wcn-dialog-select` | S | hepsi — seçicili gerekçe diyalogları |
| `sharedConfirm` sarmalayıcı | 7788-7904 | üstteki | S | çağrı sözleşmesi |
| Gerekçe + kişi seçici diyalogu | 8421-8546 | `TasksApi.assignablePeople()` (parametreleşmeli) | M | S5, S6 |
| Sonuç seçimi + koşullu gerekçe diyalogu | 8562-8641 | `closureOutcomesFor` göreve özel | M | S6 karar sonucu + gerekçe |
| `renderRelated` + `renderSourceCard`/`sourceRow` | 4402-4407, 4480-4561 | `relatedRecords`, l10n `RelatedType*`, `.wcn-related-row` | S/M | **S4** doğrudan |
| Üç bölgeli detay kabuğu `detailHtml` + `card()` | 4563-5180 | `.wcn-detail-*`; `card()` HTML koklayarak varyant seçiyor (temizlenmeli) | L | S4–S6 detay |
| Kontrol listesi/alt görev ilerleme kartı | 3179-3268, 3621-3762, `cappedList` 3416-3440 | `DitenCheckItem` (paylaşımlı), `.wcn-progress` | M | S6 katılım/karar listesi |
| Offcanvas hızlı düzenleme (`showPanel`/`hidePanel`/`setPanelBusy`) | 5745-5824 (markup 5197-5249) | bootstrap Offcanvas, `TaskForm.enhance*` | M | S5/S6 satır düzenleme |
| Aylık takvim `renderCalendar` | 5918-5999 | `activeItems()`, l10n `Cal*`, `.wcn-cal-*` | M | **S3b takvim** birebir |
| Boş durum kartı + süzgeç çipleri | 6271-6322, 1250-1406 | `.wcn-empty`, `.wcn-fchip*` | S/M | hepsi |
| `renderTriggerResponses` (davet gelen kutusu kartı) | 6403-6447 | `WorkCenterNextTriggerResponseResolver`, `.wcn-row-meeting` | M | **S5 davet/RSVP** — WCN `meetingInvite` tipini zaten modelliyor |

Not: çoklu seçim "placeholder + sayı + temizle" süsleyicisi (`syncPanelMultiSummary`, 6479-6504) ~50 `index.js`'te ayrı ayrı yazılmış — ürün geneli temizlik, bu kapsamın dışı.

## 3. Toplantı S3'te yeniden yazılmış olanlar
| Meetings parçası | mevcut karşılık | öneri |
|---|---|---|
| Kişi seçici (`Meetings/form.js:60-64, 90-98, 325-332`) | `TaskForm.renderPersonOptions` ailesi (`Tasks/form.js:549-813`) | **önce çıkar** (üçüncü kopya; `app.js:520-522` ikinciyi zaten borç diye yazmış) |
| `buildSearchableDropdownAdapter` (`form.js:34-50`) | `Governance/RoleAssignments/index.js:719-735` | **önce çıkar** → `shared/diten-select2.js` |
| Hata kodu köprüsü (`Meetings/api.js:37-77`) | `Tasks/api.js:42-253` | **kalsın** (kodlar modüle özgü; iskelet bilerek aynalanmış) |
| İptal modalı (`Details.cshtml:125-143`, `form.js:354-376`) | `showConfirm({type:'danger', showInput, inputType:'textarea', inputRequired})` — WCN `withdrawComment` `app.js:7111-7121` | **hemen değiştir** — çıkarma gerekmez |
| Düzenleyen değiştir modalı (`Details.cshtml:102-119`, `form.js:378-398`) | `showConfirm({inputType:'select', didOpen: bindDialogSelect2})` — WCN `openCreateInSource` `app.js:8356-8399` | **önce çıkar** (`bindDialogSelect2`) |
| `renderLinkedTasks` düz liste (`form.js:292-302`) | WCN `renderRelated`/`sourceRow` | **önce çıkar** |
| Tarih-saat: ham `datetime-local` (`_Form.cshtml:67,74`) | `DitenDateField.enhance(root,{enableTime:true})` | **hemen değiştir** |
| Süzgeç çoklu seçimleri | `dt-inline-filter-multi` sözleşmesi | düşük öncelik |

## 4. Çıkarma önceliği
1. Diyalog görünüm adaptörü (`bindDialogSelect2` + `dialogLook`/`dialogIcon`) — en ince, en düşük risk; iki Meetings modalını ve S5/S6'daki her seçicili gerekçe diyalogunu açar.
2. Kişi/avatar seçici (`Tasks/form.js:487-813` + `diten-opt` CSS) — üç kopya tek yere.
3. İlişkili kayıt satırı (`renderRelated`/`renderSourceCard`) — S4'ün ihtiyacı birebir.
4. Aylık takvim (`renderCalendar`) — S3b.
5. Üç bölgeli detay kabuğu (`detailHtml`) — dördüncü el yapımı kopyayı önler.
