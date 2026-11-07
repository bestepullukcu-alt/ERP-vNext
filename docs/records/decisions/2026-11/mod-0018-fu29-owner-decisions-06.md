# MOD-0018-FU29 — sahip kararları 06

Bu bir ek kayıttır; 01…05 numaralı kayıtlar değiştirilmez. Kaynak: CT · Auth (Yetki kulesi). Sözler, sahibin bu sohbette doğrudan yazdıklarıdır. Kişi adı yazılmaz; sahip = MOD-0018 sahibi.

| # | Karar | Söz | Nerede | Zaman |
|---|---|---|---|---|
| 14 | Altıncı turun bulguları düzeltilir; ardından kısa bir son bakış yapılır | "Evet, düzelt ve bak" | CT · Auth sohbeti, doğrudan | 2026-11-06 |
| 15 | Paket `bdcef6f1b` sürümünde (sha256 `df0565f45c4d1f0f…`), yedinci incelemenin KOŞULLU sonucuyla onaya gider (engel yok). Paket bir daha değiştirilmez. İncelemenin 15 bulgusu, aşağıdaki tabloda yazılı kod adımları için bağlayıcı başlama şartıdır. Kod adımı, şartı kapanmadan başlamaz. Adımın kabulü, şartın kapandığını kodla ve testle gösterir | "Evet, şartla onay" | CT · Auth sohbeti, doğrudan | 2026-11-06 |

Bağlayıcı şartlar. Kaynak: `docs/records/reviews/MOD-0018-FU29/2026-11-06-bdcef6f1b.md`.

| Bulgu | Önem | Bağlandığı adım | Şart |
|---|---|---|---|
| F-1 | YÜKSEK | DL-1 başlamadan | Yeni hata kodlarının adları `RoleErrorCodeContractTests` desenine ve ekran haritalarına uyar. Uymuyorsa testin değişikliği CT kararıyla Repo Scope'a girer. AC-10'daki "test yok" iddiası düzeltilir |
| F-2 | YÜKSEK | DL-1 başlamadan | `Persistence/DependencyInjection.cs` DL-1 kapsamına girer. `AddPersistence` parmak izi testinin yeni değeri teslimde ölçülür; değişikliği sahip ve CT onaylar |
| F-3 | YÜKSEK | DL-3 başlamadan | Geçmiş ekranı kullanıcı verisini yalnız kaçışlı (`textContent` / `escapeHtml`) çizer. Sabotajlı bir JS testi bunu sınar: `<img onerror>` metin olarak görünmelidir |
| F-4 | ORTA | DL-1 | Sonuç yazıcısı "yazıldı / zaten var / yazılamadı" durumlarını ayırır. Yazılamadı durumu hata günlüğüne düşer ve bu, gerçek Mongo testiyle sınanır |
| F-5 | ORTA | DL-2 | Geçmiş cevabının alanları sabittir. Metadata cevaba ham konmaz; gizli hesabın kimliği sızmaz |
| F-6 | ORTA | DL-2 başlamadan | Eylemci adının `auth.roles.read` sahibine dönmesinin Viewer etkisi ölçülür ve sahibe sunulur |
| F-7 | ORTA | DL-1 başlamadan | `entity_base` standartla uyumlu yazılır ya da CT istisna kararı alınır |
| F-8 | ORTA | DL-2 | Etiketler DL-2 PR'ında, Platform sahibinin ikinci onayıyla girer. "Önce main'de" şartı uygulanmaz |
| F-9 | DÜŞÜK | DL-1 başlamadan | Outcome = 4'ün Platform kabulü üretim koduyla ölçülür |
| F-10 | DÜŞÜK | DL-1 | Silinmiş rolün Manual hakkını kaldırma dalı ve testi yazılır |
| F-11 | DÜŞÜK | DL-2 | AC-16 aile sabotajı tohumla birlikte genişletilir ya da daraltılır |
| F-12 | DÜŞÜK | — | Çapa ve takvim metinleri; kod adımına etkisi yok, kabul kaydında güncellenir |
| F-13 | DÜŞÜK | DL-1 | Zaman aşımı süresi testte enjekte edilebilir olur |
| F-14 | DÜŞÜK | DL-3 | Ekranda teknik terim kullanılmaz; Step5 değerleri 7 dilde yazılır |
| F-15 | DÜŞÜK | — | ZIP kapsamı; her adımın kabulünde kodla ölçülür |

Bu kayıt commit'lendikten sonra değiştirilmez.
