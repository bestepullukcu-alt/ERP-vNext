/**
 * Config
 * -------------------------------------------------------------------------------------
 * ! IMPORTANT: Make sure you clear the browser local storage In order to see the config changes in the template.
 * ! To clear local storage: (https://www.leadshook.com/help/how-to-clear-local-storage-in-google-chrome-browser/).
 */

'use strict';
/* JS global variables
 !Please use the hex color code (#000) here. Don't use rgba(), hsl(), etc
*/
window.config = {
  colors: {
    primary: window.Helpers.getCssVar('primary'),
    secondary: window.Helpers.getCssVar('secondary'),
    success: window.Helpers.getCssVar('success'),
    info: window.Helpers.getCssVar('info'),
    warning: window.Helpers.getCssVar('warning'),
    danger: window.Helpers.getCssVar('danger'),
    dark: window.Helpers.getCssVar('dark'),
    black: window.Helpers.getCssVar('pure-black'),
    white: window.Helpers.getCssVar('white'),
    cardColor: window.Helpers.getCssVar('paper-bg'),
    bodyBg: window.Helpers.getCssVar('body-bg'),
    bodyColor: window.Helpers.getCssVar('body-color'),
    headingColor: window.Helpers.getCssVar('heading-color'),
    textMuted: window.Helpers.getCssVar('secondary-color'),
    borderColor: window.Helpers.getCssVar('border-color')
  },
  colors_label: {
    primary: window.Helpers.getCssVar('primary-bg-subtle'),
    secondary: window.Helpers.getCssVar('secondary-bg-subtle'),
    success: window.Helpers.getCssVar('success-bg-subtle'),
    info: window.Helpers.getCssVar('info-bg-subtle'),
    warning: window.Helpers.getCssVar('warning-bg-subtle'),
    danger: window.Helpers.getCssVar('danger-bg-subtle'),
    dark: window.Helpers.getCssVar('dark-bg-subtle')
  },
  fontFamily: window.Helpers.getCssVar('font-family-base'),
  enableMenuLocalStorage: true
};

window.assetsPath = document.documentElement.getAttribute('data-assets-path');
window.templateName = document.documentElement.getAttribute('data-template');

if (typeof TemplateCustomizer !== 'undefined') {  // MOD-0013: 7 Language Support for Template Customizer
  TemplateCustomizer.LANGUAGES.en = {
    panel_header: 'Template Customizer',
    panel_sub_header: 'Customize and preview in real time',
    theming_header: 'Theming',
    color_label: 'Primary Color',
    theme_label: 'Theme',
    skin_label: 'Skins',
    semiDark_label: 'Semi Dark',
    layout_header: 'Layout',
    layout_label: 'Menu (Navigation)',
    layout_header_label: 'Header Types',
    content_label: 'Content',
    layout_navbar_label: 'Navbar Type',
    direction_label: 'Direction',
    style_switch_light: 'Light',
    style_switch_dark: 'Dark',
    layout_static: 'Static',
    layout_offcanvas: 'Offcanvas',
    layout_fixed: 'Fixed',
    layout_fixed_offcanvas: 'Fixed Offcanvas',
    layout_dd_open_label: 'Open on hover',
    layout_footer_label: 'Fixed Footer',
    misc_header: 'Misc',
    theme_light: 'Light',
    theme_dark: 'Dark',
    theme_system: 'System',
    skin_default: 'Default',
    skin_bordered: 'Bordered',
    layout_expanded: 'Expanded',
    layout_collapsed: 'Collapsed',
    content_compact: 'Compact',
    content_wide: 'Wide',
    direction_ltr: 'Left to Right',
    direction_rtl: 'Right to Left',
    navbar_sticky: 'Sticky',
    navbar_static: 'Static',
    navbar_hidden: 'Hidden'
  };

  TemplateCustomizer.LANGUAGES.tr = {
    panel_header: 'Tema Ozellestirici',
    panel_sub_header: 'Gercek zamanli duzenleyin ve gorun',
    theming_header: 'Temalar',
    color_label: 'Ana Renk',
    theme_label: 'Gorunum',
    skin_label: 'Stiller',
    semiDark_label: 'Yari Karanlik Menu',
    layout_header: 'Yerlesim',
    layout_label: 'Menu (Gezinti)',
    layout_header_label: 'Baslik Tipleri',
    content_label: 'Icerik Genisligi',
    layout_navbar_label: 'Navigasyon Cubugu',
    direction_label: 'Yon',
    style_switch_light: 'Acik',
    style_switch_dark: 'Koyu',
    layout_static: 'Sabit Olmayan',
    layout_offcanvas: 'Disa Acilir',
    layout_fixed: 'Sabit',
    layout_fixed_offcanvas: 'Sabit Disa Acilir',
    layout_dd_open_label: 'Uzerine Gelince Ac',
    layout_footer_label: 'Sabit Alt Bilgi',
    misc_header: 'Diger',
    theme_light: 'Acik',
    theme_dark: 'Koyu',
    theme_system: 'Sistem',
    skin_default: 'Varsayilan',
    skin_bordered: 'Kenarlikli',
    layout_expanded: 'Genisletilmis',
    layout_collapsed: 'Daraltilmis',
    content_compact: 'Dar',
    content_wide: 'Genis',
    direction_ltr: 'Soldan Saga',
    direction_rtl: 'Sagdan Sola',
    navbar_sticky: 'Sabit',
    navbar_static: 'Statik',
    navbar_hidden: 'Gizli'
  };

  TemplateCustomizer.LANGUAGES.es = {
    panel_header: 'Personalizador de Plantilla',
    panel_sub_header: 'Personaliza y previsualiza en tiempo real',
    theming_header: 'Temas',
    color_label: 'Color Principal',
    theme_label: 'Apariencia',
    skin_label: 'Estilos',
    semiDark_label: 'Menu Semi Oscuro',
    layout_header: 'Diseno',
    layout_label: 'Menu (Navegacion)',
    layout_header_label: 'Tipos de Encabezado',
    content_label: 'Ancho del Contenido',
    layout_navbar_label: 'Barra de Navegacion',
    direction_label: 'Direccion',
    style_switch_light: 'Claro',
    style_switch_dark: 'Oscuro',
    layout_static: 'Estatico',
    layout_offcanvas: 'Desplegable',
    layout_fixed: 'Fijo',
    layout_fixed_offcanvas: 'Fijo Desplegable',
    layout_dd_open_label: 'Abrir al pasar el raton',
    layout_footer_label: 'Pie de pagina fijo',
    misc_header: 'Varios',
    theme_light: 'Claro',
    theme_dark: 'Oscuro',
    theme_system: 'Sistema',
    skin_default: 'Por defecto',
    skin_bordered: 'Con bordes',
    layout_expanded: 'Expandido',
    layout_collapsed: 'Contraido',
    content_compact: 'Compacto',
    content_wide: 'Ancho',
    direction_ltr: 'Izquierda a Derecha',
    direction_rtl: 'Derecha a Izquierda',
    navbar_sticky: 'Fijo',
    navbar_static: 'Estatico',
    navbar_hidden: 'Oculto'
  };

  TemplateCustomizer.LANGUAGES.fr = {
    panel_header: 'Personnalisation du Theme',
    panel_sub_header: 'Personnaliser et previsualiser en temps reel',
    theming_header: 'Theme',
    color_label: 'Couleur Principale',
    theme_label: 'Theme',
    skin_label: 'Styles',
    semiDark_label: 'Semi Sombre',
    layout_header: 'Disposition',
    layout_label: 'Menu (Navigation)',
    layout_header_label: 'Types d'entete',
    content_label: 'Contenu',
    layout_navbar_label: 'Type de barre de navigation',
    direction_label: 'Direction',
    style_switch_light: 'Clair',
    style_switch_dark: 'Sombre',
    layout_static: 'Statique',
    layout_offcanvas: 'Hors canevas',
    layout_fixed: 'Fixe',
    layout_fixed_offcanvas: 'Fixe hors canevas',
    layout_dd_open_label: 'Ouvrir au survol',
    layout_footer_label: 'Pied de page fixe',
    misc_header: 'Divers',
    theme_light: 'Clair',
    theme_dark: 'Sombre',
    theme_system: 'Systeme',
    skin_default: 'Par defaut',
    skin_bordered: 'Bordure',
    layout_expanded: 'Etendu',
    layout_collapsed: 'Reduit',
    content_compact: 'Compact',
    content_wide: 'Large',
    direction_ltr: 'De gauche a droite',
    direction_rtl: 'De droite a gauche',
    navbar_sticky: 'Fixe',
    navbar_static: 'Statique',
    navbar_hidden: 'Cache'
  };

  TemplateCustomizer.LANGUAGES.ru = {
    panel_header: 'Nastroika shablona',
    panel_sub_header: 'Nastroika i predvaritelnyi prosmotr',
    theming_header: 'Temy',
    color_label: 'Osnovnoi tsvet',
    theme_label: 'Vid',
    skin_label: 'Stili',
    semiDark_label: 'Polutemnoe menu',
    layout_header: 'Razmetka',
    layout_label: 'Menu (Navigatsiya)',
    layout_header_label: 'Tipy zagolovkov',
    content_label: 'Shirina kontenta',
    layout_navbar_label: 'Panel navigatsii',
    direction_label: 'Napravlenie',
    style_switch_light: 'Svetlyi',
    style_switch_dark: 'Temnyi',
    layout_static: 'Statichnyi',
    layout_offcanvas: 'Vypadayushchii',
    layout_fixed: 'Fiksirovannyi',
    layout_fixed_offcanvas: 'Fiksirovannyi vypadayushchii',
    layout_dd_open_label: 'Otkryt pri navedenii',
    layout_footer_label: 'Fiks. futer',
    misc_header: 'Prochee',
    theme_light: 'Svetlyi',
    theme_dark: 'Temnyi',
    theme_system: 'Sistemnyi',
    skin_default: 'Po umolchaniyu',
    skin_bordered: 'S ramkoi',
    layout_expanded: 'Razvernutyi',
    layout_collapsed: 'Svernutyi',
    content_compact: 'Kompaktnyi',
    content_wide: 'Shirokii',
    direction_ltr: 'Sleva napravo',
    direction_rtl: 'Sprava nalevo',
    navbar_sticky: 'Fiksirovannyi',
    navbar_static: 'Statichnyi',
    navbar_hidden: 'Skrytyi'
  };

  TemplateCustomizer.LANGUAGES.zh = {
    panel_header: '模板自定义',
    panel_sub_header: '实时自定义并预览',
    theming_header: '主题',
    color_label: '主色',
    theme_label: '外观',
    skin_label: '样式',
    semiDark_label: '半暗色菜单',
    layout_header: '布局',
    layout_label: '菜单（导航）',
    layout_header_label: '页头类型',
    content_label: '内容宽度',
    layout_navbar_label: '导航栏类型',
    direction_label: '方向',
    style_switch_light: '浅色',
    style_switch_dark: '深色',
    layout_static: '静态',
    layout_offcanvas: '侧边滑出',
    layout_fixed: '固定',
    layout_fixed_offcanvas: '固定侧边滑出',
    layout_dd_open_label: '悬停展开',
    layout_footer_label: '固定页脚',
    misc_header: '其他',
    theme_light: '浅色',
    theme_dark: '深色',
    theme_system: '系统',
    skin_default: '默认',
    skin_bordered: '带边框',
    layout_expanded: '展开',
    layout_collapsed: '收起',
    content_compact: '紧凑',
    content_wide: '宽',
    direction_ltr: '从左到右',
    direction_rtl: '从右到左',
    navbar_sticky: '固定',
    navbar_static: '静态',
    navbar_hidden: '隐藏'
  };

  TemplateCustomizer.LANGUAGES.ar = {
    panel_header: 'مخصص القالب',
    panel_sub_header: 'تخصيص ومعاينة مباشرة',
    theming_header: 'السمات',
    color_label: 'اللون الرئيسي',
    theme_label: 'المظهر',
    skin_label: 'الأنماط',
    semiDark_label: 'قائمة شبه داكنة',
    layout_header: 'التخطيط',
    layout_label: 'القائمة (التنقل)',
    layout_header_label: 'أنواع الترويسة',
    content_label: 'عرض المحتوى',
    layout_navbar_label: 'نوع شريط التنقل',
    direction_label: 'الاتجاه',
    style_switch_light: 'فاتح',
    style_switch_dark: 'داكن',
    layout_static: 'ثابت',
    layout_offcanvas: 'لوحة جانبية',
    layout_fixed: 'مثبت',
    layout_fixed_offcanvas: 'مثبت مع لوحة جانبية',
    layout_dd_open_label: 'الفتح عند التمرير',
    layout_footer_label: 'تذييل مثبت',
    misc_header: 'متفرقات',
    theme_light: 'فاتح',
    theme_dark: 'داكن',
    theme_system: 'النظام',
    skin_default: 'افتراضي',
    skin_bordered: 'بحدود',
    layout_expanded: 'موسع',
    layout_collapsed: 'مطوي',
    content_compact: 'مضغوط',
    content_wide: 'واسع',
    direction_ltr: 'من اليسار إلى اليمين',
    direction_rtl: 'من اليمين إلى اليسار',
    navbar_sticky: 'مثبت',
    navbar_static: 'ثابت',
    navbar_hidden: 'مخفي'
  };

  const currentLang = window.CurrentLanguage || document.cookie.match(/\.AspNetCore\.Culture=c=([a-z]{2})/i)?.[1] || 'tr';

  // Apply localized titles to radio option buttons (Thematic labels)
  const t = TemplateCustomizer.LANGUAGES[currentLang];
  if (t) {
    const applyToArr = (arr, map) => {
      arr.forEach(opt => { if (map[opt.name]) opt.title = map[opt.name]; });
    };
    applyToArr(TemplateCustomizer.THEMES, { light: t.theme_light, dark: t.theme_dark, system: t.theme_system });
    applyToArr(TemplateCustomizer.SKINS, { default: t.skin_default, bordered: t.skin_bordered });
    applyToArr(TemplateCustomizer.LAYOUTS, { expanded: t.layout_expanded, collapsed: t.layout_collapsed });
    applyToArr(TemplateCustomizer.CONTENT, { compact: t.content_compact, wide: t.content_wide });
    applyToArr(TemplateCustomizer.DIRECTIONS, { ltr: t.direction_ltr, rtl: t.direction_rtl });
    applyToArr(TemplateCustomizer.NAVBAR_OPTIONS, { sticky: t.navbar_sticky, static: t.navbar_static, hidden: t.navbar_hidden });
  }

  window.templateCustomizer = new TemplateCustomizer({
    displayCustomizer: true,
    lang: currentLang,
    controls: [
      'color',
      'theme',
      'skins',
      'semiDark',
      'layoutCollapsed',
      'layoutNavbarOptions',
      'headerType',
      'contentLayout',
      'rtl'
    ]
  });

  // Ensure UI updates with the correct translations immediately
  if (window.templateCustomizer && typeof window.templateCustomizer.setLang === 'function') {
    window.templateCustomizer.setLang(currentLang);
  }
}


