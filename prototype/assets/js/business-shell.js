/* ==========================================================================
   Eksabli — Business Portal chrome shared across every /business/*.html page
   (except login.html). Injects the sidebar and topbar into placeholders so
   pages stay DRY without a build step.

   Usage in a page:
     <aside data-sidebar="customers"></aside>              <!-- value = active nav id -->
     <div class="drawer-overlay" data-sidebar-drawer-overlay></div>
     <div data-topbar data-title="Customers" data-subtitle="Manage every member of your business"></div>
   ========================================================================== */

const BusinessShell = (() => {
  const NAV_GROUPS = [
    { label: 'Overview', items: [
      { id: 'dashboard', label: 'Dashboard', icon: 'squares-2x2', href: 'dashboard.html' },
      { id: 'analytics', label: 'Analytics', icon: 'presentation-chart-line', href: 'analytics.html' },
    ]},
    { label: 'Operations', items: [
      { id: 'customers', label: 'Customers', icon: 'users', href: 'customers.html' },
      { id: 'points', label: 'Points Management', icon: 'wallet', href: 'points-management.html' },
      { id: 'rewards', label: 'Rewards', icon: 'gift', href: 'rewards.html' },
      { id: 'campaigns', label: 'Campaigns', icon: 'megaphone', href: 'campaigns.html' },
      { id: 'coupons', label: 'Coupons', icon: 'ticket', href: 'coupons.html' },
    ]},
    { label: 'Management', items: [
      { id: 'branches', label: 'Branches', icon: 'building-storefront', href: 'branches.html' },
      { id: 'employees', label: 'Employees', icon: 'users', href: 'employees.html' },
    ]},
    { label: 'Insights', items: [
      { id: 'transactions', label: 'Transactions', icon: 'banknotes', href: 'transactions.html' },
      { id: 'reports', label: 'Reports', icon: 'document-arrow-down', href: 'reports.html' },
    ]},
    { label: 'Account', items: [
      { id: 'subscription', label: 'Subscription', icon: 'credit-card', href: 'subscription.html' },
      { id: 'billing', label: 'Billing', icon: 'receipt-percent', href: 'billing.html' },
      { id: 'notifications', label: 'Notifications', icon: 'bell', href: 'notifications.html' },
      { id: 'settings', label: 'Settings', icon: 'cog-6-tooth', href: 'settings.html' },
    ]},
  ];

  function sidebarHtml(activeId) {
    const profile = window.EksabliDemoData?.business?.profile || { name: 'Business', logoInitials: 'B', plan: 'Growth' };
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
        <div class="w-9 h-9 rounded-xl bg-gradient-to-br from-primary-500 to-primary-700 flex items-center justify-center text-white font-extrabold text-sm flex-shrink-0">E</div>
        <span class="font-extrabold text-base sidebar-label-text">Eksabli</span>
      </div>
      <nav class="flex-1 overflow-y-auto px-2 pb-4 space-y-0.5">${groups}</nav>
      <div class="p-3 border-t border-slate-100 dark:border-slate-800">
        <div class="flex items-center gap-2.5 p-2 rounded-xl">
          <div class="avatar avatar-sm flex-shrink-0">${profile.logoInitials}</div>
          <div class="min-w-0 sidebar-label-text">
            <p class="text-xs font-bold truncate">${profile.name}</p>
            <p class="text-[11px] text-slate-400">${profile.plan} Plan</p>
          </div>
        </div>
      </div>`;
  }

  function topbarHtml({ title, subtitle }) {
    const unread = (window.EksabliDemoData?.business) ? 3 : 0;
    return `
      <button data-sidebar-mobile-toggle class="btn btn-icon btn-ghost lg:hidden" aria-label="Menu">${Icons.html('bars-3', { size: 20 })}</button>
      <button data-sidebar-toggle class="btn btn-icon btn-ghost hidden lg:inline-flex" aria-label="Collapse sidebar">${Icons.html('bars-3', { size: 20 })}</button>
      <div class="min-w-0 flex-1">
        <h1 class="text-base font-bold truncate">${title || ''}</h1>
        ${subtitle ? `<p class="text-xs text-slate-400 truncate hidden sm:block">${subtitle}</p>` : ''}
      </div>
      <div class="relative hidden md:block w-64">
        <span class="absolute left-3 top-1/2 -translate-y-1/2 text-slate-400">${Icons.html('search', { size: 16 })}</span>
        <input class="input pl-9 !py-2 text-sm" placeholder="Search…">
      </div>
      <button data-theme-toggle class="btn btn-icon btn-ghost" aria-label="Toggle dark mode">
        <span class="block dark:hidden">${Icons.html('sun', { size: 18 })}</span>
        <span class="hidden dark:block">${Icons.html('moon', { size: 18 })}</span>
      </button>
      <div class="relative">
        <button data-dropdown-trigger="topbar-notif" class="btn btn-icon btn-ghost relative" aria-label="Notifications">
          ${Icons.html('bell', { size: 18 })}
          ${unread ? `<span class="absolute top-1.5 right-1.5 w-2 h-2 rounded-full bg-danger-500"></span>` : ''}
        </button>
        <div class="dropdown-menu right-0 mt-2 w-80" data-dropdown-menu="topbar-notif">
          <p class="text-xs font-bold uppercase tracking-wide text-slate-400 px-2 py-1.5">Notifications</p>
          <a href="notifications.html" class="dropdown-item"><span>${Icons.html('exclamation-triangle', { size: 16 })}</span><span>1 reward is low on stock</span></a>
          <a href="customers.html" class="dropdown-item"><span>${Icons.html('users', { size: 16 })}</span><span>12 new members this week</span></a>
          <a href="billing.html" class="dropdown-item"><span>${Icons.html('credit-card', { size: 16 })}</span><span>Invoice due in 3 days</span></a>
          <div class="dropdown-divider"></div>
          <a href="notifications.html" class="dropdown-item justify-center text-primary-600 dark:text-primary-400">View all</a>
        </div>
      </div>
      <div class="relative">
        <button data-dropdown-trigger="topbar-user" class="flex items-center gap-2" aria-label="Account menu">
          <div class="avatar avatar-sm">MR</div>
        </button>
        <div class="dropdown-menu right-0 mt-2 w-56" data-dropdown-menu="topbar-user">
          <div class="px-2 py-2">
            <p class="text-sm font-bold">Marcus Reid</p>
            <p class="text-xs text-slate-400">Business Owner</p>
          </div>
          <div class="dropdown-divider"></div>
          <a href="settings.html" class="dropdown-item">${Icons.html('cog-6-tooth', { size: 16 })}<span>Business Settings</span></a>
          <a href="../index.html" class="dropdown-item">${Icons.html('arrow-path', { size: 16 })}<span>Switch Portal</span></a>
          <div class="dropdown-divider"></div>
          <a href="login.html" class="dropdown-item danger">${Icons.html('arrow-right-on-rectangle', { size: 16 })}<span>Log Out</span></a>
        </div>
      </div>`;
  }

  function init() {
    document.querySelectorAll('[data-sidebar]').forEach((el) => {
      const activeId = el.getAttribute('data-sidebar');
      el.classList.add('app-sidebar', 'flex', 'flex-col');
      el.innerHTML = sidebarHtml(activeId);
    });
    document.querySelectorAll('[data-topbar]').forEach((el) => {
      el.classList.add('app-topbar');
      el.innerHTML = topbarHtml({ title: el.getAttribute('data-title'), subtitle: el.getAttribute('data-subtitle') });
    });
    // Only re-scan the pieces that live *inside* the sidebar/topbar we just injected
    // (sidebar collapse/drawer toggles, the topbar's own dropdowns, its theme-toggle button).
    // Calling the full Eksabli.init() here would re-bind click handlers on modals/tabs/search
    // filters that components.js's own DOMContentLoaded listener already bound on first load.
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
