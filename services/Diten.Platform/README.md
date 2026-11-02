# Diten.Platform — Platform paylaşılan servisler

Platform tarafının ortak servis çatısı. Bu README yalnız İş akışı (MOD-0023) bölümünün **§17 davranışını** anlatır; öteki modüller için kendi paketlerine bakın.

| | |
|---|---|
| Port | **5057** (`AGENTS.md` §3 tablosu) |
| API projesi | `services/Diten.Platform/src/Diten.Platform.API` |
| İş akışı taban yolu | `/api/v1/workflow` (`WorkflowDefinitionsController`) |
| Modül paketi | `execution/domains/platform-shared-services/module-packs/MOD-0023-workflow-config-approval-templates.md` (§17) |
| API anlatısı | `docs/reference/architecture/api/mod-0023-workflow-idempotency.md` |
| Geçiş kapısı standardı | `services/Diten.Platform/docs/workflow-transition-gate-standard.md` |
| Kurtarma işletim notu | `docs/guides/operations/workflow-engine-recovery-runbook.md` |

## İş akışı (MOD-0023) — §17: kayıtlar silinmez, aynı anahtar aynı yanıtı verir

TRM-15 (Hazine isteği) iki dilimle karşılandı: DL-1 (kayıtları silmeme, ek okuma alanları) ve DL-2 (aynı anahtar yanıtı). Aşağıdaki kurallar koddan okunmuştur; her satırın dosya:satır kanıtı API anlatısındadır.

### Kayıtlar silinmez (DL-1)

- `WorkflowInstance`, `ApprovalTask` ve `WorkflowTransitionLog` kayıtları silinmez. Üç depo, devralınan `DeleteAsync`'i geçersiz kılar ve **yazmadan reddeder** (`NotSupportedException`); `Diten.Platform.Common` değişmedi, hiçbir arayüze silme üyesi eklenmedi.
- Bilinen istisna: dördüncü türemiş depo `WorkflowStartRecoveryStore` devralınan `DeleteAsync`'i hâlâ taşır (sahip kararı S17-8; üretimde çağıran yok). Mimari bekçi `WorkflowRetentionSourceGuardTests` bu yolu metin okuyarak kapalı tutar; kapatma paketin bir sonraki revizyonuna kalır.
- `GET /api/v1/workflow/instances/by-objects` varsayılanda silinmiş satırı **gizler**; `includeDeleted=true` ile aynı kiracının silinmiş satırlarını `isDeleted=true` işaretiyle döndürür. İzin değişmedi: `platform.workflow.instances.view`.
- `GET .../instances/{id}/history` `includeDeleted` almaz; silinmiş örnekte **404** verir (sahip kararı O-S17-2 = b).

### Ek okuma alanları (hepsi sonda ve isteğe bağlı)

| Uç | Yeni alanlar |
|---|---|
| `GET .../instances/by-objects` | `idempotencyKey`, `isDeleted` (örnek başına) |
| `GET .../instances/{id}/history` | `approvalTaskId`, `idempotencyKey`, `isDeleted` (satır başına; `isDeleted` burada her zaman `false`) |

### Aynı anahtar (DL-2)

**Başlatma** (`POST .../instances`, gövde alanı `idempotencyKey`):

- Aynı anahtar, aynı olgular, aynı başlatan → **200** ve yanıtta `idempotentReplay: true`. Anahtarın ilk yazımı `false` döner.
- Aynı anahtar, farklı olgu (şablon, nesne, aday listesi, son tarih, neden kodu, iki zorunluluk bayrağı) ya da **başka başlatan** → **409 `WORKFLOW_INSTANCE_START_CONFLICT`**. Bu karşılaştırma artık bütün başlatma tekrarlarında yapılır (eskiden yalnız yarım başlatmada yapılıyordu).

**Geçişler** (approve, reject, request-info, cancel; gövde alanı `idempotencyKey`): sıra sabittir.

1. Kayıtlı satırın aktörü isteği yapandan farklıysa → **403 `WORKFLOW_ACTOR_DENIED`**.
2. Aynı aktör, farklı gövde (neden kodu, açıklama, kanıt, e-imza kimliği, request-info hedefi) → **409 `WORKFLOW_TRANSITION_IDEMPOTENCY_CONFLICT`**; yeni satır yazılmaz.
3. Aynı aktör, aynı gövde → **200**, `isIdempotent: true`, ilk geçişin yanıtı.

Başka kiracı aynı görev kimliğini ve anahtarı gönderirse görev bulunamaz: **404 `NOT_FOUND_NON_LEAKAGE`** (403 ya da 409 değil).

Delegate değişmedi: tekrarında yalnız aktör karşılaştırılır, gövde karşılaştırılmaz.

Eşzamanlı çakışmada kaybeden hâlâ **409 `WORKFLOW_TRANSITION_CONFLICT`** ya da **503 `WORKFLOW_TRANSACTION_UNAVAILABLE`** alabilir. Bunlar iş reddi değildir; tüketici niyeti "bilinmiyor" sayar, aynı anahtarla yeniden gönderir ya da okur.

### Yeni hata kodları

| Kod | Durum | Anlam |
|---|---|---|
| `WORKFLOW_TRANSITION_IDEMPOTENCY_CONFLICT` | 409 | Anahtar aynı aktörle başka gövdeye bağlı. Yeniden denenmez. |
| `WORKFLOW_INSTANCE_START_CONFLICT` | 409 | Yeni değil; artık bütün başlatma tekrarlarında farklı olgu ya da başka başlatan için de döner. |

### Bu README'nin kapsamadığı

Kullanıcıya görünen ekran değişmedi. Ölçülmeyen alanlar (tüketici gövde kanıtı, canlı çağrı, Gateway geçişi) API anlatısının sonunda sayılmıştır.
