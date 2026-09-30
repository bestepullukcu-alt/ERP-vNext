
const PR='#2b2c9f';
const CC=['TR','UZ','AZ'];
const CN={TR:'Türkiye',UZ:'Özbekistan',AZ:'Azerbaycan'};
const CL={TR:['tr'],UZ:['uz','ru'],AZ:['az']};
const LN={en:'English',tr:'Türkçe',uz:'Oʻzbekcha',ru:'Русский',az:'Azərbaycanca'};
const ST={
approved:{l:'Onaylı',i:'bx-check-circle',bg:'#e5f6dc',fg:'#2d7310',bd:'#cdeebb'},
review:{l:'İncelemede',i:'bx-time-five',bg:'#d8f2fa',fg:'#03657e',bd:'#b5e6f3'},
draft:{l:'Taslak',i:'bx-edit-alt',bg:'#eceef1',fg:'#4b5866',bd:'#dde1e6'},
recheck:{l:'Gözden geçirilmeli',i:'bx-revision',bg:'#fff0cc',fg:'#7a4e00',bd:'#ffdf8f'},
expiring:{l:'Süresi doluyor',i:'bx-alarm-exclamation',bg:'#ffe2dc',fg:'#a8230b',bd:'#ffc4b8'},
closed:{l:'Açılmadı',i:'bx-block',bg:'#f5f5f9',fg:'#5b6673',bd:'#d9dee3'},
none:{l:'Açılmadı',i:'bx-plus-circle',bg:'#ffffff',fg:'#2b2c9f',bd:'#b9bbef'},
na:{l:'Kapsam dışı',i:'bx-minus',bg:'transparent',fg:'#8592a3',bd:'transparent'},
archived:{l:'Pasif / Arşiv',i:'bx-archive',bg:'#f5f5f9',fg:'#5b6673',bd:'#d9dee3'}};
const ROLES={
global:{n:'Global Medikal · Elif Aydın',cs:CC,core:true,edit:CC},
tr:{n:'Yerel TR ekibi · Mert Kaya',cs:CC,core:false,edit:['TR'],own:['TR']},
uz:{n:'Yerel UZ hukuk onaylayıcı · Aziz Rakhimov',cs:CC,core:false,edit:['UZ'],own:['UZ'],appr:'UZ'},
ro:{n:'Salt okuma · İç denetim',cs:CC,core:false,edit:[]},
none:{n:'Yetkisiz kullanıcı',cs:[],core:false,edit:[]}};
const PEOPLE={TR:['Ayşe Yılmaz','Burak Şahin','Mert Kaya'],UZ:['Dilnoza Karimova','Aziz Rakhimov','Bekzod Tursunov'],AZ:['Leyla Məmmədova','Rəşad Əliyev','Nigar Hüseynova']};
const HOLDER={TR:'Grand Medical İlaç A.Ş.',UZ:'Grand Medical Uzbekistan MChJ',AZ:'Grand Medical Azerbaijan MMC'};
const CLAIMS=[
{id:'C1',code:'CLM-TUTUKON-01',name:'Sindirim konforuna destek',product:'TUTUKON',type:'core',coreV:'1.0',gdate:'12.01.2026',aud:['Gastroenteroloji','Aile Hekimliği'],team:'Gastro İş Birimi',text:"TUTUKON's herbal formulation helps support digestive comfort.",quals:['Food supplement. Not a medicinal product.'],langs:['en','tr','uz','ru'],ev:4,
cs:{TR:{s:'approved',v:'1.1',cv:'1.0',ad:'02.03.2026',texts:[['tr','TUTUKON, bitkisel formülasyonu ile sindirim konforunun desteklenmesine yardımcı olur.']],quals:[['tr','Takviye edici gıdadır, ilaç değildir.']],valid:'01.03.2026 – 28.02.2028',adapt:'Birebir çeviri'},
UZ:{s:'review',v:'1.0',cv:'1.0',note:'Yerel hukuk onayı bekliyor',texts:[['uz',"TUTUKON o'simlik tarkibi bilan hazm qulayligini qo'llab-quvvatlashga yordam beradi."],['ru','Растительная формула TUTUKON помогает поддерживать пищеварительный комфорт.']],quals:[['uz',"Biologik faol qo'shimcha. Dori vositasi emas."],['ru','БАД. Не является лекарством.']],valid:'01.11.2026 – 31.10.2027',adapt:'Birebir çeviri'},
AZ:{s:'closed',note:'Ürün ruhsatlı değil'}}},
{id:'C2',code:'CLM-TUTUKON-02',name:'Şişkinlik ve bağırsak düzensizliğinde sindirim konforu',product:'TUTUKON',type:'core',coreV:'1.0',gdate:'03.04.2026',aud:['Gastroenteroloji','Aile Hekimliği'],team:'Gastro İş Birimi',text:'Supports digestive comfort in people experiencing bloating and bowel irregularity.',quals:['Food supplement. Not a medicinal product.'],langs:['en','tr','uz'],ev:1,
cs:{TR:{s:'approved',v:'1.0',cv:'1.0',ad:'28.05.2026',texts:[['tr','TUTUKON, şişkinlik ve bağırsak düzensizliği yaşayan kişilerde sindirim konforunu destekler.']],quals:[['tr','Takviye edici gıdadır, ilaç değildir.']],valid:'01.06.2026 – 31.05.2028',adapt:'Birebir çeviri'},
UZ:{s:'draft',v:'0.1',cv:'1.0',note:'ru metin eksik',texts:[['uz',"TUTUKON qorin dam bo'lishi va ichak faoliyati buzilishini boshdan kechirayotgan odamlarda hazm qulayligini qo'llab-quvvatlaydi."],['ru','']],quals:[['uz',"Biologik faol qo'shimcha. Dori vositasi emas."]],valid:'—',adapt:'Birebir çeviri'},
AZ:{s:'closed',note:'Ürün ruhsatlı değil'}}},
{id:'C3',code:'CLM-ALMIBA-01',name:'Hemodiyalizde sekonder karnitin eksikliği',product:'ALMIBA',type:'core',coreV:'2.0',gdate:'15.09.2026',aud:['Nefroloji'],team:'Nefroloji İş Birimi',text:'Indicated for secondary carnitine deficiency in haemodialysis patients.',quals:['Prescription-only medicine.','For use under physician supervision.'],langs:['en','tr','uz','ru','az'],ev:3,
cs:{TR:{s:'approved',v:'2.0',cv:'2.0',ad:'18.09.2026',texts:[['tr','ALMIBA, hemodiyaliz hastalarında sekonder karnitin eksikliğinde endikedir.']],quals:[['tr','Reçete ile satılır.'],['tr','Hekim kontrolünde kullanılır.']],valid:'18.09.2026 – 17.09.2028',adapt:'Birebir çeviri'},
UZ:{s:'recheck',v:'1.0',cv:'1.0',ad:'25.03.2025',note:'Çekirdek v2.0 onaylandı',texts:[['uz',"ALMIBA gemodializdagi bemorlarda ikkilamchi karnitin yetishmovchiligida qo'llaniladi."],['ru','ALMIBA применяется при вторичном дефиците карнитина у пациентов на гемодиализе.']],quals:[['uz',"Retsept bo'yicha beriladi."],['ru','Отпускается по рецепту.']],valid:'01.04.2025 – 31.03.2027',adapt:'Birebir çeviri'},
AZ:{s:'expiring',v:'2.0',cv:'2.0',ad:'19.09.2026',note:'Bitiş 31.10.2026',texts:[['az','ALMIBA hemodializ xəstələrində ikincili karnitin çatışmazlığı zamanı göstərişdir.']],quals:[['az','Resept əsasında buraxılır.']],valid:'19.09.2026 – 31.10.2026',adapt:'Birebir çeviri'}}},
{id:'C4',code:'CLM-ALMIBA-02',name:'Enerji metabolizması – mitokondriyal taşıma',product:'ALMIBA',type:'core',coreV:'1.0',gdate:'20.01.2026',aud:['Nefroloji'],team:'Nefroloji İş Birimi',text:'Levocarnitine supports energy metabolism by transporting long-chain fatty acids into mitochondria.',quals:['Prescription-only medicine.'],langs:['en','tr'],ev:3,
cs:{TR:{s:'recheck',v:'1.0',cv:'1.0',ad:'28.01.2026',note:'Kanıt belgesi güncellendi',texts:[['tr','Levokarnitin, uzun zincirli yağ asitlerini mitokondriye taşıyarak enerji metabolizmasını destekler.']],quals:[['tr','Reçete ile satılır.']],valid:'01.02.2026 – 31.01.2028',adapt:'Birebir çeviri'},
UZ:{s:'none',note:'Henüz karar verilmedi'},
AZ:{s:'closed',note:'Yerel mevzuat izin vermiyor'}}},
{id:'C5',code:'CLM-TR-TUTUKON-03',name:'Şişkinlik hissinde bitkisel destek',product:'TUTUKON',type:'local',coreV:null,aud:['Gastroenteroloji'],team:'TR Pazarlama',text:null,quals:[],langs:['tr'],ev:0,
cs:{TR:{s:'draft',v:'0.1',texts:[['tr','TUTUKON, bitkisel içeriğiyle şişkinlik hissinin hafiflemesine katkıda bulunur.']],quals:[['tr','Takviye edici gıdadır, ilaç değildir.']],valid:'—',adapt:'Yalnız yerel'},UZ:{s:'na',note:'Yalnız TR'},AZ:{s:'na',note:'Yalnız TR'}}}];
const DOCS={
'DOC-0188':{t:'TTK-CS-02 Klinik Çalışma Raporu',type:'Klinik çalışma',scope:'Global · en',ver:'v2'},
'DOC-0142':{t:'TUTUKON Formülasyon Dosyası',type:'İç veri',scope:'Global · en',ver:'v3'},
'DOC-0460':{t:'TUTUKON Takviye Edici Gıda Onay Yazısı',type:'Ruhsat yazısı',scope:'Türkiye · tr',ver:'v1'},
'DOC-0515':{t:"TUTUKON Davlat ro'yxatidan o'tkazish guvohnomasi",type:'Ruhsat yazısı',scope:'Özbekistan · uz, ru',ver:'v1'},
'DOC-0312':{t:'ALMIBA Core SmPC',type:'KÜB/KT',scope:'Global · en',ver:'v2'},
'DOC-0311':{t:'ALMIBA KÜB (Türkiye)',type:'KÜB/KT',scope:'Türkiye · tr',ver:'v5',cur:'v5',curDate:'16.09.2026'},
'DOC-0420':{t:'Pekala J. ve ark. L-carnitine – metabolic functions and meaning in humans life. Curr Drug Metab. 2011;12(7)',type:'Literatür',scope:'Global · en',ver:'v1'},
'DOC-0510':{t:"ALMIBA Tibbiy qo'llash bo'yicha yo'riqnoma (KÜB-UZ)",type:'KÜB/KT',scope:'Özbekistan · uz, ru',ver:'v1',valid:'14.03.2029'},
'DOC-0199':{t:'ALMIBA Özbekistan Ruhsat Yazısı',type:'Ruhsat yazısı',scope:'Özbekistan · uz, ru',ver:'v2',valid:'15.11.2026',exp:true},
'DOC-0305':{t:'ALMIBA KÜB (Azerbaycan)',type:'KÜB/KT',scope:'Azerbaycan · az',ver:'v1'}};
const EV={
C1:[{doc:'DOC-0188',ref:'Bölüm 6.2 · s. 18 · Tablo 5',quote:'Participants reported improved digestive comfort scores versus baseline at week 4.',sup:'helps support digestive comfort'},{doc:'DOC-0142',ref:'Bölüm 2 · s. 3',quote:'The product is a herbal formulation of standardised plant extracts.',sup:"TUTUKON's herbal formulation"}],
'C1-TR':[{doc:'DOC-0460',ref:'s. 1 · Onay kapsamı',quote:'Ürün takviye edici gıda olarak onaylanmıştır.',sup:'Takviye edici gıdadır, ilaç değildir. (niteleyici)'}],
'C1-UZ':[{doc:'DOC-0515',ref:'s. 1',quote:"Biologik faol qo'shimcha sifatida ro'yxatdan o'tkazilgan.",sup:"Biologik faol qo'shimcha (niteleyici)"}],
C2:[{doc:'DOC-0188',ref:'Bölüm 6.3 · s. 21 · Tablo 6',quote:'Improvements were observed in bloating and bowel regularity scores.',sup:'bloating and bowel irregularity'}],
C3:[{doc:'DOC-0312',ref:'Bölüm 4.1 · s. 1',quote:'Treatment of secondary carnitine deficiency in patients on haemodialysis.',sup:'secondary carnitine deficiency in haemodialysis patients'}],
'C3-TR':[{doc:'DOC-0311',pin:'v5',ref:'Bölüm 4.1 · s. 1',quote:'Hemodiyaliz uygulanan hastalarda sekonder karnitin eksikliğinin tedavisinde endikedir.',sup:'hemodiyaliz hastalarında sekonder karnitin eksikliğinde endikedir'}],
'C3-AZ':[{doc:'DOC-0305',ref:'Bölmə 4.1 · s. 1',quote:'Hemodializ alan xəstələrdə ikincili karnitin çatışmazlığının müalicəsi.',sup:'ikincili karnitin çatışmazlığı'}],
C4:[{doc:'DOC-0312',ref:'Bölüm 5.1 · s. 4',quote:'Levocarnitine transports long-chain fatty acids across the inner mitochondrial membrane.',sup:'transporting long-chain fatty acids into mitochondria'},{doc:'DOC-0420',ref:'Bölüm 3 · s. 669',quote:'L-carnitine is required for the transfer of long-chain fatty acids into the mitochondrial matrix.',sup:'supports energy metabolism'}],
'C4-TR':[{doc:'DOC-0311',pin:'v4',ref:'Bölüm 5.1 · s. 3',quote:'Levokarnitin, uzun zincirli yağ asitlerinin mitokondriye taşınmasını sağlar.',sup:'uzun zincirli yağ asitlerini mitokondriye taşıyarak'}]};
const USAGE={
C1:{TR:[['İçerik','TUTUKON Gastro Detay Sunumu','tr','v2','Yayında'],['İçerik','TUTUKON Aile Hekimi e-Detay','tr','v1','Yayında'],['İçerik seti','Gastro Ekim Ziyaret Seti','tr','v3','Aktif'],['Etkileşim yolculuğu','Aile hekimi sindirim sağlığı yolculuğu','tr','Adım 1/4','Aktif']]},
C2:{TR:[['İçerik','TUTUKON Şişkinlik Kartı','tr','v1','Yayında'],['İçerik seti','Gastro Ekim Ziyaret Seti','tr','v3','Aktif'],['Etkileşim yolculuğu','Aile hekimi sindirim sağlığı yolculuğu','tr','Adım 3/4','Aktif']]},
C3:{TR:[['İçerik','ALMIBA Nefroloji Detay Sunumu','tr','v3','Yayında'],['İçerik','ALMIBA Hasta Takip e-Detay','tr','v1','Yayında'],['İçerik','Diyaliz Merkezi Slayt Seti','tr','v2','Taslak'],['İçerik seti','Nefroloji Q4 Ziyaret Seti','tr','v2','Aktif'],['Etkileşim yolculuğu','Yeni nefrolog tanışma yolculuğu','tr','Adım 2/5','Aktif']],UZ:[['İçerik','ALMIBA taqdimoti','uz','v1','Yayında'],['İçerik','Презентация ALMIBA','ru','v1','Yayında']],AZ:[['İçerik','ALMIBA Təqdimatı','az','v1','Yayında'],['İçerik seti','Nefrologiya ziyarət dəsti','az','v1','Aktif']]},
C4:{TR:[['İçerik','ALMIBA Etki Mekanizması Animasyonu','tr','v1','Yayında'],['İçerik','ALMIBA Nefroloji Detay Sunumu','tr','v3','Yayında'],['İçerik seti','Nefroloji Q4 Ziyaret Seti','tr','v2','Aktif'],['Etkileşim yolculuğu','Yeni nefrolog tanışma yolculuğu','tr','Adım 3/5','Aktif']]}};
CLAIMS.forEach(c=>{c.usage=Object.values(USAGE[c.id]||{}).reduce((a,x)=>a+x.length,0);});
const VERS={
'C3-TR':[['2.0','approved','18.09.2026','Çekirdek v2.0 · KÜB-TR v5'],['1.0','archived','10.02.2025','Çekirdek v1.0 · KÜB-TR v4']],
'C1-TR':[['1.1','approved','02.03.2026','Niteleyici ifadesi düzeltildi'],['1.0','archived','20.01.2026','İlk sürüm']]};
const CORE_VERS={C3:[['2.0','approved','15.09.2026',"'Used in' → 'Indicated for'; niteleyici eklendi"],['1.0','archived','10.01.2025','İlk sürüm']]};
const C3V1={text:'Used in secondary carnitine deficiency in haemodialysis patients.',quals:['Prescription-only medicine.']};
const DIFF=[['Used in','del'],['Indicated for','ins'],[' secondary carnitine deficiency in haemodialysis patients.','same']];
const PREV={
'DOC-0510':{pages:12,page:3,b:{sec:'5.1. Farmakodinamik xususiyatlari',paras:[["Farmakoterapevtik guruhi: moddalar almashinuviga ta'sir qiluvchi boshqa vositalar. ATX kodi: A16AA01.",0],["Levokarnitin uzun zanjirli yog' kislotalarining mitoxondriya ichki membranasi orqali o'tishini ta'minlaydi.",1],["Ushbu jarayon β-oksidlanish va hujayraning energiya almashinuvi uchun zarurdir.",0]]}},
'DOC-0311':{pages:14,page:3,v4:{sec:'5.1. Farmakodinamik özellikler',paras:[['Farmakoterapötik grup: Diğer sindirim sistemi ve metabolizma ürünleri. ATC kodu: A16AA01.',0],['Levokarnitin, uzun zincirli yağ asitlerinin mitokondriye taşınmasını sağlar.',1],['Bu taşıma, yağ asitlerinin β-oksidasyonu için gereklidir.',0]]},v5:{sec:'5.1. Farmakodinamik özellikler',paras:[['Farmakoterapötik grup: Diğer sindirim sistemi ve metabolizma ürünleri. ATC kodu: A16AA01.',0],['Levokarnitin, uzun zincirli yağ asitlerinin açil-karnitin esterleri hâlinde mitokondri iç zarından geçişini sağlar.',2],['Bu taşıma, yağ asitlerinin β-oksidasyonu için gereklidir.',0],['Hemodiyaliz hastalarında diyaliz sırasında plazma karnitin düzeyi düşer.',2]]}},
'DOC-0312':{pages:9,page:1,b:{sec:'4.1 Therapeutic indications',paras:[['Treatment of secondary carnitine deficiency in patients on haemodialysis.',1],['Treatment of primary systemic carnitine deficiency.',0]]}},
'DOC-0188':{pages:42,page:18,b:{sec:'6.2 Primary endpoint',paras:[['Digestive comfort was assessed with a validated 7-item questionnaire at baseline and week 4.',0],['Participants reported improved digestive comfort scores versus baseline at week 4.',1],['Results are summarised in Table 5.',0]]}},
'DOC-0420':{pages:12,page:669,b:{sec:'3. Metabolic functions',paras:[['L-carnitine is required for the transfer of long-chain fatty acids into the mitochondrial matrix.',1],['This transport is a prerequisite for β-oxidation.',0]]}}};
const GENP={pages:1,page:1,b:{sec:'Belge içeriği',paras:[["Bu belgenin önizlemesi Belge Yönetimi'nden yüklenir.",0]]}};
const MDOCS={uz:['DOC-0510','DOC-0199','DOC-0312','DOC-0420'],core:['DOC-0312','DOC-0420','DOC-0305']};
const SEGS={uz:['Levokarnitin',"uzun zanjirli yog' kislotalarini mitoxondriyalarga tashish orqali","energiya almashinuvini qo'llab-quvvatlaydi."],core:['Helps maintain','plasma carnitine levels','in patients on long-term haemodialysis.']};
const TYPES=['KÜB/KT','Klinik çalışma','Literatür','İç veri','Ruhsat yazısı'];
const TONE={info:['#e8f6fb','#03566b','#b5e6f3'],warn:['#fff6e0','#6b4500','#ffd98a'],danger:['#fff0ed','#8f1f0a','#ffc4b8'],lock:['#eeeefc','#23247f','#c9caf2']};
const SCN=[
['list','1 · İddia listesi',{screen:'list'}],
['matrix','2 · İddia × ülke kapsama matrisi',{screen:'matrix'}],
['core','3 · Yeni çekirdek iddia (taslak)',{screen:'core',role:'global'}],
['ver','4 · Çekirdekten ülke sürümü açma',{screen:'ver',role:'global'}],
['evid','5 · Kanıt ekleme: çekirdek kanıtı + yerel KÜB',{screen:'ver',role:'global',evPre:true}],
['prev','6 · Belge önizleme',{screen:'ver',role:'global',prev:{doc:'DOC-0510',ver:'v1'}}],
['rev','7 · Yerel incelemede ülke sürümü',{screen:'detail',dkey:'C1-UZ',tab:'onay',role:'uz'}],
['chg','8 · Çekirdek değişti, gözden geçir',{screen:'detail',dkey:'C3-UZ',tab:'surum',role:'global'}],
['lock','9 · Onaylı ve kilitli, yeni sürüm aç',{screen:'detail',dkey:'C3-TR',tab:'genel',role:'global'}],
['docu','10 · Kanıt belgesi güncellendi',{screen:'detail',dkey:'C4-TR',tab:'kanit',role:'global'}],
['use','11 · Nerede kullanılıyor',{screen:'detail',dkey:'C3-TR',tab:'kullanim',role:'global'}],
['empty','12 · Boş liste',{screen:'list',emptyLib:true}],
['deny','13 · Yetkisiz kullanıcı',{role:'none'}]];
const btnP={bg:PR,fg:'#fff',bd:PR};const btnS={bg:'#fff',fg:'#384551',bd:'#d9dee3'};

class Component extends DCLogic {
  state={role:'global',scn:'list',screen:'list',dkey:'C3-TR',tab:'genel',panel:null,rowMenu:null,q:'',f:{},flag:null,dd:null,ddq:'',modal:null,mtext:'',ev:null,prev:null,toast:null,vlang:'uz',cmpLang:null,localEv:[],coreEv:[],legal:null,sOvr:{},pinOvr:{},emptyLib:false,colMenu:false,expMenu:false,
    cols:{product:true,type:true,aud:true,ev:true,appr:true,usage:true},
    vf:{uz:"Levokarnitin uzun zanjirli yog' kislotalarini mitoxondriyalarga tashish orqali energiya almashinuvini qo'llab-quvvatlaydi.",ru:'Левокарнитин поддерживает энергетический обмен, транспортируя длинноцепочечные жирные кислоты в митохондрии.',quz:"Retsept bo'yicha beriladi. Faqat kattalar uchun.",qru:'Отпускается по рецепту. Только для взрослых.',adapt:'narrow',reason:"UZ KÜB yalnız yetişkin kullanımını kapsıyor; her iki dilde 'yalnız yetişkinler' niteleyicisi eklendi.",from:'2026-11-01',to:'2027-10-31',aud:true},
    cf:{name:'Plazma karnitin düzeyinin korunması',desc:'',text:'Helps maintain plasma carnitine levels in patients on long-term haemodialysis.',product:'ALMIBA',aud:'Nefroloji',policy:'rx',team:'Nefroloji İş Birimi',comp:null,quals:['Prescription-only medicine.'],qin:'',adv:false}};
  componentDidMount(){
    this._k=e=>{if(e.key!=='Escape')return;const s=this.state;
      if(s.prev)return this.setState({prev:null});if(s.modal)return this.setState({modal:null});if(s.ev)return this.setState({ev:null});
      if(s.dd||s.rowMenu||s.colMenu||s.expMenu)return this.setState({dd:null,rowMenu:null,colMenu:false,expMenu:false});
      if(s.panel)this.setState({panel:null});};
    this._c=e=>{if(e.target.closest&&e.target.closest('[data-pop]'))return;const s=this.state;if(s.dd||s.rowMenu||s.colMenu||s.expMenu)this.setState({dd:null,rowMenu:null,colMenu:false,expMenu:false});};
    document.addEventListener('keydown',this._k);document.addEventListener('mousedown',this._c);
  }
  componentWillUnmount(){document.removeEventListener('keydown',this._k);document.removeEventListener('mousedown',this._c);clearTimeout(this._t);}
  toast(m){this.setState({toast:m});clearTimeout(this._t);this._t=setTimeout(()=>this.setState({toast:null}),4200);}
  roleF(r){return {country:r==='tr'?'TR':r==='uz'?'UZ':null};}
  evInit(target,pre){return pre?{target,q:'',doc:'DOC-0510',type:'KÜB/KT',sec:'5.1 Farmakodinamik xususiyatlari',page:'3',table:'',quote:"Levokarnitin uzun zanjirli yog' kislotalarining mitoxondriya ichki membranasi orqali o'tishini ta'minlaydi.",segs:[1,2]}:{target,q:'',doc:null,type:'',sec:'',page:'',table:'',quote:'',segs:[]};}
  getV(id,cc){const c=CLAIMS.find(x=>x.id===id);let v=c.cs[cc];const o=this.state.sOvr[id+'-'+cc];
    if(id==='C4'&&cc==='UZ'&&o){const f=this.state.vf;v={s:o,v:'1.0',cv:'1.0',texts:[['uz',f.uz],['ru',f.ru]],quals:[['uz',f.quz],['ru',f.qru]],valid:'01.11.2026 – 31.10.2027',adapt:'Daraltıldı',reason:f.reason,note:'Yerel onay bekliyor'};}
    return o?{...v,s:o}:v;}
  chip(k){const t=ST[k];return {l:t.l,i:t.i,bg:t.bg,fg:t.fg,bd:t.bd};}
  evV(e,key){const d=DOCS[e.doc];const pin=(key&&this.state.pinOvr[key])||e.pin||d.ver;const newer=!!(d.cur&&d.cur!==pin);const core=e.origin==='core';
    return {oL:core?'Çekirdekten':'Yerel',oI:core?'bx-globe':'bx-map-pin',oBg:core?'#e7e7fb':'#f3e8ff',oFg:core?PR:'#6b2fa8',title:d.t,id:e.doc,type:d.type,scope:d.scope,pin:pin+' sabitlendi',ref:e.ref,quote:e.quote,sup:e.sup,newer,newerL:newer?`${d.cur} ${d.curDate} tarihinde yayınlandı · sabitlenen ${pin}`:'',exp:!!d.exp,expL:d.exp?`Belge geçerliliği ${d.valid} tarihinde bitiyor`:'',valid:d.valid||'Süresiz',ro:core,onPrev:()=>this.setState({prev:{doc:e.doc,ver:newer?d.cur:pin}})};}
  mkDD(id,label,opts,val,pick,ph,hint){const s=this.state,open=s.dd===id,qq=s.ddq.toLowerCase();
    const o=opts.filter(x=>x[1].toLowerCase().includes(qq)).map(x=>({l:x[1],sel:x[0]===val,bg:x[0]===val?'#e7e7fb':'transparent',fg:x[0]===val?PR:'#384551',on:()=>{pick(x[0]);this.setState({dd:null,ddq:''});}}));
    const cur=opts.find(x=>x[0]===val);
    return {id,label,hint:hint||'',hasHint:!!hint,open,opts:o,empty:o.length===0,q:s.ddq,hasVal:!!cur,valueL:cur?cur[1]:(ph||'Tümü'),vfg:cur?'#384551':'#6b7785',bd:open?PR:(cur&&!ph?PR:'#d9dee3'),
      onQ:e=>this.setState({ddq:e.target.value}),onToggle:()=>this.setState({dd:open?null:id,ddq:''}),onClear:()=>{pick(null);this.setState({dd:null});}};}
  apprFor(c,cc,v,key){const s=this.state,R=ROLES[s.role];const act=k=>({approved:['Onayladı','bx-check-circle','#e5f6dc','#2d7310'],rejected:['Reddetti','bx-x-circle','#ffe2dc','#a8230b'],pending:['Yanıt bekleniyor','bx-time-five','#fff0cc','#7a4e00']})[k];
    const it=(area,who,a,date,cm,extra)=>{const x=act(a);return {area,who,l:x[0],i:x[1],bg:x[2],fg:x[3],date,c:cm||'',hasC:!!cm,isRej:a==='rejected',canAct:false,...(extra||{})};};
    let flow,rounds=[];
    if(key==='C1-UZ'){flow='Paralel · Medikal, Hukuk, Ruhsat';const L=s.legal;
      const canAct=!L&&R.appr==='UZ';
      rounds=[{t:'Tur 1 · v1.0 · 21.09.2026 tarihinde Nodira Yusupova gönderdi',items:[it('Yerel Medikal','Dilnoza Karimova','approved','22.09.2026 14:10','uz ve ru metin çekirdekle uyumlu.'),it('Yerel Ruhsat','Bekzod Tursunov','rejected','23.09.2026 09:42',"Ret gerekçesi: ru niteleyici tescil belgesindeki ifadeyle aynı olmalı: 'БАД. Не является лекарственным средством.'"),
        L?it('Yerel Hukuk','Aziz Rakhimov',L.act,'28.09.2026 · şimdi',(L.act==='rejected'?'Ret gerekçesi: ':'')+L.c):it('Yerel Hukuk','Aziz Rakhimov','pending','Atandı 21.09.2026','',{canAct,onA:()=>this.setState({modal:{kind:'approve',title:'Yerel hukuk onayı',desc:'CLM-TUTUKON-01 · Özbekistan · v1.0',label:'Yorum (isteğe bağlı)',req:false,ok:'Onayla'},mtext:''}),onR:()=>this.setState({modal:{kind:'reject',title:'Reddet',desc:'CLM-TUTUKON-01 · Özbekistan · v1.0. Reddetme gerekçesi zorunludur ve onay geçmişinde görünür.',label:'Ret gerekçesi',req:true,ok:'Reddet',danger:true},mtext:''})})]}];}
    else if(v.ad){const P=PEOPLE[cc];flow=cc==='TR'?'Sıralı · Medikal → Hukuk → Ruhsat':'Paralel · Medikal, Hukuk, Ruhsat';
      rounds=[{t:`Tur 1 · v${v.v}`,items:[it('Yerel Medikal',P[0],'approved',v.ad+' 10:05','Metin ve niteleyiciler uygun.'),it('Yerel Hukuk',P[1],'approved',v.ad+' 13:40',''),it('Yerel Ruhsat',P[2],'approved',v.ad+' 16:20','KÜB ile uyumlu.')]}];}
    else flow=cc==='TR'?'Sıralı · Medikal → Hukuk → Ruhsat':'Paralel · Medikal, Hukuk, Ruhsat';
    const global=c.type==='core'?{t:`Çekirdek v${c.coreV} · Global onay · Sıralı`,items:[it('Global Medikal','Elif Aydın','approved',c.gdate,''),it('Global Hukuk','Can Demir','approved',c.gdate,''),it('Global Ruhsat','Selin Arslan','approved',c.gdate,'')]}:null;
    return {flow,rounds,hasRounds:rounds.length>0,noRounds:rounds.length===0,global,hasGlobal:!!global};}
  renderVals(){
    const s=this.state,R=ROLES[s.role];const up=p=>this.setState(p);
    const go=p=>()=>this.setState({panel:null,rowMenu:null,dd:null,colMenu:false,expMenu:false,...p});
    const canCore=!!R.core,canEd=cc=>R.edit.includes(cc),canLocal=R.edit.length>0;
    const sOf=(id,cc)=>this.getV(id,cc).s;const hasText=(id,cc)=>!!this.getV(id,cc).texts;
    const openD=(id,cc,tab)=>go({screen:'detail',dkey:id+'-'+cc,tab:tab||'genel',cmpLang:null});
    const claims=s.emptyLib?[]:CLAIMS;const f=s.f,q=s.q.trim().toLowerCase();
    const anyS=(c,k)=>CC.some(cc=>sOf(c.id,cc)===k);
    const rows=claims.filter(c=>{
      if(q&&!(c.code+' '+c.name+' '+(c.text||'')+' '+c.product+' '+CC.map(cc=>(c.cs[cc].texts||[]).map(t=>t[1]).join(' ')).join(' ')).toLowerCase().includes(q))return false;
      if(f.product&&c.product!==f.product)return false;if(f.type&&c.type!==f.type)return false;
      if(f.aud&&!c.aud.includes(f.aud))return false;if(f.lang&&!c.langs.includes(f.lang))return false;
      if(f.country&&sOf(c.id,f.country)==='na')return false;
      if(f.status&&!(f.country?[f.country]:CC).some(cc=>sOf(c.id,cc)===f.status))return false;
      if(s.flag==='ev'&&c.ev>0)return false;if(s.flag==='recheck'&&!anyS(c,'recheck'))return false;if(s.flag==='exp'&&!anyS(c,'expiring'))return false;
      return true;});
    const FD=[['product','Ürün',[['TUTUKON','TUTUKON'],['ALMIBA','ALMIBA']]],['country','Ülke',CC.map(c=>[c,CN[c]])],['lang','Dil',Object.keys(LN).map(l=>[l,l+' · '+LN[l]])],
      ['status','Durum',['approved','review','draft','recheck','expiring','closed','none'].map(k=>[k,ST[k].l+(k==='none'?' (karar yok)':'')])],
      ['aud','Kitle',[['Gastroenteroloji','Gastroenteroloji'],['Aile Hekimliği','Aile Hekimliği'],['Nefroloji','Nefroloji']]],['type','Çekirdek / yerel',[['core','Çekirdek'],['local','Yerel']]]];
    const filters=FD.map(([id,l,o])=>this.mkDD(id,l,o,f[id],v=>this.setState(st=>({f:{...st.f,[id]:v}}))));
    const cnt=fn=>claims.filter(fn).length;
    const flags=[['ev','Kanıtı eksik','bx-error-circle',cnt(c=>c.ev===0)],['recheck','Gözden geçirilmeli','bx-revision',cnt(c=>anyS(c,'recheck'))],['exp','Süresi doluyor','bx-alarm-exclamation',cnt(c=>anyS(c,'expiring'))]]
      .map(([id,l,i,n])=>{const a=s.flag===id;return {l,i,n:String(n),act:a,bg:a?PR:'#fff',fg:a?'#fff':'#384551',bd:a?PR:'#d9dee3',nbg:a?'rgba(255,255,255,.2)':'#eceef1',on:()=>up({flag:a?null:id})};});
    const rowsV=rows.map(c=>{const core=c.type==='core';
      const chips=CC.map(cc=>{const k=sOf(c.id,cc),t=ST[k];return {cc,l:t.l,i:t.i,bg:t.bg,fg:t.fg,bd:t.bd,title:CN[cc]+': '+t.l,op:f.country&&f.country!==cc?'.4':'1'};});
      const ap=CC.filter(cc=>['approved','expiring'].includes(sOf(c.id,cc))).length;
      return {id:c.id,code:c.code,name:c.name,product:c.product,sub:core?c.text:c.cs.TR.texts[0][1],typeL:core?'Çekirdek':'Yerel',typeI:core?'bx-globe':'bx-map-pin',typeBg:core?'#e7e7fb':'#f3e8ff',typeFg:core?PR:'#6b2fa8',aud:c.aud.join(', '),chips,ap:ap+' ülke',ev:String(c.ev),evMiss:c.ev===0,evOk:c.ev>0,usage:String(c.usage),
        rowBg:s.panel===c.id?'#f5f5fe':'#fff',onOpen:()=>this.setState({panel:c.id,rowMenu:null}),
        onMenu:e=>{e.stopPropagation();const r=e.currentTarget.getBoundingClientRect();this.setState({rowMenu:s.rowMenu&&s.rowMenu.id===c.id?null:{id:c.id,x:Math.max(8,r.right-230),y:r.bottom+4}});}};});
    let rm=null;
    if(s.rowMenu){const c=CLAIMS.find(x=>x.id===s.rowMenu.id);const first=CC.find(cc=>hasText(c.id,cc))||'TR';
      const items=[{l:'Hızlı görüntüle',i:'bx-show',on:go({panel:c.id})},{l:'Ayrıntıyı aç',i:'bx-link-external',on:openD(c.id,first)},{l:'Nerede kullanılıyor',i:'bx-sitemap',on:openD(c.id,first,'kullanim')}];
      if(c.type==='core'&&canCore)items.push({l:'Çekirdekte yeni sürüm aç',i:'bx-git-branch',on:()=>{this.setState({rowMenu:null});this.toast('Çekirdek düzenleme tam sayfa formda açılır.');}});
      if(c.type==='core'&&CC.some(cc=>canEd(cc)&&sOf(c.id,cc)==='none'))items.push({l:'Ülke sürümü aç',i:'bx-plus-circle',on:go({screen:'ver'})});
      if((c.type==='core'&&canCore)||(c.type==='local'&&canEd('TR')))items.push({l:'Arşivle',i:'bx-archive',danger:true,on:()=>this.setState({rowMenu:null,mtext:'',modal:{kind:'archive',title:'İddiayı arşivle',desc:c.code+'. Silme yoktur; arşivlenen iddia yeni içeriklerde seçilemez, geçmişi korunur.',label:'Arşivleme nedeni',req:true,ok:'Arşivle',danger:true}})});
      rm={x:s.rowMenu.x,y:s.rowMenu.y,items:items.map(it=>({...it,fg:it.danger?'#a8230b':'#384551'}))};}
    let pv=null;
    if(s.panel){const c=CLAIMS.find(x=>x.id===s.panel);const core=c.type==='core';
      pv={code:c.code,name:c.name,product:c.product+(c.product==='TUTUKON'?' · Bitkisel takviye':' · Levokarnitin'),typeL:core?'Çekirdek':'Yerel',typeBg:core?'#e7e7fb':'#f3e8ff',typeFg:core?PR:'#6b2fa8',hasCore:core,text:c.text||'',quals:(c.quals||[]).join(' · '),coreL:core?`Çekirdek v${c.coreV} · Global onaylı · ${c.gdate}`:'Yalnız yerel iddia · çekirdeği yok',aud:c.aud.join(', '),team:c.team,ev:String(c.ev),usage:String(c.usage),
        vers:CC.filter(cc=>sOf(c.id,cc)!=='na').map(cc=>{const v=this.getV(c.id,cc);return {...this.chip(v.s),cn:CN[cc],ver:v.v?'v'+v.v:'',text:v.texts?(v.texts[0][1]||'—'):'',hasText:!!v.texts,note:v.note||'',hasNote:!!v.note,langs:v.texts?v.texts.map(x=>x[0]).join(' · '):'',on:v.texts?openD(c.id,cc):go({screen:'ver'}),canGo:!!v.texts||(v.s==='none'&&canEd(cc)),goL:v.texts?'Aç':'Sürüm aç'};}),
        onClose:()=>up({panel:null}),onFull:openD(c.id,CC.find(cc=>hasText(c.id,cc)))};}
    const mcolsCC=f.country?[f.country]:CC;
    const mcols=mcolsCC.map(cc=>{const ks=rows.map(c=>sOf(c.id,cc));return {cn:CN[cc],langs:CL[cc].join(', '),ok:ks.filter(k=>k==='approved'||k==='expiring').length+' onaylı',warn:ks.filter(k=>['recheck','expiring','review','draft','none'].includes(k)).length+' açık iş'};});
    const mrows=rowsV.map(r=>{const c=CLAIMS.find(x=>x.id===r.id);return {...r,cells:mcolsCC.map(cc=>{const v=this.getV(c.id,cc),t=ST[v.s];const opn=!!v.texts,canOpen=v.s==='none'&&canEd(cc);
      return {l:v.s==='na'?'—':t.l,i:t.i,bg:t.bg,fg:t.fg,bd:t.bd,ver:v.v?'v'+v.v:'',sub:v.note||(v.cv?'Çekirdek v'+v.cv:'Yalnız yerel'),opn,canOpen,noAct:!opn&&!canOpen,on:opn?openD(c.id,cc):go({screen:'ver'}),aria:`${c.code}, ${CN[cc]}: ${t.l}${v.note?', '+v.note:''}`};})};});
    const legend=['approved','review','draft','recheck','expiring','closed','none'].map(k=>({...this.chip(k),l:k==='none'?'Açılmadı · karar yok':k==='closed'?'Açılmadı · nedeni ile':ST[k].l}));
    // detail
    let D=null;
    if(s.screen==='detail'){const [id,cc]=s.dkey.split('-');const c=CLAIMS.find(x=>x.id===id);const v=this.getV(id,cc);const key=s.dkey;const edit=canEd(cc);
      const locked=v.s==='approved'||v.s==='expiring';
      const evs=[...(EV[id]||[]).map(e=>({...e,origin:'core'})),...((key==='C4-UZ'?s.localEv:EV[key])||[]).map(e=>({...e,origin:'local'}))].map(e=>this.evV(e,e.origin==='local'?key:null));
      const use=USAGE[id]||{};const visCs=R.own?R.own:CC;
      const usage=[cc,...CC.filter(x=>x!==cc)].filter(x=>use[x]&&visCs.includes(x)).map(x=>{const vv=this.getV(id,x);return {...this.chip(vv.s),cn:CN[x],ver:'v'+vv.v,count:use[x].length+' kullanım',
        items:use[x].map(([ty,n,lg,vr,st])=>({ty,n,lg,vr,st,ic:ty==='İçerik'?'bx-file':ty==='İçerik seti'?'bx-collection':'bx-git-branch',on:()=>this.toast(ty+' kaydı Bilgi İçerikleri / Content Studio ekranında açılır.')})),
        warn:vv.s==='recheck'?'Ülke sürümü gözden geçirilmeli. İçerikler son onaylı sürümü kullanmaya devam eder.':vv.s==='expiring'?"Ülke sürümü 31.10.2026'da sona eriyor; bu içerikler etkilenecek.":'',hasWarn:vv.s==='recheck'||vv.s==='expiring'};});
      const useN=usage.reduce((a,g)=>a+g.items.length,0);const hiddenUse=!!R.own&&CC.some(x=>use[x]&&!visCs.includes(x));
      const tabs=[['genel','Genel'],['kanit','Kanıtlar',evs.length],['onay','Onay'],['kullanim','Nerede kullanılıyor',useN],['surum','Sürümler ve karşılaştırma']].map(([tid,l,n])=>({l,n:n==null?'':String(n),hasN:n!=null,act:s.tab===tid,on:()=>up({tab:tid}),fg:s.tab===tid?PR:'#646e78',bd:s.tab===tid?PR:'transparent'}));
      const mod=(kind,title,desc,label,req,ok,danger)=>()=>this.setState({mtext:'',modal:{kind,title,desc,label,req,ok,danger}});
      const nextV=v.v?(Math.floor(parseFloat(v.v))+'.'+(Math.round((parseFloat(v.v)%1)*10)+1)):'';
      let bn=null;const A=(l,on,p)=>({l,on,...(p?btnP:btnS)});
      if(key==='C1-UZ'&&v.s==='review')bn={tone:'info',i:'bx-info-circle',t:'Yerel incelemede · 1 onay bekliyor, 1 ret',x:'Medikal onayladı, ruhsat reddetti, hukuk yanıtı bekleniyor. Tüm yanıtlar gelince ret nedeniyle sürüm Taslak durumuna döner.',acts:[A('Onay ayrıntısı',()=>up({tab:'onay'}))]};
      else if(v.s==='recheck'&&id==='C3')bn={tone:'warn',i:'bx-revision',t:'Çekirdek değişti, gözden geçir',x:"Çekirdek v2.0 15.09.2026'da onaylandı. Bu ülke sürümü hâlâ çekirdek v1.0'a bağlı. Yeni çekirdeğe uyarlayın ya da gerekçesiyle mevcut hali koruyun.",acts:[A('Farkı gör',()=>up({tab:'surum'})),...(edit?[A('Mevcut hali koru',mod('keep','Mevcut hali koru','CLM-ALMIBA-01 · Özbekistan v1.0 metni değişmeden çekirdek v2.0’a bağlanır ve yerel onaya gönderilir.','Koruma gerekçesi',true,'Gerekçeyle koru')),A('Yeni çekirdeğe uyarla',mod('adapt','Yeni çekirdeğe uyarla','v1.1 taslağı çekirdek v2.0 metni ve niteleyicileriyle açılır. Onaylı v1.0 yenisi onaylanana kadar kullanımda kalır.','Değişiklik notu (isteğe bağlı)',false,'v1.1 taslağını aç'),true)]:[])]};
      else if(v.s==='recheck'&&id==='C4')bn={tone:'warn',i:'bx-file',t:'Kanıt belgesi güncellendi',x:"ALMIBA KÜB (Türkiye) v5 16.09.2026'da yayınlandı; bu sürümün kanıtı v4'e sabit. Bölüm 5.1 metni değişti.",acts:[A('Değişikliği önizle',()=>up({prev:{doc:'DOC-0311',ver:'v5'}})),...(edit?[A("Etkilemiyor, v5'e sabitle",mod('dockeep',"Metni değiştirmeden v5'e sabitle",'İddia metni değişmez; kanıt KÜB-TR v5 Bölüm 5.1’e sabitlenir. Gerekçe onay geçmişine yazılır.','Gerekçe',true,"v5'e sabitle")),A('Yeni sürüm aç',mod('docnew','Yeni sürüm aç','v1.1 taslağı KÜB-TR v5 sabitlenmiş olarak açılır. Onaylı v1.0 yenisi onaylanana kadar kullanımda kalır.','Değişiklik notu (isteğe bağlı)',false,'v1.1 taslağını aç'),true)]:[])]};
      else if(v.s==='expiring')bn={tone:'danger',i:'bx-alarm-exclamation',t:'Süresi doluyor',x:"Geçerlilik 31.10.2026'da bitiyor (33 gün kaldı). Bitişten sonra bu sürüm içeriklerde seçilemez.",acts:edit?[A('Yeni sürüm aç',mod('newver','Yeni sürüm aç',`v${nextV} taslağı açılır; onaylı v${v.v} yenisi onaylanana kadar kullanımda kalır.`,'Değişiklik nedeni',true,'Taslağı aç'),true)]:[]};
      else if(locked)bn={tone:'lock',i:'bx-lock-alt',t:'Onaylı sürüm kilitli',x:'Metin, niteleyiciler, kapsam ve kanıtlar değiştirilemez. Değişiklik için yeni sürüm açın; onaylı sürüm yenisi onaylanana kadar kullanımda kalır.',acts:edit?[A('Yeni sürüm aç',mod('newver','Yeni sürüm aç',`${c.code} · ${CN[cc]}: v${nextV} taslağı v${v.v} içeriğiyle açılır. Onaylı v${v.v} yenisi onaylanana kadar kullanımda kalır.`,'Değişiklik nedeni',true,`v${nextV} taslağını aç`),true)]:[]};
      else if(v.s==='draft')bn={tone:'info',i:'bx-edit-alt',t:'Taslak',x:evs.length===0?'Onaya göndermek için en az 1 kanıt ekleyin.':(v.note?v.note+'. ':'')+'Taslak yetkili yerel ekip tarafından düzenlenebilir.',acts:edit?[A('Düzenle',()=>this.toast('Düzenleme tam sayfa formda açılır.')),...(evs.length?[]:[A('Kanıt ekle',()=>up({tab:'kanit'}),true)])]:[]};
      else if(v.s==='review')bn={tone:'info',i:'bx-time-five',t:'İncelemede',x:'Yerel onay bekleniyor. İncelemedeki sürüm düzenlenemez.',acts:[]};
      const tone=bn?TONE[bn.tone]:TONE.info;
      const cl=s.cmpLang&&v.texts.find(x=>x[0]===s.cmpLang)?s.cmpLang:v.texts[0][0];
      const bound1=id==='C3'&&v.cv==='1.0';
      const vlist=(VERS[key]||[[v.v,v.s,v.ad||'—',c.type==='core'?'Çekirdek v'+v.cv:'Yalnız yerel']]).map(([vv,st,d,n])=>({v:'v'+vv,...this.chip(st),d,n}));
      const cvlist=(CORE_VERS[id]||(c.type==='core'?[[c.coreV,'approved',c.gdate,'İlk sürüm']]:[])).map(([vv,st,d,n])=>({v:'v'+vv,...this.chip(st),d,n}));
      D={code:c.code,cn:CN[cc],name:c.name,ver:'v'+v.v,...this.chip(v.s),locked,isCore:c.type==='core',isLocal:c.type==='local',coreRef:c.type==='core'?`Çekirdek v${v.cv}'e bağlı`:'Yalnız yerel iddia',
        texts:v.texts.map(([l,tx])=>({lang:l,ln:LN[l],text:tx||'Metin girilmedi',empty:!tx,has:!!tx,len:(tx||'').length+' karakter'})),
        coreText:c.text||'',coreV:'v'+c.coreV,coreQ:(c.quals||[]).join(' · '),quals:(v.quals||[]).map(([l,x])=>({l,x})),hasQuals:(v.quals||[]).length>0,adapt:v.adapt,reason:v.reason||'Birebir çeviri; uyarlama yok.',
        facts:[['Ürün',c.product+(c.product==='TUTUKON'?' · Bitkisel takviye':' · Levokarnitin')],['Ruhsat sahibi (bilgi)',HOLDER[cc]],['Ülke dilleri',CL[cc].join(', ')],['Hedef kitle',c.aud.join(', ')],['Uygunluk politikası',c.product==='ALMIBA'?'Reçeteli ürün – yalnız hekim':'Takviye – sağlık meslek mensubu'],['Geçerlilik',v.valid||'—'],['Sorumlu ekip',c.team]].map(([k,x])=>({k,x})),
        others:CC.filter(x=>x!==cc&&sOf(id,x)!=='na').map(x=>{const vv=this.getV(id,x);return {...this.chip(vv.s),cn:CN[x],ver:vv.v?'v'+vv.v:'',note:vv.note||'',can:!!vv.texts,cant:!vv.texts,on:openD(id,x)};}),hasOthers:c.type==='core',
        tabs,tG:s.tab==='genel',tK:s.tab==='kanit',tO:s.tab==='onay',tU:s.tab==='kullanim',tS:s.tab==='surum',
        bn,hasBn:!!bn,bnBg:tone[0],bnFg:tone[1],bnBd:tone[2],
        evs,hasEv:evs.length>0,noEv:evs.length===0,canAddEv:edit&&!locked&&v.s!=='review',onAddEv:key==='C4-UZ'?()=>up({ev:this.evInit('uz')}):()=>this.toast('Kanıt ekleme penceresi bu sürüm için açılır (prototipte ALMIBA-02 · UZ akışı etkin).'),
        ap:this.apprFor(c,cc,v,key),usage,hasUse:usage.length>0,noUse:usage.length===0,hiddenUse,
        cmpLangs:v.texts.map(([l])=>({l,act:l===cl,bg:l===cl?PR:'#fff',fg:l===cl?'#fff':'#384551',on:()=>up({cmpLang:l})})),cmpText:(v.texts.find(x=>x[0]===cl)||[,''])[1]||'Metin girilmedi',cmpL:cl,
        cmpCoreV:'v'+(v.cv||''),cmpCore:bound1?C3V1.text:(c.text||''),cmpCoreQ:(bound1?C3V1.quals:(c.quals||[])).join(' · '),cmpQ:(v.quals||[]).filter(x=>x[0]===cl).map(x=>x[1]).join(' · ')||'—',
        isChg:key==='C3-UZ'&&v.s==='recheck',diff:DIFF.map(([t,k])=>({t,del:k==='del',ins:k==='ins',same:k==='same'})),
        vlist,cvlist,hasCv:cvlist.length>0,
        onBack:go({screen:'list'}),onMatrix:go({screen:'matrix'})};}
    // core-new
    const cf=s.cf;const setCf=p=>this.setState(st=>({cf:{...st.cf,...p}}));
    const cfCode=cf.product==='TUTUKON'?'CLM-TUTUKON-04':'CLM-ALMIBA-03';
    const cfReq=[['Kod',true],['İddia adı',!!cf.name.trim()],['İddia metni',!!cf.text.trim()],['Ürün',!!cf.product],['Hedef kitle',!!cf.aud],['En az 1 kanıt',s.coreEv.length>0]];
    const cfOk=cfReq.every(x=>x[1]);
    const cfSel=[this.mkDD('cf-product','Ürün *',[['ALMIBA','ALMIBA · Levokarnitin'],['TUTUKON','TUTUKON · Bitkisel takviye']],cf.product,v=>setCf({product:v}),'Ürün seçin','Ürün ana kaydından. Kod ürüne göre önerilir.'),
      this.mkDD('cf-aud','Hedef kitle *',[['Nefroloji','Nefroloji'],['Gastroenteroloji','Gastroenteroloji'],['Aile Hekimliği','Aile Hekimliği']],cf.aud,v=>setCf({aud:v}),'Kitle seçin','Tanımlı kitle profillerinden.'),
      this.mkDD('cf-policy','Uygunluk politikası',[['rx','Reçeteli ürün – yalnız hekim'],['sup','Takviye – sağlık meslek mensubu']],cf.policy,v=>setCf({policy:v}),'İsteğe bağlı','İddianın kime söylenebileceğini belirler.'),
      this.mkDD('cf-team','Sorumlu ekip',[['Nefroloji İş Birimi','Nefroloji İş Birimi'],['Gastro İş Birimi','Gastro İş Birimi'],['Global Medikal','Global Medikal']],cf.team,v=>setCf({team:v}),'İsteğe bağlı','Filtreleme ve inceleme yönlendirmesi içindir; görünürlüğü sınırlamaz.')];
    const cfAdv=[this.mkDD('cf-comp','Bileşenler',[['c1','ALMIBA mekanizma animasyonu'],['c2','Nefroloji referans slaytı'],['c3','Diyaliz hasta profili kartı']],cf.comp,v=>setCf({comp:v}),'Bileşenleri seçin…','Kimlikle referans verilen yeniden kullanılabilir bilgi içeriği (yalnız yeniden kullanım; yeni bileşen oluşturulmaz).')];
    // version-new
    const vf=s.vf;const setVf=p=>this.setState(st=>({vf:{...st.vf,...p}}));const reasonReq=vf.adapt!=='same';
    const vReq=[['Çekirdek onaylı (v1.0)',true],['uz metin',!!vf.uz.trim()],['ru metin',!!vf.ru.trim()],['Uyarlama nedeni',!reasonReq||!!vf.reason.trim()],['Geçerlilik başlangıcı',!!vf.from],['En az 1 kanıt',true]];
    const vOk=vReq.every(x=>x[1]);
    const vCoreEv=EV.C4.map(e=>this.evV({...e,origin:'core'}));const vLocEv=s.localEv.map(e=>this.evV({...e,origin:'local'}));
    // evidence modal
    let EM=null;
    if(s.ev){const e=s.ev;const setE=p=>this.setState(st=>({ev:{...st.ev,...p}}));const bound=e.target==='uz'?['DOC-0312','DOC-0420']:[];const added=(e.target==='uz'?s.localEv:s.coreEv).map(x=>x.doc);
      const docs=MDOCS[e.target].filter(id=>!e.q||(id+DOCS[id].t).toLowerCase().includes(e.q.toLowerCase())).map(id=>{const d=DOCS[id];const b=bound.includes(id),a=added.includes(id);const sel=e.doc===id;
        return {id,t:d.t,type:d.type,scope:d.scope,ver:d.cur||d.ver,valid:d.valid?'Geçerlilik '+d.valid:'Süresiz',exp:!!d.exp,dis:b||a,bL:b?'Çekirdekte bağlı':'Eklendi',sel,bd:sel?PR:'#e4e6e8',bg:sel?'#f5f5fe':(b||a?'#fafafb':'#fff'),on:()=>{if(!(b||a))setE({doc:id,type:d.type});}};});
      const segs=SEGS[e.target].map((tx,i)=>{const on=e.segs.includes(i);return {tx,on,bg:on?'#fff3b0':'#fff',bd:on?'#e0b400':'#d9dee3',on2:()=>setE({segs:on?e.segs.filter(x=>x!==i):[...e.segs,i]})};});
      const ok=!!e.doc&&!!(e.sec.trim()||e.page.trim())&&!!e.quote.trim()&&e.segs.length>0;const d=e.doc?DOCS[e.doc]:null;
      EM={title:e.target==='uz'?'Yerel kanıt ekle · CLM-ALMIBA-02 · Özbekistan':'Kanıt ekle · Yeni çekirdek iddia',ctx:e.target==='uz'?'ALMIBA · Özbekistan ve Global belgeler':'ALMIBA · tüm ülkeler',q:e.q,onQ:ev=>setE({q:ev.target.value}),docs,noDocs:docs.length===0,
        hasDoc:!!e.doc,noDoc:!e.doc,dT:d?d.t:'',dId:e.doc||'',dVer:d?(d.cur||d.ver):'',dScope:d?d.scope:'',
        types:TYPES.map(ty=>({ty,sel:e.type===ty,bg:e.type===ty?PR:'#fff',fg:e.type===ty?'#fff':'#384551',bd:e.type===ty?PR:'#d9dee3',on:()=>setE({type:ty})})),
        sec:e.sec,page:e.page,table:e.table,quote:e.quote,onSec:ev=>setE({sec:ev.target.value}),onPage:ev=>setE({page:ev.target.value}),onTable:ev=>setE({table:ev.target.value}),onQuote:ev=>setE({quote:ev.target.value}),
        segs,segL:e.target==='uz'?'uz metin':'en çekirdek metin',ok,okBg:ok?PR:'#b9bbd9',
        onPrev:()=>{if(e.doc)this.setState({prev:{doc:e.doc,ver:d.cur||d.ver}});},onUpload:()=>this.toast("Belge Yönetimi'nin yükleme penceresi açılır; yeni belge kontrollü kayıt olarak eklenir."),
        onClose:()=>up({ev:null}),
        onAdd:()=>{if(!ok)return;const rec={doc:e.doc,ref:[e.sec&&('Bölüm '+e.sec),e.page&&('s. '+e.page),e.table&&('Tablo '+e.table)].filter(Boolean).join(' · '),quote:e.quote,sup:[...e.segs].sort().map(i=>SEGS[e.target][i]).join(' … ')};
          if(e.target==='uz')this.setState(st=>({localEv:[...st.localEv,rec],ev:null}));else this.setState(st=>({coreEv:[...st.coreEv,rec],ev:null}));this.toast(`Kanıt eklendi; ${e.doc} ${d.cur||d.ver} sabitlendi.`);}};}
    // preview
    let PV=null;
    if(s.prev){const d=DOCS[s.prev.doc];const P=PREV[s.prev.doc]||GENP;const body=P.v4?(P[s.prev.ver]||P.v5):P.b;
      PV={title:d.t,id:s.prev.doc,ver:s.prev.ver,type:d.type,scope:d.scope,valid:d.valid||'Süresiz',status:'Yürürlükte',exp:!!d.exp,sec:body.sec,page:`Sayfa ${P.page} / ${P.pages}`,
        paras:body.paras.map(([t,h])=>({t,q:h===1,ch:h===2,pl:h===0})),hasVers:!!P.v4,
        vers:['v4','v5'].map(x=>({l:x==='v4'?'v4 · sabitlenen':'v5 · güncel',bg:s.prev.ver===x?PR:'#fff',fg:s.prev.ver===x?'#fff':'#384551',on:()=>this.setState(st=>({prev:{...st.prev,ver:x}}))})),
        onClose:()=>up({prev:null}),onDM:()=>this.toast("Belge Yönetimi'nde belge kaydı yeni sekmede açılır.")};}
    const M=s.modal;const mOk=M?(!M.req||!!s.mtext.trim()):false;
    return {
      role:s.role,scn:s.scn,roleOpts:Object.keys(ROLES).map(k=>({v:k,l:ROLES[k].n})),scnOpts:SCN.map(x=>({v:x[0],l:x[1]})),
      onRole:e=>{const r=e.target.value,RR=ROLES[r];let screen=s.screen;if((screen==='core'&&!RR.core)||(screen==='ver'&&!RR.edit.includes('UZ')))screen='list';this.setState({role:r,f:this.roleF(r),screen,panel:null,modal:null,ev:null,prev:null,rowMenu:null});},
      onScn:e=>{const sc=SCN.find(x=>x[0]===e.target.value);const p=sc[2];const role=p.role||s.role;
        this.setState({scn:sc[0],panel:null,rowMenu:null,modal:null,prev:null,emptyLib:false,flag:null,q:'',dd:null,tab:'genel',f:this.roleF(role),role,...p,ev:p.evPre?this.evInit('uz',true):null});},
      denied:s.role==='none',allowed:s.role!=='none',onHome:()=>this.toast('ERP ana sayfasına yönlendirilir.'),
      isLM:s.screen==='list'||s.screen==='matrix',isList:s.screen==='list',isMatrix:s.screen==='matrix',isCore:s.screen==='core'&&canCore,isVer:s.screen==='ver'&&canEd('UZ'),isDetail:s.screen==='detail',
      tabList:go({screen:'list'}),tabMatrix:go({screen:'matrix'}),
      tlBg:s.screen==='list'?PR:'transparent',tlFg:s.screen==='list'?'#fff':'#566a7f',tmBg:s.screen==='matrix'?PR:'transparent',tmFg:s.screen==='matrix'?'#fff':'#566a7f',
      canCore,canLocal,canLocalTR:canEd('TR')&&!canCore,
      onNewCore:go({screen:'core'}),onNewLocal:()=>this.toast('Yeni yerel iddia formu tam sayfa açılır; ülke, yetkili olduğunuz ülkelerle sınırlıdır.'),
      localNote:R.own?`Varsayılan olarak kendi ülkeniz (${R.own.map(x=>CN[x]).join(', ')}) gösteriliyor. Çekirdek iddialar salt okunur.`:'',hasLocalNote:!!R.own,roNote:s.role==='ro',
      q:s.q,onQ:e=>up({q:e.target.value}),filters,flags,
      listGrid:['minmax(240px,2.4fr)',s.cols.product&&'110px',s.cols.type&&'120px',s.cols.aud&&'minmax(130px,1fr)','200px',s.cols.ev&&'96px',s.cols.appr&&'84px',s.cols.usage&&'88px','56px'].filter(Boolean).join(' '),
      mGrid:`minmax(240px,1.3fr) repeat(${mcolsCC.length},minmax(170px,1fr))`,
      cols:s.cols,colMenu:s.colMenu,onCols:()=>up({colMenu:!s.colMenu,expMenu:false}),colOpts:[['product','Ürün'],['type','Tür'],['aud','Kitle'],['ev','Kanıt'],['appr','Onaylı ülke'],['usage','Kullanım']].map(([k,l])=>({l,on:s.cols[k],tg:()=>this.setState(st=>({cols:{...st.cols,[k]:!st.cols[k]}}))})),
      expMenu:s.expMenu,onExp:()=>up({expMenu:!s.expMenu,colMenu:false}),expOpts:['Excel (.xlsx)','CSV','PDF'].map(l=>({l,on:()=>{this.setState({expMenu:false});this.toast(`${rows.length} iddia ${l} olarak dışa aktarılıyor.`);}})),
      rows:rowsV,rowsN:rows.length+' iddia',hasRows:rows.length>0,noRows:rows.length===0,emptyLib:rows.length===0&&s.emptyLib,emptyFilt:rows.length===0&&!s.emptyLib,
      onClearAll:()=>up({q:'',f:{},flag:null}),onExitEmpty:()=>up({emptyLib:false,scn:'list'}),
      rm,hasRm:!!rm,pv,hasPv:!!pv,mcols,mrows,legend,D,
      cf,cfCode,cfReqN:`${cfReq.filter(x=>x[1]).length} / ${cfReq.length}`,cfReq:cfReq.map(([l,ok])=>({l,ok,no:!ok})),cfOk,cfNo:!cfOk,cfSel,cfAdv,cfSubBg:cfOk?PR:'#b9bbd9',
      onCfName:e=>setCf({name:e.target.value}),onCfDesc:e=>setCf({desc:e.target.value}),onCfText:e=>setCf({text:e.target.value}),onCfQin:e=>setCf({qin:e.target.value}),
      onCfQadd:()=>{if(cf.qin.trim())setCf({quals:[...cf.quals,cf.qin.trim()],qin:''});},onCfQkey:e=>{if(e.key==='Enter'){e.preventDefault();if(cf.qin.trim())setCf({quals:[...cf.quals,cf.qin.trim()],qin:''});}},
      cfQuals:cf.quals.map((x,i)=>({x,rm:()=>setCf({quals:cf.quals.filter((_,j)=>j!==i)})})),cfTextLen:cf.text.length+' karakter',
      cfAdvOpen:cf.adv,onCfAdv:()=>setCf({adv:!cf.adv}),cfAdvI:cf.adv?'bx-chevron-up':'bx-chevron-down',
      cfEv:s.coreEv.map(e=>this.evV({...e,origin:'core'})),cfHasEv:s.coreEv.length>0,cfNoEv:s.coreEv.length===0,
      onCfEv:()=>up({ev:this.evInit('core')}),onCancel:go({screen:'list'}),onSaveDraft:()=>this.toast('Taslak kaydedildi.'),
      onCfSubmit:()=>{if(!cfOk)return;this.toast('Global onaya gönderildi: Medikal → Hukuk → Ruhsat.');go({screen:'list'})();},
      vf,vReq:vReq.map(([l,ok])=>({l,ok,no:!ok})),vReqN:`${vReq.filter(x=>x[1]).length} / ${vReq.length}`,vOk,vSubBg:vOk?PR:'#b9bbd9',
      vLangs:['uz','ru'].map(l=>({l,ln:LN[l],act:s.vlang===l,done:!!vf[l].trim(),miss:!vf[l].trim(),bd:s.vlang===l?PR:'transparent',fg:s.vlang===l?PR:'#646e78',on:()=>up({vlang:l})})),
      vText:vf[s.vlang],vQual:vf['q'+s.vlang],vLangL:s.vlang+' · '+LN[s.vlang],vLen:vf[s.vlang].length+' karakter',
      onVText:e=>setVf({[s.vlang]:e.target.value}),onVQual:e=>setVf({['q'+s.vlang]:e.target.value}),
      vAdapt:[['same','Birebir çeviri','Anlam çekirdekle aynı.'],['narrow','Daraltıldı','Kapsam, kitle ya da ifade çekirdekten dar.'],['soft','Yumuşatıldı','İfade çekirdekten daha temkinli.']].map(([k,l,d])=>({l,d,sel:vf.adapt===k,bd:vf.adapt===k?PR:'#d9dee3',bg:vf.adapt===k?'#f5f5fe':'#fff',ri:vf.adapt===k?'bx-radio-circle-marked':'bx-radio-circle',rc:vf.adapt===k?PR:'#8592a3',on:()=>setVf({adapt:k})})),
      reasonReq,reasonL:reasonReq?'Uyarlama nedeni *':'Uyarlama nedeni (isteğe bağlı)',onVReason:e=>setVf({reason:e.target.value}),onVFrom:e=>setVf({from:e.target.value}),onVTo:e=>setVf({to:e.target.value}),onVAud:e=>setVf({aud:e.target.checked}),
      vCoreEv,vLocEv,vHasLoc:vLocEv.length>0,vNoLoc:vLocEv.length===0,onVEv:()=>up({ev:this.evInit('uz')}),
      vOthers:CC.filter(x=>x!=='UZ').map(x=>({...this.chip(sOf('C4',x)),cn:CN[x],note:this.getV('C4',x).note||''})),
      onVSubmit:()=>{if(!vOk)return;this.setState(st=>({sOvr:{...st.sOvr,'C4-UZ':'review'},screen:'matrix',scn:'matrix',f:this.roleF(st.role)}));this.toast('Özbekistan sürümü yerel onaya gönderildi (paralel: Medikal, Hukuk, Ruhsat).');},
      EM,hasEM:!!EM,PV,hasPV:!!PV,
      M,hasM:!!M,mtext:s.mtext,onMtext:e=>up({mtext:e.target.value}),mOk,mBg:M?(mOk?(M.danger?'#c7300f':PR):'#c3c6d3'):PR,onMclose:()=>up({modal:null}),
      onMok:()=>{if(!mOk)return;const k=M.kind,t=s.mtext.trim();const so=(key,val)=>this.setState(st=>({sOvr:{...st.sOvr,[key]:val}}));this.setState({modal:null});
        if(k==='approve'){this.setState({legal:{act:'approved',c:t}});so('C1-UZ','draft');this.toast('Onayınız kaydedildi. Ruhsat reddi nedeniyle sürüm Taslak durumuna döndü.');}
        else if(k==='reject'){this.setState({legal:{act:'rejected',c:t}});so('C1-UZ','draft');this.toast('Ret gerekçesi kaydedildi; sürüm Taslak durumuna döndü.');}
        else if(k==='keep'){so('C3-UZ','review');this.toast('Gerekçe kaydedildi; mevcut metin çekirdek v2.0 ile yerel onaya gönderildi.');}
        else if(k==='adapt'){so('C3-UZ','draft');this.toast("v1.1 taslağı açıldı ve çekirdek v2.0'a bağlandı.");}
        else if(k==='dockeep'){so('C4-TR','approved');this.setState(st=>({pinOvr:{...st.pinOvr,'C4-TR':'v5'}}));this.toast("Kanıt v5'e sabitlendi; metin değişmedi.");}
        else if(k==='docnew'){this.toast('v1.1 taslağı açıldı; KÜB-TR v5 sabitlenecek.');}
        else if(k==='newver'){this.toast('Yeni sürüm taslağı açıldı. Onaylı sürüm kullanımda kalır.');}
        else if(k==='archive'){this.toast('İddia arşivlendi. Geçmiş ve kullanım kayıtları korunur.');}},
      toast:s.toast,hasToast:!!s.toast};
  }
}
