# Unapplied pack delta / Phase 1.5 önerisi

Pack değiştirilmedi. Mevcut backend status/§28 authority korunur. Gelecekte exact approved patch ile **ayrı UI scope eki** önerilir; ready-for-dev backend etiketi UI'ye taşınmaz. Bu belge patch değil review edilecek semantic delta'dır; target/preimage seçilince exact patch ayrı hazırlanır.

| Phase1.5 | Öneri |
|---|---|
| Entity/ownership | Backend LoadPlan ve EntityBase değişmez; UI entity/persistence yok |
| DTO | CreateLoadCommand ve LoadListResponse plain wire uyumu; internal fields gönderilmez |
| Repository | UI writer DB/repository yazmaz; MVC→Gateway yalnız GET/POST Loads |
| CQRS/layers | Mevcut backend yeniden yazılmaz; thin MVC proxy, module JS |
| Shell/golden/count | UI scope shell:tenant; golden_reference:slim; form_field_count:7; tekrar satırlarında min10 kontrol açık |
| Views | OWNED-PATHS exact liste; Index ve create-only offcanvas. Details/Edit/transition yok |
| Validation | SCOPE required/null/lexical kuralları; field presence ≠ nonempty. Backend authority korunur |
| Lookup | UUID/string reference entry; yeni endpoint/master yok; enum label'ları L10n |
| Filtre istisnası | Status single-value contract nedeniyle single Select2; genel multiselect standardına bu UI scope için açık istisna önerisi |
| Permission/gateway | read/create; backend transition grant korunur ama UI action yok. Shared tek integration owner |
| Acceptance | U01–U19, SIMULATED/live ayrımı; root/live/restart/Auth unresolved OPEN |

Dispatch kapıları: kullanıcı UI slice/field-count/filtre kararını seçer → module-pack-author exact unapplied delta/preimage üretir → ayrı yetkili pack amendment → CT tek writer ve target atar → integration owner exact shared preimage/diff/handoff → DEV. VER ancak writer-complete ve immutable manifest sonrası. Bu hazırlık hiçbir kapıyı onaylanmış işaretlemez.
