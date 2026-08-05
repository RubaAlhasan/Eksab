/* ==========================================================================
   Eksabli — Customer app chrome shared across every /customer/*.html page:
   the decorative status bar, the bottom tab bar, and a generic inner-page
   top bar (back button + title + optional action). Injected into
   placeholder elements so every page stays DRY without a build step.

   Usage in a page:
     <div data-status-bar></div>
     ...
     <div data-top-bar data-title="Store Details" data-back="store-details.html"></div>
     ...
     <div data-bottom-nav="home"></div>   <!-- value = active tab id -->
   ========================================================================== */

const CustomerShell = (() => {
  function statusBarHtml() {
    return `
      <div class="flex items-center justify-between px-6 pt-3 pb-1 text-xs font-semibold text-slate-900 dark:text-slate-50 select-none">
        <span>9:41</span>
        <div class="flex items-center gap-1.5">
          ${Icons.html('presentation-chart-line', { size: 14 })}
          <span class="w-4 h-3 rounded-sm border border-current relative">
            <span class="absolute inset-0.5 bg-current rounded-[1px]"></span>
          </span>
        </div>
      </div>`;
  }

  const NAV_ITEMS = [
    { id: 'home', label: 'Home', icon: 'home', href: 'home.html' },
    { id: 'search', label: 'Search', icon: 'search', href: 'search.html' },
    { id: 'wallet', label: 'Wallet', icon: 'wallet', href: 'wallet.html' },
    { id: 'notifications', label: 'Alerts', icon: 'bell', href: 'notifications.html' },
    { id: 'profile', label: 'Profile', icon: 'user', href: 'profile.html' },
  ];

  function bottomNavHtml(activeId) {
    const items = NAV_ITEMS.map((item) => {
      const active = item.id === activeId;
      return `
        <a href="${item.href}" class="bottom-nav-item ${active ? 'active' : ''}">
          ${Icons.html(item.icon, { size: 22 })}
          <span>${item.label}</span>
        </a>`;
    }).join('');
    return `<nav class="bottom-nav">${items}</nav>`;
  }

  function topBarHtml({ title, back, actionIcon, actionHref, onAction }) {
    return `
      <div class="flex items-center gap-3 px-5 py-4 border-b border-slate-100 dark:border-slate-800 bg-white/80 dark:bg-slate-950/80 backdrop-blur sticky top-0 z-30">
        ${back ? `<a href="${back}" class="btn btn-icon btn-ghost -ml-2" aria-label="Back">${Icons.html('arrow-left', { size: 18 })}</a>` : ''}
        <h1 class="text-base font-bold flex-1 truncate">${title}</h1>
        ${actionIcon ? `<a href="${actionHref || '#'}" ${onAction ? `onclick="${onAction}"` : ''} class="btn btn-icon btn-ghost -mr-2" aria-label="Action">${Icons.html(actionIcon, { size: 18 })}</a>` : ''}
      </div>`;
  }

  function init() {
    document.querySelectorAll('[data-status-bar]').forEach((el) => { el.outerHTML = statusBarHtml(); });
    document.querySelectorAll('[data-bottom-nav]').forEach((el) => {
      el.outerHTML = bottomNavHtml(el.getAttribute('data-bottom-nav'));
    });
    document.querySelectorAll('[data-top-bar]').forEach((el) => {
      el.outerHTML = topBarHtml({
        title: el.getAttribute('data-title') || '',
        back: el.getAttribute('data-back'),
        actionIcon: el.getAttribute('data-action-icon'),
        actionHref: el.getAttribute('data-action-href'),
        onAction: el.getAttribute('data-on-action'),
      });
    });
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }

  return { statusBarHtml, bottomNavHtml, topBarHtml, NAV_ITEMS };
})();
