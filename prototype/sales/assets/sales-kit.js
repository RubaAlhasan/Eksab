/* ==========================================================================
   Eksabli — Sales Enablement & Demo Kit: shared behaviour.

   Bilingual approach: BOTH languages live in the HTML side by side, marked
   with data-lang="en" / data-lang="ar", and sales-kit.css shows exactly one
   based on <html lang>. That is deliberate over a JS string dictionary —
   this kit is mostly long-form prose, and keeping each Arabic sentence next
   to its English source in the markup is what stops the two drifting apart
   when someone edits a talk track.

   Language + theme choices persist in localStorage, matching the pattern
   components.js already uses for the prototype's dark-mode toggle.
   ========================================================================== */

const SalesKit = (() => {
  const LANG_KEY = 'eksabli-kit-lang';

  const PAGES = [
    { id: 'hub',     href: 'index.html',       icon: 'squares-2x2',                en: 'Sales Hub',      ar: 'دليل المبيعات' },
    { id: 'script',  href: 'demo-script.html', icon: 'presentation-chart-line',    en: 'Demo Runbook',   ar: 'سيناريو العرض' },
    { id: 'screens', href: 'screens.html',     icon: 'device-phone-mobile',        en: 'Screen Catalog', ar: 'دليل الشاشات' },
  ];

  /* ---------------------------------------------------------------- */
  /* Language                                                          */
  /* ---------------------------------------------------------------- */
  // ?lang=ar wins over the stored choice, so a rep can bookmark or share an
  // Arabic-first link (and it gives us a deterministic way to test RTL).
  function currentLang() {
    const q = new URLSearchParams(location.search).get('lang');
    if (q === 'ar' || q === 'en') return q;
    return localStorage.getItem(LANG_KEY) === 'ar' ? 'ar' : 'en';
  }

  function applyLang(lang) {
    const root = document.documentElement;
    root.setAttribute('lang', lang);
    root.setAttribute('dir', lang === 'ar' ? 'rtl' : 'ltr');
    localStorage.setItem(LANG_KEY, lang);
    document.querySelectorAll('[data-lang-btn]').forEach((btn) => {
      btn.classList.toggle('active', btn.getAttribute('data-lang-btn') === lang);
    });
    // Screen previews are re-pointed at the same English prototype pages either
    // way — the note explaining that is markup, not logic, so nothing else to do.
  }

  function toggleLang() {
    applyLang(currentLang() === 'ar' ? 'en' : 'ar');
  }

  /* ---------------------------------------------------------------- */
  /* Chrome — one header for all three kit pages                       */
  /* ---------------------------------------------------------------- */
  function headerHtml(activeId) {
    const tabs = PAGES.map((p) => `
      <a href="${p.href}" class="kit-tab ${p.id === activeId ? 'active' : ''}">
        ${Icons.html(p.icon, { size: 16 })}
        <span data-lang="en">${p.en}</span><span data-lang="ar">${p.ar}</span>
      </a>`).join('');

    return `
      <div class="max-w-7xl mx-auto px-5 py-3 flex items-center gap-4 flex-wrap">
        <a href="../index.html" class="flex items-center gap-2.5 flex-shrink-0">
          <div class="w-9 h-9 rounded-xl bg-gradient-to-br from-primary-500 to-primary-700 flex items-center justify-center text-white font-extrabold text-sm shadow-soft">E</div>
          <div class="leading-none">
            <p class="font-extrabold text-base">Eksabli</p>
            <p class="text-[10px] text-slate-400 font-semibold uppercase tracking-wide mt-0.5">
              <span data-lang="en">Sales Kit</span><span data-lang="ar">حقيبة المبيعات</span>
            </p>
          </div>
        </a>

        <nav class="kit-tabs flex-1 min-w-0">${tabs}</nav>

        <div class="flex items-center gap-1.5 flex-shrink-0">
          <div class="tabs-pill">
            <button data-lang-btn="en" class="tab-pill-trigger" onclick="SalesKit.applyLang('en')">EN</button>
            <button data-lang-btn="ar" class="tab-pill-trigger" onclick="SalesKit.applyLang('ar')">ع</button>
          </div>
          <button data-theme-toggle class="btn btn-icon btn-ghost" aria-label="Toggle dark mode">
            <span class="block dark:hidden">${Icons.html('sun', { size: 18 })}</span>
            <span class="hidden dark:block">${Icons.html('moon', { size: 18 })}</span>
          </button>
        </div>
      </div>`;
  }

  function footerHtml() {
    return `
      <div class="max-w-7xl mx-auto px-5 py-10 text-xs text-slate-400 dark:text-slate-600 space-y-1.5">
        <p>
          <span data-lang="en"><strong>Internal sales material.</strong> Not for distribution to customers or prospects.</span>
          <span data-lang="ar"><strong>مادة مبيعات داخلية.</strong> غير مخصّصة للتوزيع على العملاء أو العملاء المحتملين.</span>
        </p>
        <p>
          <span data-lang="en">Every business name, customer name and figure shown in the product screens is fictional demo data from <code>prototype/assets/js/demo-data.js</code>. No real customer, price or performance claim appears anywhere in this kit.</span>
          <span data-lang="ar">جميع أسماء الشركات والعملاء والأرقام الظاهرة في شاشات المنتج هي بيانات تجريبية خيالية من <code>prototype/assets/js/demo-data.js</code>. لا يحتوي هذا الدليل على أي عميل حقيقي أو سعر أو ادّعاء بالأداء.</span>
        </p>
        <p>
          <span data-lang="en">Sources: <code>docs/eksabli-loyalty-platform/</code> (strategy + 8 feature specs) · <code>prototype/</code> (61 screens) · <code>NEXT_SESSION_PROMPT.md</code> (build status).</span>
          <span data-lang="ar">المصادر: <code>docs/eksabli-loyalty-platform/</code> (الاستراتيجية و٨ مواصفات) · <code>prototype/</code> (٦١ شاشة) · <code>NEXT_SESSION_PROMPT.md</code> (حالة التنفيذ).</span>
        </p>
      </div>`;
  }

  /* ---------------------------------------------------------------- */
  /* Screen preview — a scaled, non-interactive iframe of a real page   */
  /* ---------------------------------------------------------------- */
  function screenPreview({ href, kind = 'phone', large = false, label = '' }) {
    const openLabel = currentLang() === 'ar' ? 'افتح الشاشة' : 'Open screen';
    return `
      <a href="${href}" target="_blank" rel="noopener"
         class="screen-link screen-frame screen-frame--${kind}${large ? ' is-lg' : ''}"
         data-open-label="${openLabel}" aria-label="${label || href}">
        <iframe src="${href}" loading="lazy" tabindex="-1" title="${label || href}" scrolling="no"></iframe>
      </a>`;
  }

  /* ---------------------------------------------------------------- */
  function init(activeId) {
    document.querySelectorAll('[data-kit-header]').forEach((el) => {
      el.classList.add('kit-header');
      el.innerHTML = headerHtml(activeId || el.getAttribute('data-kit-header'));
    });
    document.querySelectorAll('[data-kit-footer]').forEach((el) => {
      el.innerHTML = footerHtml();
    });

    applyLang(currentLang());

    // components.js binds theme/tabs/dropdowns on its own DOMContentLoaded pass,
    // but our header is injected after that — bind just its theme button here.
    if (window.Eksabli) {
      document.querySelectorAll('[data-kit-header] [data-theme-toggle]').forEach((btn) => {
        btn.addEventListener('click', window.Eksabli.toggleTheme);
      });
    }

    // Render any icons declared declaratively in page markup.
    document.querySelectorAll('[data-icon]').forEach((el) => {
      el.innerHTML = Icons.html(el.getAttribute('data-icon'), {
        size: Number(el.getAttribute('data-icon-size')) || 18,
      });
    });

    // Render any screen previews declared declaratively in page markup.
    document.querySelectorAll('[data-screen]').forEach((el) => {
      el.outerHTML = screenPreview({
        href: el.getAttribute('data-screen'),
        kind: el.getAttribute('data-screen-kind') || 'phone',
        large: el.hasAttribute('data-screen-lg'),
        label: el.getAttribute('data-screen-label') || '',
      });
    });
  }

  return { init, applyLang, toggleLang, currentLang, screenPreview, PAGES };
})();

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', () => SalesKit.init());
} else {
  SalesKit.init();
}
