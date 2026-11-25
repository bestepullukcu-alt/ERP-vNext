hedef: execution/domains/pharmacovigilance/module-packs/MOD-0230-case-intake-triage.md @ 7f612c2c7d714a487318f0a39313e324f8c1afc1d7414dfa4b3e6e9eddcb2b4c  
model: gpt-6.2-astra  
effort: high  
reviewer_session: 01a1f849-c6bd-7ba3-b788-6ebf7a7551e9  
kaynak_commit: bd3a1c6390d92e1a2e3cc9a8a3052641d2fe63df

F-1 | ENGEL | AC-45, mevcut Mongo güvenlik testinin aynen korunmasını istiyor. PvgPersistenceBoundaryScanTests.cs:214 içindeki olumlu örnek `/opt/fake/mongod` yolunu kullanıyor. Windows bu yolu geçerli sürücüye göre çözdüğü için PvgMongoTestHarness.cs:51 içindeki `Path.IsPathFullyQualified` denetimi yolu reddeder. `--nounixsocket` düzeltmesi ve kurulu bir `mongod` bu testi geçiremez. AC-45 ve DL-12 dosya kapsamında, mevcut mutlak yol denetimini ve 27017 yasağını koruyarak yalnız sentetik test yollarının platforma uygun tam yollarla kurulmasına izin verilmeli. | açık | K5 | 2026-11-23

sonuç: UYGUN DEĞİL

ZIP’in, paketin ve domain-config’in SHA-256 değerlerini doğruladım. Manifestteki 271 girdinin tamamı eşleşti. Windows bulgusu kaynak koduna dayanıyor. Derleme, test ve sabotaj çalıştırılmadı. Blueprint çalışma kitabı ve yedi dilin kaynak dosyaları ZIP’te bulunmadığından içerikleri doğrudan doğrulanamadı.
