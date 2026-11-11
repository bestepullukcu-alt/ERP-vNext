# HCM-CORE-BUNDLE — çalışan–kullanıcı bağı ve çalışan okuma sözleşmesi · v0.2 öneri

> **Durum: öneri — onay değil, dondurulmuş değil.** Sahibi MOD-0251 Core HR / Employee Master (Blueprint
> `HCM-CORE-BUNDLE`). Tek yazar: Çalışan kaydı CT (sahip kararı `dagilim-evet`, 2026-10-27). Okuyan: İzin ve devamsızlık
> CT (MOD-0280 Time, Attendance & Leave Management, MOD-0280-FU02 Leave & Absence Management). İş paketi `HCM-EMP-06`.
> Bu metin kod, rota, izin tohumu ya da başka modüle müdahale izni vermez. Hiçbir uç nokta ya da olay bugün yoktur
> (§5 "Bugünkü durum"); taklit (mock) dışında tüketilemez.
>
> **v0.2 (2026-10-27):** v0.1 (yayımlanmadı, commit'siz) üzerine İzin ve devamsızlık CT'nin beş notu işlendi: ters bağ
> (§1), tarihe bağlı okuma `asOf` (§2.1), olay ön koşulu ve `updatedSince` yedeği (§3), servis kimliği (K-4), onay zinciri
> kaynağı (K-8). Görünen ad için kişisel veri sınırı (§2) açık yazıldı.

## 1. Bağ ilkesi (sahip kararı `bag-kisi`)

```text
Kullanıcı (UserId, MOD-0360) ◀──bağ──▶ Kişi (PersonId, MOD-0288) ◀──PersonId── Çalışan (EmployeeId, MOD-0251)
```

- MOD-0251 çalışanı kişiden bulur; `Employee` kaydına `UserId` eklenmez (`docs/records/decisions/2026-10/hcm-employee-user-link-owner-decision-01.md`).
- Kullanıcı ↔ kişi bağı MOD-0251'in dışındadır. Bu sözleşme onu iki yönlü bir **port** olarak tüketir:
  - `ResolvePersonId(tenantId, userId) → PersonId | none` — talep açan kullanıcının kişisi.
  - `ResolveUserId(tenantId, personId) → UserId | none` — ters yön: İK'nın bir çalışan adına hak girmesi ya da talep açması, bildirimin kime gideceği.
  - Bağı hangi modülün tutacağı açık karardır (K-1); iki yön aynı sahipte tutulur.
- Kural U-1: bir kiracıda bir kullanıcı en çok bir kişiye, bir kişi en çok bir kullanıcıya bağlıdır (iki yön de tekil).
- Kural U-2 (R1): bir kiracıda bir kişinin aynı anda en çok **bir aktif** çalışanı vardır. Böylece "bir kullanıcı = en çok bir aktif çalışan" çıkar (İzin isteği madde 2).

## 2. Okuma uçları (önerilen; Gateway üzerinden, `Response<T>` zarfı)

| Uç | İzin | Amaç |
|---|---|---|
| `GET /api/v1/hcm/employees/me/employment-summary?asOf=YYYY-MM-DD` | oturum sahibi (K-3) | Talep açan kullanıcının çalışan özeti. Bağ yoksa ya da `asOf`'ta aktif çalışan yoksa 404 `HCM_EMPLOYEE_NOT_LINKED`. |
| `GET /api/v1/hcm/employees/{employeeId}/employment-summary?asOf=YYYY-MM-DD` | `mod0251.employee.view` | Tek çalışan (İK, yönetici ekranı). |
| `GET /api/v1/hcm/employees/by-person/{personId}/employment-summary?asOf=YYYY-MM-DD` | `mod0251.employee.view` | Ters yön: kişiden (dolayısıyla kullanıcıdan) çalışana. |
| `GET /api/v1/hcm/employment-summaries?page&pageSize&status&legalEntityId&workLocationId&updatedSince` | `mod0251.employee.search` ya da servis kimliği (K-4) | İK toplu liste ve eşitleme; `pageSize` en çok 100; `updatedSince` artımlı okuma için; sıra `updatedAtUtc`, sonra `employeeId`. |

- Kiracı yalnız sunucuda çözülür; istemcinin `TenantId`'si yok sayılır. Başka kiracının kaydı **404** (varlık sızdırılmaz).

### 2.1 Tarihe bağlı okuma (`asOf`)

İzin talepleri gelecek tarihli olabilir; özet bu yüzden bir güne göre okunur. `asOf` verilmezse bugünün tarihi (kiracı saat dilimi; K-9).

- **Aktiflik kuralı:** `status = active` ve `hireDate ≤ asOf` ve (`terminationDate` yok ya da `asOf ≤ terminationDate`). `terminationDate` **son çalışma günüdür ve dahildir**. Bu koşul tutmazsa `/me` 404, diğerleri `status` ile birlikte `activeAsOf = false` döner.
- **Şirket ve iş yeri:** `asOf`'u kapsayan `EmploymentRecord` (`StartDate ≤ asOf` ve (`EndDate` yok ya da `asOf ≤ EndDate`)) okunur. Birden çoksa çakışma veri hatasıdır → 409 `HCM_EMPLOYMENT_OVERLAP`. Hiçbiri yoksa `legalEntityId` null ve `activeAsOf = false`.
- **Geleceğe dönük değişiklik:** gelecekte yürürlüğe girecek bir `EmploymentRecord` ya da ayrılış o tarihten itibaren okunur; olayı yine bugün yayımlanır, `effectiveDate` yürürlük günüdür.
- Sonuç: izin, ayrılış tarihinden sonraya ya da işe girişten önceye talep açtırmamak için `asOf = talebin ilk günü` (ve son günü) ile okur.

### `EmploymentSummary` (yalnız bu alanlar)

| Alan | Tür | Kaynak / not | İzin isteği |
|---|---|---|---|
| `employeeId` | uuid | MOD-0251 | 1 |
| `personId` | uuid | MOD-0251 (`Employee.PersonId`) | 2 |
| `employeeNumber` | string | MOD-0251 | — |
| `status` | `draft` · `pending_approval` · `active` · `terminated` | MOD-0251 | 3 |
| `asOf`, `activeAsOf` | date, bool | §2.1 | 3 |
| `hireDate` | date | MOD-0251 (`Employee.HireDate`) | 4 |
| `seniorityDate` | date \| null | **yeni alan** (K-5); yoksa `hireDate` kullanılır | 4 |
| `terminationDate` | date \| null | MOD-0251; son çalışma günü, dahil | 3 |
| `legalEntityId` | uuid \| null | `asOf`'taki `EmploymentRecord.LegalEntityId` (MDM tüzel kişi) | 5 |
| `workLocationId` | uuid \| null | **yeni alan** (K-6) | 5 |
| `employmentType` | string | MOD-0251 (sözlük K-7) | 6 |
| `ftePercent` | decimal 0–100 \| null | **yeni alan** (K-5); yarı zamanlı hak oranı | 6 |
| `workerType` | string | MOD-0251 | — |
| `organizationUnitId`, `positionId` | uuid \| null | `asOf`'taki `EmploymentRecord`; **onay zinciri için kullanılmaz** (K-8) | — |
| `version` | int | eşzamanlılık ve olay sırası | 7 |
| `updatedAtUtc` | date-time | | 8 |

Hiç dönmeyen alanlar (İzin isteği madde 9 ve paket kuralı): doğum tarihi, devlet kimlik numarası (ham, belirteç ya da
özet), kişisel e-posta, telefon, uyruk. **Görünen ad bu sözleşmede yoktur:** tüketici MOD-0288 kişi okumasından
(`PersonReference.DisplayName`) alır; aynı kişisel veri sınırı orada da geçerlidir ve izin listelerinde yalnız görünen ad
gösterilir.

## 3. Olaylar (önerilen; outbox, en az bir kez teslim)

| Olay | Ne zaman | Yük (zarf + alanlar) |
|---|---|---|
| `hcm.employee.activated.v1` | çalışan `active` olur (onay MOD-0023'ten gelir) | `personId`, `hireDate`, `seniorityDate`, `legalEntityId`, `workLocationId`, `employmentType`, `ftePercent` |
| `hcm.employee.terminated.v1` | ayrılış kaydedilir (gelecek tarihli olabilir) | `terminationDate` |
| `hcm.employee.hire-date-changed.v1` | `hireDate` ya da `seniorityDate` düzeltilir | eski ve yeni değer |
| `hcm.employee.employment-changed.v1` | `legalEntityId`, `workLocationId`, `employmentType` ya da `ftePercent` değişir (şirket değişikliği dahil) | değişen alanların eski ve yeni değeri |

- Zarf: `eventId` (uuid, tekilleştirme anahtarı), `eventType`, `tenantId`, `employeeId`, `version`, `occurredAtUtc`, `effectiveDate`, `correlationId`.
- Tüketici `eventId` ile tekilleştirir; aynı `employeeId` için `version`'ı küçük olanı yok sayar.
- Yükte §2'deki "hiç dönmeyen" alanlar yoktur.
- **Ön koşul:** Diten.HcmService'te bugün outbox yoktur. Olaylar ve outbox **HCM-EMP-05 dilim önerisinin kapsamındadır**; dilim onaylanana kadar olay yoktur. Olaylar gelse bile `updatedSince` eşitlemesi **zorunlu yedektir** (kaçan olay, yeni kurulan tüketici, mutabakat): tüketici olaya ve eşitlemeye birlikte dayanır, yalnız olaya dayanmaz.
- Not: paketin bugünkü adı `employee.created.v1`; bu öneri `activated` adını önerir, çünkü çalışan taslak olarak zaten oluşur. Ad kararı K-2.

## 4. Onay zinciri (yönetici) — bu sözleşmenin dışında

`EmploymentSummary.positionId` MOD-0251'in çalışan tarafı anlık görüntüsüdür (paket: "Job Assignment / Employment
Assignment Snapshot"). Platform'daki `PositionAssignment` (`UserId → PositionId`, MOD-0288) ayrı bir kayıttır. Blueprint
8.8 SoR_Map pozisyon atamalarını ve onay bağlamını MOD-0288'e verir ("directory … for workflow routing, ownership
assignment, delegation, and approval context"). **Öneri:** izin ve puantaj onaylayanı aynı kaynaktan, MOD-0288
`PositionAssignment` üzerinden bulur; MOD-0251 `positionId` onay için okunmaz. İki kaydın eşitlenmesi ve yinelenme kararı
K-8.

## 5. Bugünkü durum (ölçüm, `main` 826150ef4 ile aynı kod)

- Yukarıdaki uçların, alanların ve olayların **hiçbiri yok**. Var olan: `GET /api/v1/hcm/employees` ve `/{id}` (onaylı dilim dışında, `docs/records/audits/2026-10/hcm-emp-02-inspection-01/INSPECTION-NOTE.md` madde 4).
- Aktif çalışan oluşamıyor: onaya gönderme 409 (`services/Diten.HcmService/src/Diten.HcmService.Api/Controllers/Hcm/EmployeeDraftsController.cs:80-90`).
- Kullanıcı ↔ kişi bağı hiçbir modülde yok; HcmService'te outbox yok (İzin CT ölçümü, 2026-10-27).
- Gerçekleşmesi MOD-0251 "aktifleştirme ve izne hazır çalışan" dilimine (`HCM-EMP-05`) ve K-1'e bağlıdır. İzin modülünün Diten.HcmService'e taşınma tetikleyicisi de bunlardır (İzin CT, ADR-005 — o kulenin kaydı).

## 6. Açık kararlar (sahip; onay değildir)

- **K-1** Kullanıcı ↔ kişi bağını (iki yön) hangi modül tutar: MOD-0360 Tenant User & Identity Management mı, MOD-0288 Organization, Person & Position Directory mi; hangi serviste. Başka modül → XMC-001.
- **K-2** Olay adları: `hcm.employee.activated.v1` mi, paketteki `employee.created.v1` mi.
- **K-3** Kendi özetini okuma izni: yeni `mod0251.employee.view_self` anahtarı mı, oturum sahibi kuralı mı.
- **K-4** Servis kimliği: izin modülünün zamanlanmış eşitlemesi kullanıcı belirteciyle koşamaz; **öneri:** ilk dilimde servisler arası kimlik (Auth tarafında; başka modül) ve yalnız `employment-summaries` okuma yetkisi.
- **K-5** `seniorityDate` ve `ftePercent` MOD-0251 `EmploymentRecord`'a eklenir mi (paket revizyonu, HCM-EMP-05).
- **K-6** Çalışma yerinin kaynağı: MOD-0288 organizasyon birimi mi, ayrı bir iş yeri sözlüğü mü. Envanterin `LOCATION` sözleşmesi (Site→Depo→Raf; MOD-0173-FU02) fiziksel depo yeridir, çalışma yeri değildir. Tatil takvimi bağı MOD-0362 Working Calendar & Public Holidays üzerinden.
- **K-7** `employmentType` sözlüğü (tam / yarı zamanlı / geçici …) ve yönetimi (MOD-0048 Reference Data Management mı).
- **K-8** Onay zinciri hangi kayıttan okunur (§4): öneri MOD-0288 `PositionAssignment`; MOD-0251 `EmploymentRecord.PositionId` ile ilişkisi ve yinelenme kararı.
- **K-9** `asOf` varsayılanı ve gün sınırı: kiracının saat dilimi mi, tüzel kişinin mi.
