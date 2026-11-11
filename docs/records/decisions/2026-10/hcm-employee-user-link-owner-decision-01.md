---
status: decided
decided: 2026-10-27
decider: repository owner (role)
work_package: HCM-EMP-04
queue: hcm-employee
---

# HCM-EMP-04 — çalışan–kullanıcı bağının sahibi (sahip kararı `bag-kisi`)

> Karar kaydıdır; kod, paket değişikliği ya da başka modüle müdahale izni değildir.

## Soru

Kullanıcı hesabı ile çalışan kaydı arasındaki bağ nerede tutulur? Bugün hiçbir yerde yoktur: `Employee` kaydında `UserId`
yok (`services/Diten.HcmService/src/Diten.HcmService.Domain/Entities/Employee.cs@6704ac9e3`), `PersonReference`'ta
(MOD-0288) `UserId` yok, `PositionAssignment` `UserId → Position` tutar ama kişiye bağlı değildir
(`services/Diten.Platform/src/Diten.Platform.Domain/Entities/Organization/*.cs@6704ac9e3`).

## Karar

Sahip sözü, CT · Çalışan kaydı sohbeti, 2026-10-27: **`bag-kisi`**.

- Zincir: **Kullanıcı (MOD-0360 Tenant User & Identity Management) → Kişi (MOD-0288 Organization, Person & Position
  Directory) → Çalışan (MOD-0251 Core HR / Employee Master, `PersonId`)**.
- MOD-0251 çalışanı kullanıcıdan değil, kişiden bulur; çalışan kaydına `UserId` eklenmez. Paketin "Auth `UserId`
  `PersonId` yerine kullanılamaz" kuralı (GWT-B07) korunur.
- Dayanak: Blueprint 8.8 SoR_Map — MOD-0360 "tenant users; tenant memberships; external identity bindings", MOD-0288
  "Platform person directory records" (`docs/records/audits/2026-10/hcm-employee-discovery-01/RECORD.md`, keşif kaydı;
  Blueprint alıntısı inceleme ZIP'inde `excerpts/blueprint-8.8-hcm-rows.txt`).
- Bu kule (Çalışan kaydı CT) bağın **sözleşmesini** tek yazar olarak yazar (HCM-EMP-06; sahip kararı `dagilim-evet`);
  İzin ve devamsızlık CT okur.

## Bu kararın vermediği

- Kullanıcı → kişi bağının **hangi modülde ve serviste** tutulacağı (MOD-0360 mı, MOD-0288 mi) ve o modüle dokunma izni:
  başka modül (XMC-001), ayrıca sorulur.
- Kod, commit, push, PR.
