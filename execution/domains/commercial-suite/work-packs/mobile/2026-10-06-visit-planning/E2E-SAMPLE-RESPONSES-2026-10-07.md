# Gerçek yanıt örnekleri — E2E TUTUKON (2026-10-07)

> Yerel ortam, kiracı 97c5, kullanıcı Beste ("Admin User", `read-all` yok). Web proxy'leri üzerinden alındı (CRM yanıt zarfı aynen).
> Mobil sözleşme notu (Faz 2 + 2b) için kaynak.
> Alan adları **taslak değil, canlı**; içerik adımlarının listesi kısaltılmadı.

## `GET /api/crm/resources/me`
```json
{"data":{"items":[{"resourceId":"c5769c62-8f25-4ab7-91ea-12984423e8b1","resourceType":"user","status":"active","displayName":"Admin User"}]},"errors":null,"statusCode":200,"isSuccessful":true}
```

## `GET /api/crm/planned-visits` — liste öğesi (B-8 adlar + SB-3b içerik kalemleri)
```json
{"plannedVisitId":"66e3c393-3356-4ad9-bd0f-6346a7bdb645","visitCode":"VP-a42373cb-0018","targetType":"contact","targetId":"1caea26e-b8e1-4919-8d95-3380b74b2f97","accountId":"9d335f32-16bb-4bcf-b0ac-aba88ab48590","contactId":"1caea26e-b8e1-4919-8d95-3380b74b2f97","accountContactLinkId":"8fed01d8-4a27-4037-ae4f-456920550110","plannedDate":"2026-11-09","plannedStartTime":"09:00","plannedEndTime":"09:06","plannedDurationMinutes":6,"resourceId":"c5769c62-8f25-4ab7-91ea-12984423e8b1","resourceType":"user","resourceDisplayName":"Admin User","visitPurpose":"medical-visit","visitType":"field-visit","businessUnit":null,"territoryNodeId":null,"campaignId":null,"planStatus":"planned","source":"route-plan","consentStatus":"unknown","frequencyStatus":"resolved","version":0,"createdAt":"2026-10-07T08:26:02.3509672+00:00","updatedAt":null,
 "contentItems":[{"productId":"b1ebcf4d-ab91-4a9b-8968-b753b6b35945","productCode":"TUTUKON","role":"promo","journeyId":"97a1b154-0c0e-4480-86f7-fed1912577b5","journeyCode":"CEJ-2026-8AD806","stageId":"9e1c58cc-2577-4329-868a-06306ea9e727","stageIndex":1,"stageCode":"E2E-TUT-S2","stageName":"Pekiştirme","pathId":"29eff507-85fb-4dd8-bbe0-043c67ee4e3d","pathCode":"KP-2026-269A07","pathVersion":"1.0",
   "steps":[{"stepId":"7e163671-6691-49c8-8987-15e1f193570e","contentId":"2b4d5763-9a75-473e-bf79-615d1224ea16","contentCode":"KC-2026-E70D11","title":"E2E-TUT — TUTUKON: Sindirim konforu (detaylama)","type":"core-message","minutes":null},
            {"stepId":"7242a92b-8d07-4ba9-99db-6bf10da01bea","contentId":"2b4d5763-9a75-473e-bf79-615d1224ea16","contentCode":"KC-2026-E70D11","title":"E2E-TUT — TUTUKON: Sindirim konforu (detaylama)","type":"core-message","minutes":null},
            {"stepId":"e32ed1d0-f657-4b3f-bf6a-6f6fdca2a090","contentId":"ad5ed530-9ad0-4c82-9741-bdf1be9dee51","contentCode":"KC-2026-2EC045","title":"E2E-TUT — TUTUKON: Kullanım ve dozaj","type":"core-message","minutes":null},
            {"stepId":"186d4f0f-ec71-4ca2-a7f3-bc5f73d4d06a","contentId":"2b4d5763-9a75-473e-bf79-615d1224ea16","contentCode":"KC-2026-E70D11","title":"E2E-TUT — TUTUKON: Sindirim konforu (detaylama)","type":"core-message","minutes":null},
            {"stepId":"923b07f0-5d05-45ae-9070-93dc328a6d50","contentId":"ad5ed530-9ad0-4c82-9741-bdf1be9dee51","contentCode":"KC-2026-2EC045","title":"E2E-TUT — TUTUKON: Kullanım ve dozaj","type":"core-message","minutes":null}],
   "claims":[],"warnings":[]}],
 "targetDisplayName":"HALİL ÖZARI","accountDisplayName":"ŞİŞLİ HAMİDİYE ETFAL EĞ.ARŞ.HAST","contactDisplayName":"HALİL ÖZARI","targetInactive":false}
```
> Not: `steps` yolun iki dalını birden içeriyor (E7-B1, düzeltilecek). Gelecekte dal seçimi gelince adım sayısı azalır.

## `GET /api/crm/visit-plan/my-accounts?search=memorial&pageSize=2`
```json
{"data":{"territoryStatus":"assigned","territories":[{"territoryNodeId":"92872f2c-b4e3-4431-9f52-92b1daeb45cc","code":"TR-34-BEYOGLU","name":"Beyoğlu","coverageScope":"exact-territory"},{"territoryNodeId":"92a62c4d-7219-43d8-86ac-057e90b4a422","code":"TR-34-FATIH","name":"Fatih","coverageScope":"exact-territory"},{"territoryNodeId":"61466523-2dc7-4548-9860-b9b50648f1a8","code":"TR-34-KAGITHANE","name":"Kağıthane","coverageScope":"exact-territory"},{"territoryNodeId":"4f52a015-5a8f-42f9-a66a-02b5229c1204","code":"TR-34-SISLI","name":"Şişli","coverageScope":"territory-subtree"}],
 "items":[{"accountId":"ddf6069d-b799-4ebd-92f0-70db6e6336ac","accountName":"MEMORİAL ŞİŞLİ HASTANESİ","accountType":"hospital","cityRef":"TR-34-ISTANBUL","districtRef":null,"latitude":41.070873,"longitude":28.990298}],"totalCount":1,"page":1,"pageSize":2},"errors":null,"statusCode":200,"isSuccessful":true}
```
> Faz 2b sonrası her öğeye `activeContactCount` eklenecek; `hasActiveContacts` filtresi gelecek.

## `GET /api/crm/visit-report/calendar?from=2026-10-19&to=2026-10-19` — öğe (rapor gönderildikten sonra)
```json
{"plannedVisitId":"9d71c2d1-59ce-499d-b9b9-8a6536209fcc","visitCode":"VP-a42373cb-0001","plannedDate":"2026-10-19","plannedStartTime":"09:00","plannedEndTime":"09:06","slotSequenceOrder":1,"slotStartTime":"09:00","targetType":"contact","targetId":"1caea26e-b8e1-4919-8d95-3380b74b2f97","resourceId":"c5769c62-8f25-4ab7-91ea-12984423e8b1","planStatus":"planned","plannedJourneyId":"97a1b154-0c0e-4480-86f7-fed1912577b5","plannedStageId":"483015bd-c9a3-4245-b105-819fa65e2781","plannedStageIndex":0,"visitReportId":"c229bb38-8ff0-4f57-913e-1f9976488ba0","reportState":"submitted","executionOutcome":"completed","actualStageIndex":0,"matchedPlan":true,"targetDisplayName":"HALİL ÖZARI","targetInactive":false}
```
> CRM rotaları koddan doğrulandı: `api/crm/resources/me`, `api/crm/planned-visits`, `api/crm/visit-plan/my-accounts`, `api/crm/visit-report/calendar`. Web bunları `/CRM/{PlannedVisits|VisitPlanning|VisitExecution}/api/...` proxy'leriyle okuyor.
