# DL-12 — aynı onaylı kapsamın üç işçiye ayrılması

2026-11-20 (Asia/Baku). Sahip: PVG CT. Tür: plan; ayrı portföy plan satırı yok. Yetki rev21 MOD-0230 pack/domain ve kayıttaki insan onaylarıdır; yeni modül, FU, AC veya yazma kapsamı açılmaz. Ayrıntılı değişmez sınırlar module pack ve `docs/roadmap/plans/pvg-mod0230-dl12-parallel-delivery-plan-2026-11-20.md` içindedir.

| İş | Tür / sahiplik | Bağımlılık | Yazma sınırı |
| --- | --- | --- | --- |
| WP-0230-DL12-BE | Sahip: arka uç/API kabulü; CT yönetir, işçi uygular | İki PREP ve REGISTRY-01 entegre | Paketteki tam yedi backend/test yolu; üç production dosyasında update/reason/item-response, mevcut üç testte yalnız isimli beklenti/tek satır; yeni tek test dosyası |
| WP-0230-DL12-L10N | Sahip: sayfa metni; CT yönetir, l10n-agent uygular | İki PREP ve REGISTRY-01 entegre | Yedi CaseIntakeTriageIndex sayfa RESX dosyasında yalnız DraftNotEditable; SharedResource/JS/bridge yok |
| WP-0230-DL12-FE | Sahip: ekran/MVC ve gerçek connected no-loss kabulü; CT yönetir, işçi uygular | BE ve L10N bağımsız doğrulanıp CT'ye entegre | Paketteki tam altı frontend yolu; önceki PREP csproj/CI dosyaları değişmez |

En çok iki aktif alt çalışma. BE ile L10N paralel, FE daha sonra. Her normal prompt kendi gerçek temiz dal/HEAD ve source-pack sürümüne bağlanır; bağımsız inceleme ve gerçek dağıtım kapısı 0 olmadan uygulama yok. HIGH BE/FE kabul üreticisini CT dondurur; işçi bu üreticiyi değiştirmez. Beklentinin adını saymak davranış kanıtı değildir. Arka uç için yeni testler eski ürün üzerinde derlenip gerçek assertion RED vermeden implementation başlamaz; eski 211 GREEN ve Web kayıp gözlemi bu RED'in yerine geçmez.

Plan yazıldığında WIN hâlâ VERIFYING; feature işleri PLANNED'dır ve dağıtılmamıştır. Ana WP-0230-DL12 BLOCKED kalır. PREP'ler entegre olunca DoR ayrıca yeniden ölçülür. Çocuk işler parent BLOCKED satırına bağımlı yapılmaz; parent'ın mevcut PREP bağımlılıkları korunur, döngü yaratılmaz. Parent ilerlemesi çocukların gerçek kapı/teslimlerine bağlı yönetim kaydıdır; başarı uydurulmaz.

Her doğrulanmış teslim CT dalına ayrı no-ff merge ile alınır, birleşik testler koşulur. Sonuç tek PR'dır; kesin final CT dalı/SHA için uzak işlem kapısı ayrıca uygulanır. Final gerçek Linux/Phase1 ve ikinci insan main merge kapıları saklıdır. Operasyonel kullanım kapalıdır. Worktree temizliği main erişilebilirliği ve korunmamış iş olmadığı ölçülünce yönetilen arşiv aracıyla yapılır.
