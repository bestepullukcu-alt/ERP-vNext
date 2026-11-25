**WP-0230-DL12-L10N v1.1 tamamlandı.** `DraftNotEditable` anahtarı yedi sayfa RESX dosyasına eklendi. Tek yerel commit `43f856f22f0549be1d61fab09d40086b755661ba`. Çalışma ağacı temiz.

| Dil | Anahtar sayısı | Eklenen metin |
|---|---:|---|
| en | 111 → 112 | This record can no longer be edited. |
| tr | 111 → 112 | Bu kayıt artık düzenlenemez. |
| fr | 111 → 112 | Cet enregistrement ne peut plus être modifié. |
| es | 111 → 112 | Este registro ya no se puede editar. |
| zh | 111 → 112 | 此记录已无法编辑。 |
| ar | 111 → 112 | لم يعد من الممكن تعديل هذا السجل. |
| ru | 111 → 112 | Эту запись больше нельзя редактировать. |

Yedi XML dosyası ayrıştırıldı ve anahtar kümeleri eşit bulundu. Boş değer, yinelenen anahtar ve yeni metinlerde yer tutucu sayısı **0**. İlk XML karşılaştırması kardeş öğeler arasındaki girintiyi de hesaba katmıştı. Düzeltilen kontrolde eski 111 öğenin metni, öznitelikleri ve sırası eşleşti. Eklenen satır çıkarıldığında her dosyanın önceki ham SHA-256 özeti birebir elde edildi. Commit farkı **7 dosya, 7 eklenen satır, 0 silinen satır**. Tam staged diff incelendi ve `git diff --check` çıkışı `0` oldu. [Önceki ham/LF özetleri](C:/Users/epenc/AppData/Local/Temp/pvg-dl12-l10n-evidence/WP-0230-DL12-L10N-1795207314259/before.json) · [Son ham/LF özetleri ve XML ölçümleri](C:/Users/epenc/AppData/Local/Temp/pvg-dl12-l10n-evidence/WP-0230-DL12-L10N-1795207314259/after.json)

| Git kaydı | Başlangıç | Son |
|---|---|---|
| Commit | `ab039c2c53d82edf8413bae4e3304d21b57e04e4` | `43f856f22f0549be1d61fab09d40086b755661ba` |
| Tree | `47c42a86d940992fca0f063f18c3667a3b23342f` | `72e59b13b99d4fa5ba7ccacb2544145128f4d7f2` |

Dal `feature/pvg/mod-0230-dl12-l10n`. İstenen model/efor `gpt-6.2-astra / high`, gerçek oturum değerleri verilemiyor. Yerel teslim CT’nin bağımsız incelemesini bekliyor. [Worktree](C:/Users/epenc/.codex/worktrees/pvg-dl12-prep-web/ERP-vNext-git) · [Çalıştırılan komutlar](C:/Users/epenc/AppData/Local/Temp/pvg-dl12-l10n-evidence/WP-0230-DL12-L10N-1795207314259/commands.json) · [Kanıt manifesti](C:/Users/epenc/AppData/Local/Temp/pvg-dl12-l10n-evidence/WP-0230-DL12-L10N-1795207314259/manifest.json)
