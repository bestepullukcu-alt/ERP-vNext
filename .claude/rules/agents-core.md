> **Üretilmiş kopya — elle düzenlemeyin.** Kaynak: `AGENTS.md` · kaynak özeti: `sha256:c9fc70b86047` · yeniden üret: `python3 .antigravity/scripts/sync_adapters.py`

## Çekirdek — her işte geçerli davranış kuralları

> Kısa ama **bağlayıcıdır** — hangi araçta kendiliğinden yüklendiğinden bağımsız olarak her işte geçerlidir. Ayrıntı gerektiğinde belirtilen bölümü (§N) ya da kural dosyasını **aç** — özetle yetinme.

1. **Yetki sırası (§1):** Module Pack > Domain Config > AGENTS.md > `.antigravity/` > Archive. Çakışmada üstteki kazanır; bu sırayı bozma.
2. **Kiracı izolasyonu (§6, `multi-tenancy`):** Kiracı verisine dokunan her sorgu ve endpoint `TenantId` ile sınırlanır; kiracılar arası erişim 404 döner. Üretim kodunda sabit ya da varsayılan kiracı kullanma. Silme yalnız soft delete (`IsDeleted`, `DeletedAt`).
3. **Yetki (§6, `security-jwt`, `permission-key-standard`):** JWT + RBAC; her eylem `[HasPermission]` ister. Yetkisiz kullanıcıya sayfa iskeleti çizme, yönlendirme yapma (UAS-001).
4. **Dil (§6, `localization-standard`):** Kullanıcıya görünen metin hardcoded yazılmaz. Platform modülü 2 dil (en, tr), kiracı modülü 7 dil (en, tr, fr, es, zh, ar, ru); bir dil eksikse iş bitmemiştir.
5. **Portlar (§3):** Portların tek kaynağı §3 tablosudur; port uydurma. Web (5001) servislere doğrudan değil, Gateway (5000) üzerinden gider.
6. **Korunan yollar (§4):** `.antigravity/**` değişikliği kullanıcı onayı ister. `ocelot.json`'u yalnız `integration-agent` değiştirir. Archive ve `_Layout.cshtml` donuktur. Başka domain'in servisine dokunma.
7. **Modül ve kimlik (§7, §10, DCP-002):** Kod yalnız `approved` / `ready-for-dev` module pack ile yazılır; `@orchestrator` pack oluşturmaz. MOD kimliği uydurulmaz; pack açmadan önce `verify_module_id.py` ön kontrolü (fail-closed) koşulur.
8. **Git (§9, GIT-002 `git-safety`):** `main`'e commit/push yok. Yalnız açık dosya yoluyla stage (`git add -A` / `git add .` yok). Commit, push, PR ve merge yalnız kullanıcının açık onayıyla; yıkıcı git komutları onaysız çalışmaz.
9. **Başka modül (XMC-001):** İşin kapsamı dışındaki bir modülde düzeltme gerekirse dokunmadan önce sor. **Kayıt kodu (RCS-001):** yeni kayıt türünün görünen kodu `MG-ORG-000042` biçiminde ortak servisten gelir.
10. **Test ve belge:** Kural, kendi kopyasıyla değil üretim koduyla ölçülür; sabotaj kanıtı olmayan guard, guard değildir. Belge yeri `docs-organization` beş sorusuyla belirlenir; `docs/` köküne dosya konmaz.
11. **Kural haritası (§6.1):** İşe başlamadan §6.1'de işin satırını bul ve o kural dosyalarını aç. Bu blokta olmayan her ayrıntı için ilgili bölüm (§N) ya da `.antigravity/AGENTS-REFERENCE.md` açılır.
