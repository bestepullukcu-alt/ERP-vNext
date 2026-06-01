---
description: "GIT-002 — Diten ERP vNext Git Operasyon Güvenliği (branch, dirty-tree, staging, commit ve push kapıları)"
---

# Git Operasyon Güvenliği

Bu kural, repoda yapılan her git operasyonunun güvenlik sınırlarını tanımlar. Amaç; ana dalın (`main`) korunması, kullanıcı emeğinin kaybolmaması ve hiçbir staging/commit/push işleminin kullanıcı onayı olmadan yapılmamasıdır.

> **Otorite:** Bu kural `.antigravity/` katmanındadır (AGENTS.md §1 yetki hiyerarşisine tabidir). Yedekleme biçimi ve branch isimlendirme için tamamlayıcı kural: [git-backup-policy.md](git-backup-policy.md) (GIT-001).
>
> **İlişki:** Salt-okunur denetim modu için: [../workflows/read-only-audit.md](../workflows/read-only-audit.md).

---

## 1. Branch Güvenliği

- **Salt-okunur denetim** (`/read-only-audit`) `main` üzerinde çalışabilir; dosya değiştirmediği sürece branch gerekmez.
- **Herhangi bir dosya değişikliği** için önce bir feature branch açılmalıdır. `main` üzerinde doğrudan dosya düzenlemesi YASAKTIR.
- Feature branch isimlendirmesi AGENTS.md §9 kuralına uyar.
- `main`'e doğrudan push **YASAKTIR**. Değişiklik her zaman feature branch + PR yoluyla gider.

## 2. Çalışma Ağacı (Dirty Tree) Davranışı

- Bir işleme başlamadan önce `git status --short` ile çalışma ağacı kontrol edilir.
- **Beklenmeyen kirli çalışma ağacı** görülürse derhal DURULUR ve durum kullanıcıya raporlanır. Beklenmeyen değişiklikler kullanıcının devam eden işi olabilir.
- Mevcut yerel değişiklikler **korunur**; üzerine yazılmaz, silinmez, izinsiz stash'lenip unutulmaz.

## 3. Staging Disiplini

- `git add -A` **KESİNLİKLE YASAKTIR**.
- `git add .` **KESİNLİKLE YASAKTIR**.
- Yalnızca **açık dosya yolları** stage edilir (`git add path/to/file`).
- Commit öncesi staged değişiklikler **iki kademede** denetlenir:
  - `git diff --cached --name-only` → staged **dosya listesi** doğrulanır.
  - `git diff --cached` → staged **tam içerik** (satır satır) doğrulanır.
- İki denetimden herhangi biri beklenmeyen veya kapsam dışı bir değişiklik gösterirse commit DURDURULUR.

## 4. Commit Kapısı

- Commit **yalnızca açık kullanıcı onayından sonra** atılır.
- Kullanıcı açıkça "commit" istemeden commit oluşturulmaz.
- Hook atlama (`--no-verify`) veya imza atlama açık kullanıcı talebi olmadan kullanılmaz.
- Mevcut commit'i ezmek (`--amend`) varsayılan değildir; varsayılan yeni commit oluşturmaktır.

## 5. Push Kapısı

- Push **yalnızca açık kullanıcı onayından sonra** yapılır.
- `main`'e doğrudan push yapılmaz (bkz. §1).
- Push hedefi (remote + branch) handoff raporunda açıkça belirtilir.

## 6. Yıkıcı Komut Yasakları

Aşağıdaki komutlar **açık kullanıcı onayı olmadan çalıştırılmaz**:

- `git reset --hard`
- `git clean` (ve `-f` / `-d` varyantları)
- force checkout (`git checkout -f`, çalışma ağacındaki dosyayı ezen `git checkout -- <path>`)
- force push (`git push --force` / `--force-with-lease`)
- branch silme (`git branch -D`)

Dosya kaybı riski taşıyan herhangi bir işlemden önce GIT-001'e göre yedek alınır (`.git-backups/` artefact üçlüsü veya backup branch).

## 7. Handoff Raporu

Bir git işlemi tamamlandığında ajan handoff sırasında şunları raporlar:

- aktif branch
- `git status --short` özeti
- diff özeti (`git diff --stat`)
- staged dosyalar (`git diff --cached --name-only`)
- commit hash (varsa)
- push hedefi (varsa)

## ✅ Kontrol Listesi

- [ ] Dosya değişikliği feature branch üzerinde mi (`main` değil)?
- [ ] Çalışma ağacı beklenen durumda mı (beklenmeyen kirlilik yok)?
- [ ] Staging açık dosya yolu ile mi yapıldı (`git add -A` / `git add .` yok)?
- [ ] Commit öncesi `git diff --cached --name-only` (dosya listesi) VE `git diff --cached` (tam içerik) denetlendi mi?
- [ ] Commit/push için açık kullanıcı onayı alındı mı?
- [ ] `main`'e doğrudan push yok mu?
- [ ] Yıkıcı komut öncesi yedek alındı mı?
- [ ] Handoff raporu branch/status/diff/staged/commit/push hedefini içeriyor mu?

> **Mühür:** Bu kural Antigravity'nin "Güvenli Eller" kuralıdır. Ana dal korunur, kullanıcı onayı olmadan tarih değişmez, hiçbir emek kaybolmaz.
