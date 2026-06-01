---
description: "READ-ONLY-AUDIT — Salt-okunur mimari/governance denetimi (worktree-read-only ve strict repository-read-only modları)"
---

# /read-only-audit

Bu workflow, repoyu **değiştirmeden** mimari/governance denetimi yapmak içindir. Hiçbir dosya oluşturulmaz veya düzenlenmez; hiçbir branch/staging/commit/push yapılmaz.

> **Tetikleme yolları:**
> - Kullanıcı açıkça "salt-okunur", "read-only", "audit — kod yazma" veya benzeri bir denetim talep ettiğinde.
> - `@orchestrator` audit-only bir talebi bu workflow'a yönlendirdiğinde (bkz. orchestrator Talep Sınıflandırma kapısı).
>
> **İlişki:** Git operasyon güvenliği için [../rules/git-safety.md](../rules/git-safety.md) (GIT-002). Salt-okunur denetim `main` üzerinde çalışabilir.

---

## 1. İki Denetim Modu

Bu workflow iki modu açıkça ayırır:

```text
worktree-read-only audit
strict repository-read-only audit
```

Hangi modun geçerli olduğu denetim başında kullanıcı ile netleştirilir; belirtilmezse varsayılan **worktree-read-only**'dir.

## 2. Worktree-read-only audit

**İzin verilen:**

- dosya okuma
- dizin listeleme
- metin arama
- git geçmişi inceleme
- git status inceleme
- diff inceleme
- güvenli salt-okunur analiz komutları
- yalnızca senkronizasyon kanıtı gerektiğinde `git fetch --prune origin`

**Yasak:**

- dosya düzenleme
- dosya oluşturma
- branch oluşturma (`git branch <new>`, `git checkout -b`, `git switch -c`)
- branch değiştirme / switch (`git switch <branch>`, `git checkout <branch>`)
- staging
- commit
- push
- merge
- reset
- clean
- silme
- formatlama
- kod üretimi

> **Not:** `git fetch --prune origin`, çalışma ağacını (working tree) değiştirmese de `.git` altındaki remote-tracking metadata'yı güncelleyebilir. Bu nedenle yalnızca senkronizasyon kanıtı gerektiğinde ve worktree-read-only modunda kullanılır.

## 3. Strict repository-read-only audit

Worktree-read-only kısıtlarına **ek olarak** şunlar da yasaktır:

```text
git fetch
git pull
.git metadata'yı değiştiren herhangi bir komut
```

Bu modda hiçbir `.git` metadata mutasyonu kabul edilmez; senkronizasyon durumu yalnızca mevcut yerel referanslardan okunur.

## 4. Zorunlu Final Doğrulama (Baseline Karşılaştırması)

Salt-okunur denetim, **kasıtlı olarak kirli (dirty)** bir branch üzerinde de çalışabilir (örn. devam eden bir hardening dalı). Bu nedenle final doğrulama, çalışma ağacının boş olmasını DEĞİL, **denetim başındaki baseline ile final durumun aynı kaldığını** kanıtlar.

### 4.1 Preflight baseline (denetim başında yakalanır)

```bash
git branch --show-current
git rev-parse --short HEAD
git status --short
git diff --name-only
git diff --cached --name-only
```

Gerekiyorsa ilgili untracked yollar da not edilir. Bu çıktı **baseline** olarak saklanır.

### 4.2 Denetimi repo durumunu değiştirmeden yürüt

§2 / §3'teki izin ve yasak listelerine uy; hiçbir mutasyon yapma.

### 4.3 Final durumu baseline ile karşılaştır

```bash
git branch --show-current
git rev-parse --short HEAD
git status --short
git diff --name-only
git diff --cached --name-only
git diff --check
```

**Baseline'a göre kanıtlanması gerekenler:**

- branch değişmemiş
- HEAD değişmemiş
- denetim tarafından **yeni veya değişmiş dosya eklenmemiş** (status/diff baseline ile birebir aynı)
- staged durum değişmemiş
- commit yok
- push yok
- stash yok
- branch switch yok
- yıkıcı işlem yok
- `git diff --check` temiz (whitespace hatası / conflict marker yok)

> **Not:** Çalışma ağacının boş olması ZORUNLU DEĞİLDİR. Boş çalışma ağacı yalnızca denetim talebi açıkça bir "clean-tree precondition" istiyorsa beklenir. Aksi halde belirleyici olan, durumun baseline'dan **sapmamasıdır**.

## 5. Çıktı

Denetim raporu + "no-change" doğrulaması birlikte sunulur. Bulgular **düzeltme değil, rapor** olarak verilir; düzeltme ayrı ve onaylı bir implementasyon işidir (bkz. GIT-002 + ilgili workflow).
