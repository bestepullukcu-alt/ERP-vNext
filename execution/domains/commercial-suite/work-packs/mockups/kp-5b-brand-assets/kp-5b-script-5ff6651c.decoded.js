window.ISD = (function(){
const P = {
 KRD:{n:"Kardiyolin 5 mg",gp:"GP-00412",mdm:"MRK-0012",brand:"Kardiyolin®",c:"danger",ini:"KR"},
 GLS:{n:"Glisemax XR 500 mg",gp:"GP-00388",mdm:"MRK-0009",brand:"Glisemax®",c:"info",ini:"GL"},
 PLM:{n:"Pulmaven İnhaler",gp:"GP-00451",mdm:"MRK-0015",brand:"Pulmaven®",c:"success",ini:"PU"},
 OST:{n:"Osteforte D3",gp:"GP-00297",mdm:"MRK-0004",brand:"Osteforte®",c:"warning",ini:"OS"},
 NVR:{n:"Nevrazol 50 mg",gp:"GP-00466",mdm:"MRK-0018",brand:"Nevrazol®",c:"primary",ini:"NE"}
};
const LBL = {primary:["#e7e7ff","#696cff"],secondary:["#ebeef0","#8592a3"],success:["#e8fadf","#56b51f"],info:["#d7f5fc","#0396b6"],warning:["#fff2d6","#c98700"],danger:["#ffe0db","#e5341a"],dark:["#dcdfe1","#233446"]};
const CN = {tr:{TR:"Türkiye",BY:"Belarus",UZ:"Özbekistan",TM:"Türkmenistan",GE:"Gürcistan",AZ:"Azerbaycan"},en:{TR:"Türkiye",BY:"Belarus",UZ:"Uzbekistan",TM:"Turkmenistan",GE:"Georgia",AZ:"Azerbaijan"}};
const LANGS = [
 {k:"tr",code:"TR",n:["Türkçe","Turkish"]},{k:"en",code:"EN",n:["İngilizce","English"]},{k:"fr",code:"FR",n:["Fransızca","French"]},
 {k:"es",code:"ES",n:["İspanyolca","Spanish"]},{k:"zh",code:"中文",n:["Çince","Chinese"]},{k:"ar",code:"AR",n:["Arapça","Arabic"]},{k:"ru",code:"RU",n:["Rusça","Russian"]}
];
const KIT_ST = {draft:["Taslak","Draft","secondary"],review:["İncelemede","In review","warning"],active:["Aktif","Active","success"],superseded:["Yerini aldı","Superseded","info"],archived:["Arşiv","Archived","dark"]};
const IMG_ST = {draft:["Taslak","Draft","secondary"],review:["İncelemede","In review","warning"],approved:["Onaylı","Approved","success"],expired:["Süresi doldu","Expired","danger"],withdrawn:["Geri çekildi","Withdrawn","dark"]};
const TYPES = {photo:["Fotoğraf","Photo"],diagram:["Diyagram","Diagram"],logo:["Logo","Logo"],icon:["İkon","Icon"],chart:["Grafik görseli","Chart graphic"]};
const SRC = {agency:["Ajans","Agency"],stock:["Stok","Stock"],internal:["Kurum içi","In-house"]};
const KITS = [
 {id:"BK-0007",p:"KRD",ver:"v3",st:"draft",colors:7,logos:3,upd:"30.09.2026",used:0},
 {id:"BK-0001",p:"KRD",ver:"v2",st:"active",colors:6,logos:3,upd:"14.06.2026",used:4},
 {id:"BK-0004",p:"KRD",ver:"v1",st:"superseded",colors:5,logos:2,upd:"02.02.2026",used:0},
 {id:"BK-0008",p:"GLS",ver:"v2",st:"review",colors:5,logos:2,upd:"28.09.2026",used:0},
 {id:"BK-0002",p:"GLS",ver:"v1",st:"active",colors:5,logos:2,upd:"11.03.2026",used:3},
 {id:"BK-0003",p:"PLM",ver:"v1",st:"active",colors:4,logos:3,upd:"22.05.2026",used:2},
 {id:"BK-0006",p:"NVR",ver:"v1",st:"draft",colors:3,logos:1,upd:"25.09.2026",used:0},
 {id:"BK-0005",p:"OST",ver:"v1",st:"archived",colors:4,logos:2,upd:"09.12.2025",used:0}
];
const PATHS = {
 "KP-0012":["Kardiyolin hasta başlangıç yolu","Kardiyolin patient onboarding path","TR"],
 "KP-0019":["Hekim bilgilendirme yolu","Physician information path","AZ"],
 "KP-0023":["Doz hatırlatma yolu","Dose reminder path","GE"],
 "KP-0027":["Kardiyolin eczacı yolu","Kardiyolin pharmacist path","TR"],
 "KP-0031":["Glisemax ilk reçete yolu","Glisemax first prescription path","TR"],
 "KP-0034":["Pulmaven inhaler eğitimi","Pulmaven inhaler training","BY"],
 "KP-0036":["Pulmaven hekim yolu","Pulmaven physician path","UZ"]
};
const IMGS = [
 {id:"VRL-0140",ti:["Kardiyolin ana logo","Kardiyolin primary logo"],type:"logo",st:"approved",cn:[],pr:["KRD"],lic:null,src:"internal",licType:["Kurum içi, süresiz","In-house, perpetual"],holder:"Grand Medical",licStart:"01.01.2024",w:1200,h:400,fmt:"SVG",size:"48 KB",doc:"BLG-2101",file:"kardiyolin_logo_ana.svg",ver:"v3",newVer:null,miss:[],credit:"Grand Medical Marka Ekibi",tags:["logo","marka"],alt:{tr:"Kardiyolin logosu",en:"Kardiyolin logo"},used:{paths:["KP-0012","KP-0019","KP-0023","KP-0027"],pages:["Giriş sayfası şablonu"],kits:["BK-0001","BK-0007"]}},
 {id:"VRL-0141",ti:["Kardiyolin tek renk logo","Kardiyolin one-colour logo"],type:"logo",st:"approved",cn:[],pr:["KRD"],lic:null,src:"internal",licType:["Kurum içi, süresiz","In-house, perpetual"],holder:"Grand Medical",licStart:"01.01.2024",w:1200,h:400,fmt:"SVG",size:"31 KB",doc:"BLG-2102",file:"kardiyolin_logo_tekrenk.svg",ver:"v1",newVer:null,miss:[],credit:"Grand Medical Marka Ekibi",tags:["logo"],alt:{tr:"Kardiyolin logosu, tek renk",en:"Kardiyolin logo, one colour"},used:{paths:["KP-0027"],pages:[],kits:["BK-0001","BK-0007"]}},
 {id:"VRL-0142",ti:["Kardiyolin ters logo (koyu zemin)","Kardiyolin reversed logo (dark)"],type:"logo",st:"review",cn:[],pr:["KRD"],lic:null,src:"internal",licType:["Kurum içi, süresiz","In-house, perpetual"],holder:"Grand Medical",licStart:"01.01.2024",w:1200,h:400,fmt:"SVG",size:"33 KB",doc:"BLG-2103",file:"kardiyolin_logo_ters.svg",ver:"v1",newVer:null,miss:[],credit:"Grand Medical Marka Ekibi",tags:["logo","koyu zemin"],alt:{tr:"Kardiyolin logosu, koyu zemin için",en:"Kardiyolin logo for dark backgrounds"},used:{paths:[],pages:[],kits:[]}},
 {id:"VRL-0152",ti:["Kalp ritmi diyagramı","Heart rhythm diagram"],type:"diagram",st:"approved",cn:["TR","AZ","GE"],pr:["KRD"],lic:"2027-03-31",src:"agency",licType:["Ajans, sınırlı kullanım","Agency, limited use"],holder:"Medivisual Ajans",licStart:"01.04.2025",w:2400,h:1600,fmt:"PNG",size:"1,2 MB",doc:"BLG-2231",file:"kalp_ritmi_diyagram.png",ver:"v2",newVer:"v3",miss:[],credit:"Medivisual",tags:["kardiyoloji","diyagram"],alt:{tr:"Normal ve düzensiz kalp ritmini karşılaştıran diyagram",en:"Diagram comparing normal and irregular heart rhythm",fr:"Schéma comparant un rythme cardiaque normal et irrégulier",es:"Diagrama que compara el ritmo cardíaco normal e irregular",zh:"比较正常与不规则心律的示意图",ar:"مخطط يقارن بين نظم القلب الطبيعي وغير المنتظم",ru:"Схема сравнения нормального и нерегулярного сердечного ритма"},used:{paths:["KP-0012","KP-0019"],pages:["Hastalık bilgisi sayfası"],kits:[]}},
 {id:"VRL-0158",ti:["Hekim–hasta görüşmesi","Physician–patient consultation"],type:"photo",st:"approved",cn:[],pr:["KRD","GLS"],lic:"2026-10-20",src:"stock",licType:["Stok, standart lisans","Stock, standard licence"],holder:"PhotoBank Ltd.",licStart:"20.10.2024",w:4000,h:2667,fmt:"JPG",size:"5,1 MB",doc:"BLG-2240",file:"hekim_hasta_gorusme.jpg",ver:"v1",newVer:"v2",miss:["zh"],credit:"© PhotoBank / M. Arslan",tags:["hekim","hasta","görüşme"],alt:{tr:"Muayene odasında hasta ile konuşan kadın hekim",en:"Female physician talking with a patient in an examination room",fr:"Médecin discutant avec un patient dans une salle d’examen",es:"Médica conversando con un paciente en una sala de consulta",ar:"طبيبة تتحدث مع مريض في غرفة الفحص",ru:"Врач беседует с пациентом в смотровом кабинете"},used:{paths:["KP-0012","KP-0027","KP-0031"],pages:["Giriş sayfası","Hekime danışın bloğu"],kits:[]}},
 {id:"VRL-0163",ti:["Yürüyüş yapan yaşlı çift","Older couple walking"],type:"photo",st:"expired",cn:[],pr:["KRD"],lic:"2026-09-12",src:"stock",licType:["Stok, standart lisans","Stock, standard licence"],holder:"PhotoBank Ltd.",licStart:"12.09.2024",w:3600,h:2400,fmt:"JPG",size:"4,4 MB",doc:"BLG-2198",file:"yasli_cift_yuruyus.jpg",ver:"v1",newVer:null,miss:[],credit:"© PhotoBank",tags:["yaşam tarzı","yürüyüş"],alt:{tr:"Parkta yürüyüş yapan yaşlı çift",en:"Older couple walking in a park"},used:{paths:["KP-0012","KP-0019","KP-0023"],pages:["Yaşam tarzı sayfası"],kits:[]}},
 {id:"VRL-0170",ti:["Glisemax doz ikonları","Glisemax dose icons"],type:"icon",st:"review",cn:[],pr:["GLS"],lic:null,src:"internal",licType:["Kurum içi, süresiz","In-house, perpetual"],holder:"Grand Medical",licStart:"10.09.2026",w:512,h:512,fmt:"SVG",size:"12 KB",doc:"BLG-2255",file:"glisemax_doz_ikon.svg",ver:"v1",newVer:null,miss:[],credit:"Grand Medical",tags:["ikon","doz"],alt:{tr:"Günde bir tablet doz ikonu",en:"Once-daily tablet dose icon"},used:{paths:[],pages:[],kits:[]}},
 {id:"VRL-0171",ti:["HbA1c değişim grafiği","HbA1c change chart"],type:"chart",st:"approved",cn:["TR"],pr:["GLS"],lic:"2027-12-31",src:"internal",licType:["Kurum içi","In-house"],holder:"Grand Medical Medikal",licStart:"01.01.2026",w:1920,h:1080,fmt:"PNG",size:"640 KB",doc:"BLG-2258",file:"hba1c_grafik.png",ver:"v1",newVer:null,miss:["ar","ru"],credit:"Kaynak: Klinik çalışma GLS-301",tags:["grafik","diyabet"],alt:{tr:"24 haftada HbA1c değişimini gösteren çubuk grafik",en:"Bar chart showing HbA1c change over 24 weeks"},used:{paths:["KP-0031"],pages:[],kits:[]}},
 {id:"VRL-0175",ti:["Akciğer kesiti","Lung cross-section"],type:"diagram",st:"draft",cn:[],pr:["PLM"],lic:"2028-01-31",src:"agency",licType:["Ajans, sınırsız","Agency, unlimited"],holder:"Medivisual Ajans",licStart:"01.02.2026",w:3000,h:2000,fmt:"PNG",size:"2,0 MB",doc:"BLG-2262",file:"akciger_kesit.png",ver:"v1",newVer:null,miss:["fr","es","zh","ar","ru"],credit:"Medivisual",tags:["solunum"],alt:{tr:"Bronşları gösteren akciğer kesiti",en:"Lung cross-section showing the bronchi"},used:{paths:[],pages:[],kits:[]}},
 {id:"VRL-0180",ti:["İnhaler kullanım adımları","Inhaler usage steps"],type:"diagram",st:"withdrawn",cn:[],pr:["PLM"],lic:"2027-05-31",src:"agency",licType:["Ajans, sınırlı kullanım","Agency, limited use"],holder:"Medivisual Ajans",licStart:"01.06.2025",w:2400,h:1350,fmt:"PNG",size:"1,6 MB",doc:"BLG-2209",file:"inhaler_adimlar.png",ver:"v2",newVer:null,miss:[],credit:"Medivisual",tags:["eğitim","inhaler"],alt:{tr:"İnhaler kullanımının dört adımı",en:"Four steps of inhaler use"},used:{paths:["KP-0034","KP-0036"],pages:["Kullanım eğitimi sayfası"],kits:[]}},
 {id:"VRL-0184",ti:["Laboratuvar ortamı","Laboratory setting"],type:"photo",st:"approved",cn:["BY","UZ","TM"],pr:["KRD","GLS","PLM"],lic:"2027-06-30",src:"agency",licType:["Ajans, bölgesel","Agency, regional"],holder:"Caspian Visuals",licStart:"01.07.2025",w:4200,h:2800,fmt:"JPG",size:"6,3 MB",doc:"BLG-2214",file:"laboratuvar.jpg",ver:"v1",newVer:null,miss:[],credit:"© Caspian Visuals",tags:["üretim","kalite"],alt:{tr:"Laboratuvarda çalışan araştırmacı",en:"Researcher working in a laboratory"},used:{paths:["KP-0036"],pages:[],kits:[]}},
 {id:"VRL-0188",ti:["Kardiyolin ürün kutusu","Kardiyolin pack shot"],type:"photo",st:"approved",cn:[],pr:["KRD"],lic:null,src:"internal",licType:["Kurum içi, süresiz","In-house, perpetual"],holder:"Grand Medical",licStart:"15.03.2026",w:3000,h:3000,fmt:"PNG",size:"3,8 MB",doc:"BLG-2244",file:"kardiyolin_kutu.png",ver:"v1",newVer:null,miss:["tr"],credit:"Grand Medical",tags:["ürün","ambalaj"],alt:{en:"Kardiyolin 5 mg pack"},used:{paths:[],pages:[],kits:[]}}
];
const DOCS = [
 {id:"BLG-2240",file:"hekim_hasta_gorusme.jpg",ver:"v2",w:4000,h:2667,fmt:"JPG",size:"5,1 MB",warn:null,linked:"VRL-0158"},
 {id:"BLG-2231",file:"kalp_ritmi_diyagram.png",ver:"v3",w:2400,h:1600,fmt:"PNG",size:"1,2 MB",warn:null,linked:"VRL-0152"},
 {id:"BLG-2266",file:"ofis_ekibi_kucuk.jpg",ver:"v1",w:640,h:427,fmt:"JPG",size:"180 KB",warn:"lowres",linked:null},
 {id:"BLG-2270",file:"kampanya_gorselleri.psd",ver:"v1",w:5000,h:3000,fmt:"PSD",size:"88 MB",warn:"unsupported",linked:null},
 {id:"BLG-2273",file:"eczane_tezgah.jpg",ver:"v1",w:3200,h:2133,fmt:"JPG",size:"3,4 MB",warn:null,linked:null}
];
const COLORS = [
 {id:1,hex:"#C8102E",role:"primary",text:true,bg:true,n:{tr:"Kardiyolin Kırmızı",en:"Kardiyolin Red",fr:"Rouge Kardiyolin",es:"Rojo Kardiyolin",zh:"心律红",ar:"أحمر كارديولين",ru:"Красный Кардиолин"}},
 {id:2,hex:"#1B2A4A",role:"secondary",text:true,bg:true,n:{tr:"Gece Mavisi",en:"Midnight Blue",fr:"Bleu nuit",es:"Azul medianoche",zh:"午夜蓝",ar:"أزرق ليلي",ru:"Полуночный синий"}},
 {id:3,hex:"#FF6F59",role:"accent",text:true,bg:true,n:{tr:"Mercan",en:"Coral",fr:"Corail",es:"Coral"}},
 {id:4,hex:"#2E3440",role:"text",text:true,bg:false,n:{tr:"Antrasit",en:"Anthracite",fr:"Anthracite",es:"Antracita",zh:"炭灰",ar:"فحمي",ru:"Антрацит"}},
 {id:5,hex:"#F7F4EF",role:"background",text:false,bg:true,n:{tr:"Kırık Beyaz",en:"Off-white",fr:"Blanc cassé",es:"Blanco roto",zh:"米白",ar:"أبيض مكسور",ru:"Молочный"}},
 {id:6,hex:"#D9DCE1",role:"background",text:true,bg:true,n:{tr:"Açık Gri",en:"Light Grey"}},
 {id:7,hex:"#B88A2E",role:"accent",text:false,bg:true,n:{tr:"Altın",en:"Gold",fr:"Or",es:"Oro",ru:"Золотой"}}
];
const T = {
 studio:["İçerik Stüdyosu","Content Studio"],brandKits:["Marka Kitleri","Brand Kits"],brandKit:["Marka kiti","Brand kit"],gallery:["Görsel Kütüphanesi","Image Library"],newImage:["Yeni görsel","New image"],
 docMgmt:["Belge Yönetimi","Document Management"],extras:["Mevcut sayfalara ekler","Additions to existing pages"],
 product:["Ürün","Product"],products:["Ürünler","Products"],status:["Durum","Status"],country:["Ülke","Country"],countries:["Ülkeler","Countries"],type:["Tür","Type"],lang:["Dil","Language"],
 all:["Tümü","All"],allProducts:["Tüm ürünler","All products"],allCountries:["Tüm ülkeler","All countries"],allTypes:["Tüm türler","All types"],allStatuses:["Tüm durumlar","All statuses"],
 onlyActive:["Yalnız aktif","Active only"],onlyUsable:["Yalnız kullanılabilir","Usable only"],onlySelectable:["Yalnız seçilebilir","Selectable only"],
 searchKits:["Kod veya ürün ara","Search code or product"],searchImgs:["Başlık, kod veya etiket ara","Search title, code or tag"],newKit:["Yeni marka kiti","New brand kit"],
 code:["Kod","Code"],version:["Sürüm","Version"],colors:["Renk","Colours"],logos:["Logo","Logos"],updated:["Son güncelleme","Last updated"],usedPaths:["Kullanan yol","Paths using"],
 licEnd:["Lisans bitişi","Licence end"],licAll:["Tüm tarihler","Any date"],lic30:["30 gün içinde","Within 30 days"],grid:["Izgara","Grid"],list:["Liste","List"],
 tabPalette:["Renk paleti","Colour palette"],tabLogos:["Logolar","Logos"],tabType:["Yazı tipleri ve kurallar","Fonts and rules"],tabPreview:["Önizleme","Preview"],tabHistory:["Sürüm geçmişi","Version history"],tabUsage:["Nerede kullanılıyor","Where used"],
 nameLang:["Ad dili","Name language"],addColor:["Renk ekle","Add colour"],hex:["HEX","HEX"],role:["Rol","Role"],colorName:["Renk adı","Colour name"],
 allowText:["Metinde kullanılabilir","Allowed for text"],allowBg:["Zeminde kullanılabilir","Allowed as background"],moveUp:["Yukarı taşı","Move up"],moveDown:["Aşağı taşı","Move down"],remove:["Kaldır","Remove"],
 paletteHint:["Kartlar sayfa tasarımcısındaki sırayla gösterilir. Sürükleyin ya da oklarla taşıyın.","Cards appear in this order in the page designer. Drag or use the arrows."],
 identity:["Kimlik","Identity"],gpLabel:["Ürün (MDM Global Product)","Product (MDM Global Product)"],lockedAfterCreate:["Oluşturulduktan sonra değiştirilemez","Cannot be changed after creation"],
 mdmBrand:["Bağlı MDM markası","Linked MDM brand"],infoOnly:["Bilgi amaçlı","For information"],createdBy:["Hazırlayan","Prepared by"],prevActive:["Önceki aktif sürüm","Previous active version"],
 approvalSteps:["Onay adımları","Approval steps"],approverTbd:["Onaycı grubu yapılandırmaya göre (Regülasyon veya MLR)","Approver group per configuration (Regulatory or MLR)"],
 extraStep:["Ek adım (gerekirse)","Additional step (if needed)"],medLegal:["Medikal / Hukuk","Medical / Legal"],
 submitted:["Gönderildi","Submitted"],preparing:["Hazırlanıyor","In preparation"],step1:["Onay adımı 1","Approval step 1"],waiting:["Bekliyor","Waiting"],approvedStep:["Onaylandı","Approved"],rejectedStep:["Reddedildi","Rejected"],
 decisionComment:["Karar yorumu","Decision comment"],commentPh:["Kararınızın gerekçesini yazın","Explain your decision"],approve:["Onayla","Approve"],reject:["Reddet","Reject"],
 rejectNeeds:["Reddetmek için yorum zorunludur.","A comment is required to reject."],
 ownRecord:["Bu kaydı siz onaya gönderdiniz. Karar paneli gönderene gösterilmez.","You submitted this record. The decision panel is not shown to the submitter."],
 viewerNote:["Bu kayıt için karar yetkiniz yok.","You have no decision rights for this record."],
 draftNote:["Kayıt taslak. Onaya gönderildiğinde adımlar başlar.","Record is a draft. Steps start when it is submitted."],
 flowNote:["Adım sayısı ve onaycı rolü henüz kesinleşmedi; panel genel çizilmiştir.","Number of steps and approver role are not final; panel is generic."],
 submit:["Onaya gönder","Submit for approval"],withdrawReq:["Geri çek","Withdraw"],newVersion:["Yeni sürüm","New version"],archive:["Arşivle","Archive"],save:["Kaydet","Save"],unpublish:["Yayından geri çek","Unpublish"],
 bReview:["Bu kayıt onay bekliyor ve düzenlenemez.","This record is awaiting approval and cannot be edited."],
 bActive:["Aktif sürüm düzenlenemez. Değişiklik için yeni sürüm oluşturun.","The active version cannot be edited. Create a new version to make changes."],
 bOld:["Bu sürüm salt okunurdur.","This version is read-only."],
 lMain:["Ana logo","Primary logo"],lMono:["Tek renk","One colour"],lInv:["Ters (koyu zemin)","Reversed (dark background)"],
 lMainD:["Varsayılan kullanım","Default use"],lMonoD:["Tek renk baskı ve kabartma","Single-colour print and emboss"],lInvD:["Koyu ve renkli zeminler","Dark and coloured backgrounds"],
 pickAsset:["Onaylı varlık seçin · Belge Yönetimi","Select approved asset · Document Management"],choose:["Görsel seç","Choose image"],change:["Değiştir","Change"],
 minSize:["En küçük boyut","Minimum size"],clearSpace:["Boşluk payı","Clear space"],clearSpaceUnit:["% logo yüksekliği","% of logo height"],allowedBgs:["Kullanılabilir zeminler","Allowed backgrounds"],
 bgLight:["Açık","Light"],bgDark:["Koyu","Dark"],bgPhoto:["Fotoğraf üzeri","On photo"],bgColor:["Renkli","Coloured"],
 fonts:["Yazı tipleri","Fonts"],headFam:["Başlık ailesi","Heading family"],bodyFam:["Gövde ailesi","Body family"],fallback:["Yedek aileler","Fallback families"],
 fontFile:["Yazı tipi dosyası (isteğe bağlı)","Font file (optional)"],pickFromDocs:["Belge Yönetimi'nden seç","Select from Document Management"],
 typoRules:["Tipografi kuralları","Typography rules"],minFont:["En küçük yazı boyutu","Minimum font size"],headScale:["Başlık ölçeği","Heading scale"],bodyText:["Gövde","Body"],
 usageNotes:["Kullanım notları","Usage notes"],usageNotesHint:["Yapılmaması gerekenler","Things to avoid"],
 lightBg:["Açık zemin","Light background"],darkBg:["Koyu zemin","Dark background"],previewHint:["Palet, logo ve yazı tiplerinin örnek bir Bilgi Yolu sayfa parçasında görünümü.","How the palette, logo and fonts look on a sample Knowledge Path page fragment."],
 sampleHead:["Tedavi sürecinizi birlikte planlayalım","Let’s plan your treatment together"],sampleBody:["Bu alan, Bilgi Yolu sayfasında gövde metninin nasıl görüneceğini gösterir.","This area shows how body text will look on a Knowledge Path page."],sampleBtn:["Ürün bilgisini oku","Read product information"],sampleFoot:["Güvenlilik bilgisi için KÜB’e bakınız.","See the SmPC for safety information."],
 vVer:["Sürüm","Version"],vDate:["Tarih","Date"],vBy:["Kişi","By"],vNote:["Not","Note"],
 kitPaths:["Bu kiti kullanan Bilgi Yolları","Knowledge Paths using this kit"],kitTemplates:["Sayfa şablonları","Page templates"],draftUnused:["Taslak sürüm henüz kullanılmıyor. Aşağıda aktif sürümün (v2) kullanımı gösteriliyor.","Draft version is not used yet. Usage of the active version (v2) is shown below."],
 preview:["Önizleme","Preview"],details:["Bilgiler","Details"],altText:["Alternatif metin","Alt text"],missing:["Eksik","Missing"],captionCredit:["Altyazı / kaynak","Caption / credit"],tags:["Etiketler","Tags"],
 rights:["Kullanım hakkı","Usage rights"],rSource:["Kaynak","Source"],licType:["Lisans türü","Licence type"],holder:["Hak sahibi","Rights holder"],validFrom:["Geçerlilik başlangıcı","Valid from"],validTo:["Bitiş tarihi","End date"],
 perpetual:["Süresiz","Perpetual"],daysLeft:["gün kaldı","days left"],expiredOn:["Süresi doldu","Expired"],
 sourceDoc:["Kaynak belge","Source document"],pinned:["Sabitlenen sürüm","Pinned version"],latestInDocs:["Belge Yönetimi'nde güncel","Latest in Document Management"],openDoc:["Belgeyi aç","Open document"],
 whereUsed:["Nerede kullanılıyor","Where used"],kpLabel:["Bilgi Yolları","Knowledge Paths"],pages:["Sayfalar","Pages"],asLogo:["Marka kitleri (logo olarak)","Brand kits (as logo)"],none:["Yok","None"],
 aLicSoonT:["Lisans 18 gün içinde bitiyor","Licence ends in 18 days"],aLicSoon:["Bitişte görsel seçilemez olur ve kullanan 3 yolun sorumlularına uyarı gider.","On expiry the image becomes unselectable and owners of the 3 paths using it are alerted."],
 aNewVerT:["Yeni sürüm var — güncelle?","New version available — update?"],aNewVer:["Belge Yönetimi'nde daha yeni bir sürüm var. Görsel kendiliğinden değişmez; güncelleme yeniden onay gerektirir.","A newer version exists in Document Management. The image does not change automatically; updating requires re-approval."],update:["Güncelle","Update"],
 aExpT:["Lisans 12.09.2026 tarihinde doldu · etkilenen 3 yol","Licence expired on 12.09.2026 · 3 paths affected"],aExp:["Görsel artık seçilemez. Kullanan yolların sorumlularına uyarı gönderildi.","The image can no longer be selected. Owners of paths using it were alerted."],
 aWdT:["Görsel yayından geri çekildi · etkilenen 2 yol","Image unpublished · 2 paths affected"],aWd:["Kullanan yolların sorumlularına uyarı gönderildi.","Owners of paths using it were alerted."],showPaths:["Yolları göster","Show paths"],
 lockedTitle:["Önizleme kilitli","Preview locked"],lockedText:["Bu belgeyi Belge Yönetimi'nde görme yetkiniz yok.","You are not allowed to view this document in Document Management."],
 srcPick:["Belge Yönetimi'nden seç","Select from Document Management"],srcPickD:["Mevcut bir görsel belgesini bağlayın.","Link an existing image document."],
 srcUpload:["Dosya yükle","Upload file"],srcUploadD:["Dosya Belge Yönetimi'ne kaydedilir, buradan bağlanır.","The file is saved to Document Management and linked here."],
 step1T:["1. Kaynak","1. Source"],step2T:["2. Bilgiler","2. Details"],step3T:["3. Kullanım hakkı","3. Usage rights"],
 dropTitle:["Dosyayı buraya sürükleyin ya da seçin","Drag a file here or browse"],dropSub:["PNG, JPG, SVG, WEBP · en çok 25 MB · Belge Yönetimi › Tanıtım görselleri klasörüne kaydedilir","PNG, JPG, SVG, WEBP · max 25 MB · saved to Document Management › Promotional images"],browse:["Dosya seç","Browse"],
 auto:["Otomatik","Automatic"],dims:["Boyut","Dimensions"],fileType:["Dosya türü","File type"],fileSize:["Dosya büyüklüğü","File size"],willPin:["Sabitlenecek sürüm","Version to pin"],
 pinNote:["Belgenin yeni sürümü gelirse görsel kendiliğinden değişmez; güncellemek için uyarı gösterilir.","If a new document version arrives, the image does not change automatically; you will be prompted to update."],
 wLow:["Düşük çözünürlük: en az 1200 px genişlik önerilir.","Low resolution: at least 1200 px width recommended."],wUns:["Desteklenmeyen tür: PSD görsel olarak kullanılamaz.","Unsupported type: PSD cannot be used as an image."],alreadyLinked:["Zaten bağlı:","Already linked:"],
 title:["Başlık","Title"],titlePh:["Örn. Hekim–hasta görüşmesi","e.g. Physician–patient consultation"],required:["zorunlu","required"],altReq:["7 dilde zorunlu","Required in 7 languages"],altPh:["Görseli görmeyen biri için kısaca tanımlayın","Describe the image briefly for someone who cannot see it"],
 countriesHint:["Boş bırakılırsa tüm ülkeler","Leave empty for all countries"],tagsPh:["Etiket yazıp Enter’a basın","Type a tag and press Enter"],
 cancel:["Vazgeç","Cancel"],saveDraft:["Taslak olarak kaydet","Save as draft"],missingFields:["Gönderim için eksik:","Missing to submit:"],
 pickerTitle:["Onaylı görsel seçin","Select an approved image"],pickerFor:["Marka kiti · logo alanı","Brand kit · logo field"],context:["Bağlam","Context"],
 ctxNote:["Bağlam, pencereyi açan ekrandan gelir.","Context comes from the screen that opened this window."],select:["Seç","Select"],selectToPreview:["Önizlemek için bir görsel seçin.","Select an image to preview."],
 selectable:["seçilebilir","selectable"],of:["görselden","images,"],rNotApproved:["Onay bekliyor","Awaiting approval"],rDraft:["Taslak · onaylı değil","Draft · not approved"],rWithdrawn:["Yayından geri çekildi","Unpublished"],
 rCountry:["Bu ülke için izinli değil","Not allowed for this country"],rProduct:["Bu ürün için izinli değil","Not allowed for this product"],rExpired:["Lisans {d} tarihinde doldu","Licence expired on {d}"],rAlt:["{l} alternatif metin yok","No {l} alt text"],
 emptyKits:["Henüz marka kiti yok","No brand kits yet"],emptyKitsD:["Bir ürün için ilk marka kitini oluşturun. Her ürünün tek aktif sürümü olur.","Create the first brand kit for a product. Each product has one active version."],
 emptyImgs:["Görsel kütüphanesi boş","The image library is empty"],emptyImgsD:["Belge Yönetimi'nden bir görsel bağlayın ya da dosya yükleyin.","Link an image from Document Management or upload a file."],
 emptyRec:["Kayıt bulunamadı","Record not found"],emptyRecD:["Kayıt silinmiş ya da taşınmış olabilir.","The record may have been deleted or moved."],
 errTitle:["Veriler yüklenemedi","Data could not be loaded"],errText:["Bağlantı sırasında bir sorun oluştu. Yeniden deneyin; sorun sürerse sistem yöneticinize bildirin.","Something went wrong while connecting. Try again; if it persists, contact your administrator."],retry:["Yeniden dene","Try again"],
 noMatch:["Filtrelere uyan kayıt yok.","No records match the filters."],clearFilters:["Filtreleri temizle","Clear filters"],showing:["kayıt gösteriliyor","records shown"],
 docDetail:["Belge ayrıntısı","Document details"],docType:["Belge türü","Document type"],imageDoc:["Görsel","Image"],owner:["Sahibi","Owner"],folder:["Klasör","Folder"],inlinePreview:["Satır içi önizleme","Inline preview"],
 usedAsImg:["Onaylı görsel olarak kullanılıyor","Used as approved image"],pinnedBy:["sabitli","pinned"],current:["Güncel","Current"],newBadge:["EK","NEW"],
 mdmBrandDetail:["Marka ayrıntısı","Brand details"],name:["Ad","Name"],kitLink:["Marka kiti","Brand kit"],readonly:["salt okunur","read-only"],
 toastSubmitted:["Onaya gönderildi","Submitted for approval"],toastWithdrawn:["Onaydan geri çekildi; kayıt yeniden taslak","Withdrawn; record is a draft again"],toastSaved:["Taslak kaydedildi","Draft saved"],toastNewVer:["Yeni sürüm taslağı oluşturuldu","New version draft created"],toastArchived:["Arşivlendi","Archived"],toastApproved:["Onaylandı","Approved"],toastRejected:["Reddedildi; kayıt taslağa döndü","Rejected; record returned to draft"],toastPicked:["Görsel bağlandı","Image linked"],toastUnpub:["Yayından geri çekildi; kullanan yollara uyarı gitti","Unpublished; paths using it were alerted"],toastUpdate:["Yeni sürüm taslağı oluşturuldu; yeniden onay gerekir","New version draft created; re-approval required"],
 you:["Siz","You"]
};
return {P,LBL,CN,LANGS,KIT_ST,IMG_ST,TYPES,SRC,KITS,PATHS,IMGS,DOCS,COLORS,T};
})();
