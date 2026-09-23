# WORK PACKAGE — WP-KNOWLEDGE-L10N · Knowledge Taxonomy terim lokalizasyonu (Subject/Topic/AudienceProfile, 7 dil) (frontend, resx-only)

> **CT (SoR).** MOD-0162-FU02 Knowledge/Taxonomy Admin UI. Branch `feature/crm-scmm-studio`. **Kullanıcı E4:** Türkçe kullanıcıda "**Subject Oluştur**" header'ı — "Subject" çevrilmemiş; tr resx taksonomi terimlerini İngilizce bırakmış, es/fr/ru/zh/ar tamamen İngilizce placeholder. CRM = tenant modülü → **7 dil zorunlu** (`AGENTS.md`). **Yalnız resx değerleri** (frontend). Kod/JS/köprü/backend DEĞİŞMEZ.

## Kanıt
- **Dosya:** `frontend/Diten.Web/Resources/Views/CRM/Knowledge/KnowledgeIndex.{en,tr,fr,es,zh,ar,ru}.resx` — her biri **145 anahtar**.
- **tr yarım çeviri** (İngilizce kalmış taksonomi terimleri): `SubjectId`=Subject · `TopicId`=Topic · `AudienceProfileId`=Audience Profile · `SubjectsTab`=Subjects · `TopicsTab`=Topics · `ProfilesTab`=Audience Profiles · `CreateSubject`=**Subject Oluştur** · `CreateTopic`=Topic Oluştur · `CreateProfile` · `EditSubject`=Subject Düzenle · `EditTopic` · `EditProfile` · `ArchiveSubject`=Subject Arşivle · `ArchiveTopic` · `ArchiveProfile` · `SubjectSource`=Subject kaynağı · `GlobalProductSubjectHint`="Subject'i MDM…" · (ilgili) `ParentSubjectId`/`ParentTopicId`/`ProfileType`.
- **es/fr/ru/zh/ar:** aynı anahtarlar tamamen İngilizce ("Create Subject" vb.).
- **JS L-köprüsü** (`frontend/Diten.Web/Views/CRM/Knowledge/_IndexL10n.cshtml`): bu anahtarları **zaten whitelist'lemiş** (SubjectId/TopicId/CreateSubject/CreateTopic/CreateProfile/EditSubject…/ArchiveSubject…/SubjectsTab/TopicsTab/ProfilesTab). → köprü **DEĞİŞMEZ**; yalnız resx değerleri düzelince JS de düzelir.
- **Kullanılan yer:** `Taxonomy.cshtml:223` `@Localizer["CreateSubject"]` + taxonomy.js `L.CreateSubject` vb.

## Terim sözlüğü (onaylı — kullanıcı)
| EN | TR | FR | ES | ZH | AR | RU |
|---|---|---|---|---|---|---|
| Subject | **Konu** | Sujet | Tema | 主题 | الموضوع | Тема |
| Topic | **Başlık** | Thème | Tópico | 话题 | العنوان | Раздел |
| Audience Profile | **Hedef Kitle Profili** | Profil d'audience | Perfil de audiencia | 受众画像 | ملف الجمهور | Профиль аудитории |

> Subject>Topic hiyerarşisi korunur (Konu üst, Başlık alt). Fiiller: Oluştur/Create/Créer/Crear/创建/إنشاء/Создать · Düzenle/Edit · Arşivle/Archive. Ajan her dilde doğal, tutarlı birleşim üretir (ör. tr `CreateSubject`="Konu Oluştur", `SubjectsTab`="Konular", `AudienceProfileId`="Hedef Kitle Profili").

## NE (yalnız resx değerleri; 7 dil)
1. **7 dilde** yukarıdaki taksonomi terminoloji anahtarlarını (Subject/Topic/AudienceProfile ailesi: `*Id`, `*sTab`/`*Tab`/`ProfilesTab`, `Create*`, `Edit*`, `Archive*`, `Parent*Id`, `ProfileType`, `SubjectSource`, `GlobalProductSubjectHint`) o dilin doğru terimiyle güncelle. **en** = kanonik İngilizce (zaten doğru — dokunma gerekmez, yalnız gerekiyorsa tutarlılık).
2. **tr**: yarım çevirileri tamamla (Subject→Konu vb.); İngilizce hiçbir taksonomi terimi kalmasın.
3. **es/fr/ru/zh/ar**: İngilizce placeholder taksonomi terimlerini gerçek çeviriyle değiştir.
4. Anahtar adları, sayısı (145), sıra, `xml:space` **değişmez** — yalnız `<value>` içerikleri. Değer, anahtar adını echo'lamaz (guard).

## KORU / YAPMA
- **Yalnız `KnowledgeIndex.*.resx` `<value>` içerikleri.** `_IndexL10n.cshtml` köprüsü (whitelist), `Taxonomy.cshtml`, `taxonomy.js`, backend, diğer resx dosyaları DEĞİŞMEZ. Anahtar ekleme/silme/yeniden adlandırma YOK (145 sabit). Taksonomi-dışı jenerik anahtarları (Save/Loading/ErrorState vb.) bu WP'de ÇEVİRME (ayrı, daha büyük boşluk — aşağı bak). MDM/Subject provenance davranışı DEĞİŞMEZ (yalnız etiket).
- **DUR:** bir dilde uygun/doğal terim belirsizse (ör. domain ayrımı kaybolacaksa) → o dili en-fallback ile bırakıp raporla (yanlış çeviri koyma).

## Not — daha geniş boşluk (bu WP'de YOK)
`KnowledgeIndex.{es,fr,ru,zh,ar}.resx`'in **145 anahtarının tamamı İngilizce placeholder** (yalnız taksonomi terimleri değil). Bu WP kullanıcının işaretlediği **taksonomi terminolojisiyle** sınırlı; tüm dosyanın tam lokalizasyonu ayrı iş = **F-KNOWLEDGE-L10N-FULL** (deferred).

## Acceptance
- **E2:** `dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo` → **201/0** (L10n echo/completeness guard'ları yeşil). git diff: yalnız `Resources/Views/CRM/Knowledge/KnowledgeIndex.*.resx` (`<value>` satırları). Kod/JS/köprü/diğer resx diff YOK. Her dilde taksonomi terimleri için İngilizce kalıntı yok (en hariç).
- **E4 (FLEET RESTART — resx değişikliği tam yeniden başlatma ister):** tr'de Taxonomy → **"Konu Oluştur"** (Subject değil); sekmeler "Konular / Başlıklar / Hedef Kitle Profilleri"; dil değiştir → her dilde doğru terim. **resx → FLEET RESTART.**

---

## §36.1 Agent Prompt (paste-ready) — DISPATCH: owner
```text
@[.antigravity/agents/frontend-specialist.md]
WP: WP-KNOWLEDGE-L10N · Knowledge Taxonomy terim lokalizasyonu (Subject/Topic/AudienceProfile, 7 dil, resx-only) (MOD-0162-FU02, frontend)
Repository: C:\Users\user\Desktop\ERP-vNext (ANA checkout)
Branch: feature/crm-scmm-studio · Worktree: ana checkout

Kullanıcı E4: Türkçe'de "Subject Oluştur" header'ı — "Subject" çevrilmemiş; tr resx taksonomi terimlerini İngilizce bırakmış, es/fr/ru/zh/ar tamamen İngilizce. CRM tenant modülü → 7 dil zorunlu. YALNIZ resx <value> içerikleri; kod/JS/köprü/backend DEĞİŞMEZ.

Önce oku: execution/domains/commercial-suite/work-packs/WP-KNOWLEDGE-L10N-taxonomy-terms.md · frontend/Diten.Web/Resources/Views/CRM/Knowledge/KnowledgeIndex.{en,tr,fr,es,zh,ar,ru}.resx (145 anahtar/dosya) · frontend/Diten.Web/Views/CRM/Knowledge/_IndexL10n.cshtml (köprü whitelist — DOKUNMA) · memory local-fleet-and-resx-rebuild + l10n-bridge-pascalcase-loader.

Terim sözlüğü (onaylı): Subject→ TR Konu / FR Sujet / ES Tema / ZH 主题 / AR الموضوع / RU Тема · Topic→ TR Başlık / FR Thème / ES Tópico / ZH 话题 / AR العنوان / RU Раздел · AudienceProfile→ TR "Hedef Kitle Profili" / FR "Profil d'audience" / ES "Perfil de audiencia" / ZH 受众画像 / AR "ملف الجمهور" / RU "Профиль аудитории". Subject>Topic hiyerarşisi korunur. Fiiller: Oluştur/Create/Créer/Crear/创建/إنشاء/Создать; Düzenle/Edit/Modifier/Editar/编辑/تعديل/Изменить; Arşivle/Archive/Archiver/Archivar/归档/أرشفة/Архивировать.

NE (yalnız resx <value>; 7 dil):
 1) Taksonomi terminoloji anahtarları: SubjectId/TopicId/AudienceProfileId, SubjectsTab/TopicsTab/ProfilesTab, CreateSubject/CreateTopic/CreateProfile, EditSubject/EditTopic/EditProfile, ArchiveSubject/ArchiveTopic/ArchiveProfile, ParentSubjectId/ParentTopicId, ProfileType, SubjectSource, GlobalProductSubjectHint — her dilde doğru terimle güncelle (ör. tr CreateSubject="Konu Oluştur", SubjectsTab="Konular", AudienceProfileId="Hedef Kitle Profili").
 2) tr: yarım çevirileri tamamla; İngilizce taksonomi terimi kalmasın.
 3) es/fr/ru/zh/ar: İngilizce placeholder taksonomi terimlerini gerçek çeviriyle değiştir. en=kanonik, dokunma.
KORU/YAPMA: yalnız KnowledgeIndex.*.resx <value>; _IndexL10n.cshtml/Taxonomy.cshtml/taxonomy.js/backend/diğer resx DEĞİŞMEZ; anahtar ekleme/silme/yeniden-adlandırma YOK (145 sabit); sıra/xml:space değişmez; değer anahtar adını echo'lamaz; taksonomi-DIŞI jenerik anahtarları (Save/Loading/ErrorState…) ÇEVİRME (ayrı iş F-KNOWLEDGE-L10N-FULL).
DOĞRULA (E2): cd C:\Users\user\Desktop\ERP-vNext; dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -c Release --nologo → 201/0 (L10n guard'ları yeşil); git diff yalnız KnowledgeIndex.*.resx; kod/JS/köprü/diğer resx diff yok; taksonomi terimlerinde en-dışı İngilizce kalıntı yok. Ayrı commit ("i18n(knowledge): WP-KNOWLEDGE-L10N — taxonomy Subject/Topic/AudienceProfile terms in 7 languages (MOD-0162-FU02)" + son satır Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>). §22 TÜRKÇE. K13.
Durma: bir dilde domain-doğru terim belirsizse (Subject>Topic ayrımı kaybolacaksa) → o dili en-fallback bırak + raporla.
```

## §37 CT bağımsız doğrulama (2026-09-23) → **ACCEPTED (E2)**
```
Commit: 783a7613 · CT: ACCEPTED E2 · izole worktree @6db7d751 (L10N+SORTORDER dahil) → Diten.Web.Tests 201/0
```
- ✅ **Kapsam (6 resx, 113/113 değer-swap):** yalnız `KnowledgeIndex.{ar,es,fr,ru,tr,zh}.resx` (en zaten kanonik "Create Subject" — dokunulmadı). **JS/köprü/Taxonomy.cshtml/backend/diğer resx diff = 0.** Anahtar eklenmedi/silinmedi (yalnız `<value>`).
- ✅ **tr terimleri:** SubjectId=Konu · TopicId=Başlık · AudienceProfileId=Hedef Kitle Profili · SubjectsTab=Konular · TopicsTab=Başlıklar · ProfilesTab=Hedef Kitle Profilleri · **CreateSubject=Konu Oluştur** (kullanıcının E4 şikayeti çözüldü) · EditSubject=Konu Düzenle · ArchiveSubject=Konu Arşivle. Subject>Topic hiyerarşisi korundu.
- ✅ **6 dil çevrildi:** fr=Créer un sujet · es=Crear tema · zh=创建主题 · ar=إنشاء موضوع · ru=Создать тему.
- ✅ **E2:** Diten.Web.Tests **201/0** (L10n echo/completeness guard'ları yeşil).
- ⚠ **Küçük kalıntı (F-KNOWLEDGE-L10N-PROSE, deferred):** 4 düz-metin string hâlâ küçük-harf İngilizce terim içeriyor — `PageDescription` ("…subject / topic / audience taksonomisini…") + `GlobalProductPickerUnavailable/EndpointMissing/PermissionMissing` ("…özel bir subject girin"). Terminoloji anahtarları (header/tab/label/buton) tam düzeldi; bunlar WP kapsamındaki anahtar listesinde değildi (cümle-içi geçiş). Tutarlılık için ayrı küçük follow.

**WP-KNOWLEDGE-L10N KOMPLE (E2).** E4 = fleet restart (resx). Prose kalıntısı = F-KNOWLEDGE-L10N-PROSE. Not: es/fr/ru/zh/ar'ın kalan ~130 jenerik anahtarı hâlâ İngilizce = F-KNOWLEDGE-L10N-FULL (deferred).

