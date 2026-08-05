/* ==========================================================================
   Eksabli — shared interactive behavior for the static-HTML prototype.
   Wires up any element carrying the data-* hooks below. Include after
   icons.js and demo-data.js on every page:
     <script src="assets/js/icons.js"></script>
     <script src="assets/js/demo-data.js"></script>
     <script src="assets/js/components.js"></script>
   ========================================================================== */

const Eksabli = (() => {
  /* ---------------------------- Theme ---------------------------- */
  function initTheme() {
    const saved = localStorage.getItem('eksabli-theme');
    const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
    const dark = saved ? saved === 'dark' : prefersDark;
    document.documentElement.classList.toggle('dark', dark);
    document.querySelectorAll('[data-theme-toggle]').forEach((btn) => {
      btn.setAttribute('aria-pressed', String(dark));
    });
  }
  function toggleTheme() {
    const dark = document.documentElement.classList.toggle('dark');
    localStorage.setItem('eksabli-theme', dark ? 'dark' : 'light');
    document.querySelectorAll('[data-theme-toggle]').forEach((btn) => {
      btn.setAttribute('aria-pressed', String(dark));
    });
  }

  /* -------------------------- Sidebar (portal) -------------------------- */
  function initSidebar() {
    const sidebar = document.querySelector('[data-sidebar]');
    if (!sidebar) return;
    const collapsed = localStorage.getItem('eksabli-sidebar-collapsed') === '1';
    sidebar.classList.toggle('collapsed', collapsed);
    document.querySelectorAll('[data-sidebar-toggle]').forEach((btn) => {
      btn.addEventListener('click', () => {
        const isCollapsed = sidebar.classList.toggle('collapsed');
        localStorage.setItem('eksabli-sidebar-collapsed', isCollapsed ? '1' : '0');
      });
    });
    const drawerOverlay = document.querySelector('[data-sidebar-drawer-overlay]');
    document.querySelectorAll('[data-sidebar-mobile-toggle]').forEach((btn) => {
      btn.addEventListener('click', () => {
        sidebar.classList.toggle('mobile-open');
        if (drawerOverlay) drawerOverlay.classList.toggle('open');
      });
    });
    if (drawerOverlay) {
      drawerOverlay.addEventListener('click', () => {
        sidebar.classList.remove('mobile-open');
        drawerOverlay.classList.remove('open');
      });
    }
  }

  /* ------------------------------ Dropdown ------------------------------ */
  function initDropdowns() {
    document.querySelectorAll('[data-dropdown-trigger]').forEach((trigger) => {
      const id = trigger.getAttribute('data-dropdown-trigger');
      const menu = document.querySelector(`[data-dropdown-menu="${id}"]`);
      if (!menu) return;
      trigger.addEventListener('click', (e) => {
        e.stopPropagation();
        document.querySelectorAll('.dropdown-menu.open').forEach((m) => {
          if (m !== menu) m.classList.remove('open');
        });
        menu.classList.toggle('open');
      });
    });
    document.addEventListener('click', (e) => {
      document.querySelectorAll('.dropdown-menu.open').forEach((menu) => {
        if (!menu.contains(e.target)) menu.classList.remove('open');
      });
    });
    document.addEventListener('keydown', (e) => {
      if (e.key === 'Escape') {
        document.querySelectorAll('.dropdown-menu.open').forEach((m) => m.classList.remove('open'));
      }
    });
  }

  /* ------------------------------- Modal --------------------------------- */
  function openModal(id) {
    const overlay = document.querySelector(`[data-modal="${id}"]`);
    if (overlay) overlay.classList.add('open');
  }
  function closeModal(id) {
    const overlay = id ? document.querySelector(`[data-modal="${id}"]`) : null;
    if (overlay) { overlay.classList.remove('open'); return; }
    document.querySelectorAll('.modal-overlay.open').forEach((m) => m.classList.remove('open'));
  }
  function initModals() {
    document.querySelectorAll('[data-modal-open]').forEach((btn) => {
      btn.addEventListener('click', () => openModal(btn.getAttribute('data-modal-open')));
    });
    document.querySelectorAll('[data-modal-close]').forEach((btn) => {
      btn.addEventListener('click', () => closeModal(btn.getAttribute('data-modal-close') || null));
    });
    document.querySelectorAll('.modal-overlay').forEach((overlay) => {
      overlay.addEventListener('click', (e) => {
        if (e.target === overlay) overlay.classList.remove('open');
      });
    });
    document.addEventListener('keydown', (e) => {
      if (e.key === 'Escape') closeModal();
    });
  }

  /* -------------------------------- Sheet -------------------------------- */
  function openSheet(id) {
    const overlay = document.querySelector(`[data-sheet="${id}"]`);
    if (overlay) overlay.classList.add('open');
  }
  function closeSheet(id) {
    const overlay = id ? document.querySelector(`[data-sheet="${id}"]`) : null;
    if (overlay) { overlay.classList.remove('open'); return; }
    document.querySelectorAll('.sheet-overlay.open').forEach((m) => m.classList.remove('open'));
  }
  function initSheets() {
    document.querySelectorAll('[data-sheet-open]').forEach((btn) => {
      btn.addEventListener('click', () => openSheet(btn.getAttribute('data-sheet-open')));
    });
    document.querySelectorAll('[data-sheet-close]').forEach((btn) => {
      btn.addEventListener('click', () => closeSheet(btn.getAttribute('data-sheet-close') || null));
    });
    document.querySelectorAll('.sheet-overlay').forEach((overlay) => {
      overlay.addEventListener('click', (e) => {
        if (e.target === overlay) overlay.classList.remove('open');
      });
    });
  }

  /* -------------------------------- Drawer ------------------------------- */
  function initDrawers() {
    document.querySelectorAll('[data-drawer-open]').forEach((btn) => {
      btn.addEventListener('click', () => {
        const id = btn.getAttribute('data-drawer-open');
        document.querySelector(`[data-drawer-overlay="${id}"]`)?.classList.add('open');
      });
    });
    document.querySelectorAll('[data-drawer-close]').forEach((btn) => {
      btn.addEventListener('click', () => {
        const id = btn.getAttribute('data-drawer-close');
        const overlay = id ? document.querySelector(`[data-drawer-overlay="${id}"]`) : document.querySelector('.drawer-overlay.open');
        overlay?.classList.remove('open');
      });
    });
    document.querySelectorAll('.drawer-overlay').forEach((overlay) => {
      overlay.addEventListener('click', (e) => {
        if (e.target === overlay) overlay.classList.remove('open');
      });
    });
  }

  /* -------------------------------- Tabs --------------------------------- */
  function initTabs() {
    document.querySelectorAll('[data-tabs]').forEach((group) => {
      const groupId = group.getAttribute('data-tabs');
      const triggers = group.querySelectorAll(`[data-tab-trigger="${groupId}"]`);
      triggers.forEach((trigger) => {
        trigger.addEventListener('click', () => {
          const target = trigger.getAttribute('data-tab-target');
          triggers.forEach((t) => t.classList.remove('active'));
          trigger.classList.add('active');
          document.querySelectorAll(`[data-tab-panel="${groupId}"]`).forEach((panel) => {
            panel.classList.toggle('active', panel.getAttribute('data-tab-id') === target);
          });
        });
      });
    });
  }

  /* -------------------------------- Toasts -------------------------------- */
  const toastConfig = {
    success: { icon: 'check-circle', bg: 'bg-success-100 dark:bg-success-500/15', color: 'text-success-600 dark:text-success-400' },
    danger: { icon: 'exclamation-circle', bg: 'bg-danger-100 dark:bg-danger-500/15', color: 'text-danger-600 dark:text-danger-400' },
    warning: { icon: 'exclamation-triangle', bg: 'bg-warning-100 dark:bg-warning-500/15', color: 'text-warning-600 dark:text-warning-400' },
    info: { icon: 'information-circle', bg: 'bg-info-100 dark:bg-info-500/15', color: 'text-info-600 dark:text-info-400' },
  };
  function ensureToastContainer() {
    let el = document.getElementById('toast-container');
    if (!el) {
      el = document.createElement('div');
      el.id = 'toast-container';
      document.body.appendChild(el);
    }
    return el;
  }
  function showToast({ type = 'info', title, message = '', duration = 4000 } = {}) {
    const container = ensureToastContainer();
    const cfg = toastConfig[type] || toastConfig.info;
    const toast = document.createElement('div');
    toast.className = 'toast';
    toast.innerHTML = `
      <div class="toast-icon ${cfg.bg} ${cfg.color}">${Icons.html(cfg.icon, { size: 16 })}</div>
      <div class="flex-1 min-w-0">
        <p class="text-sm font-semibold text-slate-900 dark:text-slate-50">${title}</p>
        ${message ? `<p class="text-xs text-slate-500 dark:text-slate-400 mt-0.5">${message}</p>` : ''}
      </div>
      <button class="text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 flex-shrink-0" aria-label="Dismiss">${Icons.html('x-mark', { size: 16 })}</button>`;
    toast.querySelector('button').addEventListener('click', () => toast.remove());
    container.appendChild(toast);
    if (duration) setTimeout(() => toast.remove(), duration);
    return toast;
  }

  /* ------------------------------ List filter ------------------------------ */
  function initSearchFilters() {
    document.querySelectorAll('[data-filter-input]').forEach((input) => {
      const targetSelector = input.getAttribute('data-filter-input');
      const items = document.querySelectorAll(targetSelector);
      const emptyState = document.querySelector(input.getAttribute('data-filter-empty') || '');
      input.addEventListener('input', () => {
        const q = input.value.trim().toLowerCase();
        let visibleCount = 0;
        items.forEach((item) => {
          const text = (item.getAttribute('data-filter-text') || item.textContent).toLowerCase();
          const match = text.includes(q);
          item.classList.toggle('hidden', !match);
          if (match) visibleCount++;
        });
        if (emptyState) emptyState.classList.toggle('hidden', visibleCount !== 0);
      });
    });
  }

  /* -------------------------------- Init -------------------------------- */
  function init() {
    initTheme();
    initSidebar();
    initDropdowns();
    initModals();
    initSheets();
    initDrawers();
    initTabs();
    initSearchFilters();
    document.querySelectorAll('[data-theme-toggle]').forEach((btn) => btn.addEventListener('click', toggleTheme));
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }

  // initSidebar/initDropdowns are also exposed individually (not just via init()) so that
  // business-shell.js / admin-shell.js can re-scan the DOM after injecting the sidebar/topbar
  // placeholders, without re-binding everything init() already bound on first load (which would
  // double-fire click handlers on modals/tabs/search-filters that existed since page load).
  return { toggleTheme, openModal, closeModal, openSheet, closeSheet, showToast, init, initSidebar, initDropdowns };
})();
