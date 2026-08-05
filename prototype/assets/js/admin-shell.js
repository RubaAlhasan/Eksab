/* ==========================================================================
   Eksabli — Admin Portal (Host realm) chrome shared across every
   /admin/*.html page (except login.html). Mirrors business-shell.js.

   Usage in a page:
     <aside data-sidebar="businesses"></aside>
     <div class="drawer-overlay" data-sidebar-drawer-overlay></div>
     <div data-topbar data-title="Businesses" data-subtitle="Every tenant on the platform"></div>
   ========================================================================== */

const AdminShell = (() => {
  const NAV_GROUPS = [
    { label: 'Overview', items: [
      { id: 'dashboard', label: 'Dashboard', icon: 'squares-2x2', href: 'dashboard.html' },
    ]},
    { label: 'Platform', items: [
      { id: 'businesses', label: 'Businesses', icon: 'building-storefront', href: 'businesses.html' },
      { id: 'users', label: 'Users', icon: 'users', href: 'users.html' },
      { id: 'categories', label: 'Categories', icon: 'tag', href: 'categories.html' },
    ]},
    { label: 'Billing', items: [
      { id: 'subscriptions', label: 'Subscriptions', icon: 'credit-card', href: 'subscriptions.html' },
      { id: 'payments', label: 'Payments', icon: 'banknotes', href: 'payments.html' },
      { id: 'plans', label: 'Plans', icon: 'receipt-percent', href: 'plans.html' },
    ]},
    { label: 'Operations', items: [
      { id: 'support-tickets', label: 'Support Tickets', icon: 'clipboard-document', href: 'support-tickets.html' },
      { id: 'reports', label: 'Reports', icon: 'presentation-chart-line', href: 'reports.html' },
    ]},
    { label: 'System', items: [
      { id: 'feature-flags', label: 'Feature Flags', icon: 'sparkles', href: 'feature-flags.html' },
      { id: 'audit-logs', label: 'Audit Logs', icon: 'shield-check', href: 'audit-logs.html' },
      { id: 'system-settings', label: 'System Settings', icon: 'cog-6-tooth', href: 'system-settings.html' },
    ]},
  ];

  function sidebarHtml(activeId) {
    const groups = NAV_GROUPS.map((g) => `
      <p class="sidebar-section-label sidebar-label-text">${g.label}</p>
      ${g.items.map((item) => `
        <a href="${item.href}" class="sidebar-link ${item.id === activeId ? 'active' : ''}">
          ${Icons.html(item.icon, { size: 18 })}
          <span class="sidebar-label-text">${item.label}</span>
        </a>`).join('')}
    `).join('');

    return `
      <div class="flex items-center gap-2.5 px-3 py-5">
        <div class="w-9 h-9 rounded-xl bg-gradient-to-br from-slate-700 to-slate-950 flex items-center justify-center text-white font-extrabold text-sm flex-shrink-0">E</div>
        <div class="sidebar-label-text">
          <p class="font-extrabold text-base leading-none">Eksabli</p>
          <p class="text-[10px] text-slate-400 font-semibold uppercase tracking-wide mt-0.5">Admin</p>
        </div>
      </div>
      <nav class="flex-1 overflow-y-auto px-2 pb-4 space-y-0.5">${groups}</nav>
      <div class="p-3 border-t border-slate-100 dark:border-slate-800">
        <div class="flex items-center gap-2.5 p-2 rounded-xl">
          <div class="avatar avatar-sm flex-shrink-0" style="background:linear-gradient(135deg,#334155,#0F172A)">SA</div>
          <div class="min-w-0 sidebar-label-text">
            <p class="text-xs font-bold truncate">Super Admin</p>
            <p class="text-[11px] text-slate-400">Host Realm</p>
          </div>
        </div>
      </div>`;
  }

  function topbarHtml({ title, subtitle }) {
    return `
      <button data-sidebar-mobile-toggle class="btn btn-icon btn-ghost lg:hidden" aria-label="Menu">${Icons.html('bars-3', { size: 20 })}</button>
      <button data-sidebar-toggle class="btn btn-icon btn-ghost hidden lg:inline-flex" aria-label="Collapse sidebar">${Icons.html('bars-3', { size: 20 })}</button>
      <div class="min-w-0 flex-1">
        <h1 class="text-base font-bold truncate">${title || ''}</h1>
        ${subtitle ? `<p class="text-xs text-slate-400 truncate hidden sm:block">${subtitle}</p>` : ''}
      </div>
      <div class="relative hidden md:block w-64">
        <span class="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400">${Icons.html('search', { size: 16 })}</span>
        <input class="input pl-9 !py-2 text-sm" placeholder="Search tenants, users…">
      </div>
      <button data-theme-toggle class="btn btn-icon btn-ghost" aria-label="Toggle dark mode">
        <span class="block dark:hidden">${Icons.html('sun', { size: 18 })}</span>
        <span class="hidden dark:block">${Icons.html('moon', { size: 18 })}</span>
      </button>
      <div class="relative">
        <button data-dropdown-trigger="topbar-notif" class="btn btn-icon btn-ghost relative" aria-label="Notifications">
          ${Icons.html('bell', { size: 18 })}
          <span class="absolute top-1.5 right-1.5 w-2 h-2 rounded-full bg-danger-500"></span>
        </button>
        <div class="dropdown-menu right-0 mt-2 w-80" data-dropdown-menu="topbar-notif">
          <p class="text-xs font-bold uppercase tracking-wide text-slate-400 px-2 py-1.5">Platform Alerts</p>
          <a href="businesses.html" class="dropdown-item">${Icons.html('exclamation-triangle', { size: 16 })}<span>1 tenant pending approval</span></a>
          <a href="support-tickets.html" class="dropdown-item">${Icons.html('clipboard-document', { size: 16 })}<span>3 open support tickets</span></a>
          <a href="payments.html" class="dropdown-item">${Icons.html('banknotes', { size: 16 })}<span>1 failed payment needs review</span></a>
          <div class="dropdown-divider"></div>
          <span class="dropdown-item justify-center text-primary-600 dark:text-primary-400">View all</span>
        </div>
      </div>
      <div class="relative">
        <button data-dropdown-trigger="topbar-user" class="flex items-center gap-2" aria-label="Account menu">
          <div class="avatar avatar-sm" style="background:linear-gradient(135deg,#334155,#0F172A)">SA</div>
        </button>
        <div class="dropdown-menu right-0 mt-2 w-56" data-dropdown-menu="topbar-user">
          <div class="px-2 py-2"><p class="text-sm font-bold">Super Admin</p><p class="text-xs text-slate-400">platform@eksabli.app</p></div>
          <div class="dropdown-divider"></div>
          <a href="system-settings.html" class="dropdown-item">${Icons.html('cog-6-tooth', { size: 16 })}<span>System Settings</span></a>
          <a href="../index.html" class="dropdown-item">${Icons.html('arrow-path', { size: 16 })}<span>Switch Portal</span></a>
          <div class="dropdown-divider"></div>
          <a href="login.html" class="dropdown-item danger">${Icons.html('arrow-right-on-rectangle', { size: 16 })}<span>Log Out</span></a>
        </div>
      </div>`;
  }

  function init() {
    document.querySelectorAll('[data-sidebar]').forEach((el) => {
      el.classList.add('app-sidebar', 'flex', 'flex-col');
      el.innerHTML = sidebarHtml(el.getAttribute('data-sidebar'));
    });
    document.querySelectorAll('[data-topbar]').forEach((el) => {
      el.classList.add('app-topbar');
      el.innerHTML = topbarHtml({ title: el.getAttribute('data-title'), subtitle: el.getAttribute('data-subtitle') });
    });
    if (window.Eksabli) {
      window.Eksabli.initSidebar();
      window.Eksabli.initDropdowns();
      document.querySelectorAll('[data-topbar] [data-theme-toggle]').forEach((btn) => {
        btn.addEventListener('click', window.Eksabli.toggleTheme);
      });
    }
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }

  return { sidebarHtml, topbarHtml, NAV_GROUPS };
})();
