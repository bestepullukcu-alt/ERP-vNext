# Exact decision needs

## DN-01 — A10 evidence-only fault control

Required owner authorization text:

> UI183-A10 bağımsız doğrulaması için, yalnız disposable ve loopback-bound evidence ortamında çalışacak hash-bound test proxy’sini onaylıyorum. Proxy exact create/transition/POD isteklerinde (a) forward etmeden 500 veya 503 döndürebilir ve (b) upstream başarı yanıtını kaydettikten sonra ilk client yanıtını düşürebilir. Her senaryoda payload hash’i, Idempotency-Key, upstream sonucu, client gözlemi ve DB before/after kaydedilecektir. Bu yetki production source, Program.cs, controller/handler/repository, shared probe, contract, guard veya operasyonel endpoint değişikliği vermez. Proxy yalnız lane portlarını kullanacak, reusable credential arşivlemeyecek ve cleanup sırasında kaldırılacaktır.

Without this authorization, only dependency-stop 503 may be planned; it is insufficient to close A10, which stays OPEN.

## DN-02 — bounded DataTable verification profile

Required shared quality-gate owner disposition:

> MOD-0183 Shipment UI için aynı-origin MVC, read/create/detail/transition/POD bounded profile’ını onaylıyorum. Bu profile Edit, delete, bulk delete, QuickView, direct-Gateway, generic Active/Passive ve onaylanmamış import/export/save-view/column-visibility beklentileri dahil değildir. Profile UI183-A14’teki DataTables v2, `window.DtDefaults`, `stateSave:false`, server paging, responsive control/action access, exact bounded localization ve unsupported-action absence hükümlerini fail-closed doğrular. Generic 49/35 sonucu tarihsel ham kanıt olarak korunacak; bounded profile sonucu generic gate waiver veya repo-geneli PASS sayılmayacaktır.

This is a verifier-policy decision. It does not authorize adding edit/delete/bulk/QuickView product behavior.
