/* ==========================================================================
   Eksabli — Sales Kit: the 61-screen catalog.

   One entry per prototype screen, each with a single line on what it is
   USEFUL FOR IN A DEMO — not a description of what's on it (the thumbnail
   already shows that). Reps use this to assemble a custom path for an
   unusual prospect instead of being locked to the three-act runbook.

   The filter is implemented here rather than reusing components.js's
   initSearchFilters(), because that binds its NodeList at DOMContentLoaded
   and these cards are rendered afterwards — it would filter an empty set.
   ========================================================================== */

const ScreenCatalog = (() => {

  const GROUPS = [
    {
      id: 'customer',
      kind: 'phone',
      dir: '../customer/',
      en: 'Customer app', ar: 'تطبيق العميل',
      enSub: '30 screens · the mobile experience, shown in a phone frame',
      arSub: '٣٠ شاشة · تجربة الهاتف معروضة داخل إطار جهاز',
      screens: [
        { f: 'splash.html',              en: 'Splash',                ar: 'شاشة البداية',            useEn: 'Start here only if you want the full first-run story; otherwise skip.', useAr: 'ابدأ منها فقط إن أردت قصة التشغيل الأول كاملة، وإلا فتجاوزها.' },
        { f: 'onboarding.html',          en: 'Onboarding',            ar: 'التعريف بالتطبيق',        useEn: 'Three slides that state the promise: join, collect, redeem.', useAr: 'ثلاث شرائح تعرض الوعد: انضم، اجمع، استبدل.' },
        { f: 'register.html',            en: 'Register',              ar: 'إنشاء حساب',              useEn: 'Shows the account is created once with the platform, not with a shop.', useAr: 'تُظهر أن الحساب يُنشأ مرة واحدة مع المنصة لا مع متجر.' },
        { f: 'otp-verify.html',          en: 'OTP verification',      ar: 'التحقق برمز',             useEn: 'Phone-first sign-up — useful when a prospect worries about fake accounts.', useAr: 'تسجيل يعتمد الهاتف أولًا — مفيد حين يقلق العميل من الحسابات الوهمية.' },
        { f: 'login.html',               en: 'Login',                 ar: 'تسجيل الدخول',            useEn: 'Rarely worth demo time. Skip unless asked about returning users.', useAr: 'نادرًا ما تستحق وقت العرض. تجاوزها إلا إذا سُئلت عن العائدين.' },
        { f: 'forgot-password.html',     en: 'Forgot password',       ar: 'نسيت كلمة المرور',        useEn: 'Completeness only — never demo this.', useAr: 'للاكتمال فقط — لا تعرضها أبدًا.' },
        { f: 'home.html',                en: 'Home',                  ar: 'الرئيسية',                useEn: 'THE screen. Multiple businesses, separate balances, live campaigns, nearby discovery.', useAr: 'الشاشة الأهم. أنشطة متعدّدة وأرصدة منفصلة وحملات نشطة واكتشاف قريب.', star: true },
        { f: 'wallet.html',              en: 'Wallet',                ar: 'المحفظة',                 useEn: 'The one-account-many-businesses proof, as a plain list of balances.', useAr: 'إثبات «حساب واحد لأنشطة متعدّدة» في قائمة أرصدة بسيطة.', star: true },
        { f: 'my-memberships.html',      en: 'My memberships',        ar: 'عضوياتي',                 useEn: 'Use when someone asks what happens to memberships they no longer use.', useAr: 'استخدمها حين يُسأل عمّا يحدث للعضويات غير المستخدمة.' },
        { f: 'my-points.html',           en: 'My points',             ar: 'نقاطي',                   useEn: 'Tier progress bar — the psychology argument for why customers return.', useAr: 'شريط التقدّم في المستوى — الحجّة النفسية لعودة العملاء.', star: true },
        { f: 'transaction-history.html', en: 'Transaction history',   ar: 'سجل المعاملات',           useEn: 'Answers "how does the customer know their balance is right?"', useAr: 'تجيب عن: «كيف يعرف العميل أن رصيده صحيح؟»' },
        { f: 'join-store.html',          en: 'Join a business',       ar: 'الانضمام إلى نشاط',       useEn: 'Kills the "nobody downloads an app" objection — joining is one scan.', useAr: 'تُسقط اعتراض «لن يحمّل أحد التطبيق» — الانضمام بمسحة واحدة.', star: true },
        { f: 'store-details.html',       en: 'Store profile',         ar: 'صفحة النشاط',             useEn: 'What the prospect\'s own shop looks like to a customer. Very persuasive for brand-conscious owners.', useAr: 'كيف يبدو متجر العميل المحتمل لزبونه. مقنعة جدًا لمن يهتم بعلامته.' },
        { f: 'nearby-stores.html',       en: 'Nearby stores',         ar: 'المتاجر القريبة',         useEn: 'The "you are invisible if you\'re not on it" argument, in one screen.', useAr: 'حجّة «أنت غير مرئي إن لم تكن موجودًا» في شاشة واحدة.', star: true },
        { f: 'search.html',              en: 'Search',                ar: 'البحث',                   useEn: 'Discovery by category — pairs with Nearby for the competitive argument.', useAr: 'اكتشاف حسب الفئة — تُقرن بالمتاجر القريبة للحجّة التنافسية.' },
        { f: 'favorites.html',           en: 'Favorites',             ar: 'المفضّلة',                useEn: 'Customers who follow but haven\'t joined yet — a warm marketing list.', useAr: 'عملاء يتابعون دون انضمام — قائمة تسويقية مهيّأة.' },
        { f: 'rewards.html',             en: 'Rewards catalog',       ar: 'قائمة المكافآت',          useEn: 'Your catalog, your point prices. Dimmed items show what they\'re saving toward.', useAr: 'قائمتك وأسعارك بالنقاط. والعناصر الباهتة تُظهر ما يدّخرون له.' },
        { f: 'reward-details.html',      en: 'Reward detail',         ar: 'تفاصيل المكافأة',         useEn: 'Terms, validity and stock — use when asked how they cap their exposure.', useAr: 'الشروط والصلاحية والمخزون — عند السؤال عن ضبط الالتزام.' },
        { f: 'redeem-reward.html',       en: 'Redeem (QR + countdown)', ar: 'الاستبدال (رمز وعدّاد)', useEn: 'Let the countdown expire on screen — it proves the anti-fraud claim better than words.', useAr: 'دع العدّاد ينتهي أمامهم — يثبت مقاومة الاحتيال أفضل من أي كلام.', star: true },
        { f: 'coupons.html',             en: 'My coupons',            ar: 'قسائمي',                  useEn: 'Active / used / expired states — shows nothing is reusable.', useAr: 'حالات نشطة ومستخدمة ومنتهية — تُظهر أن لا شيء قابل لإعادة الاستخدام.' },
        { f: 'qr-code.html',             en: 'My wallet QR',          ar: 'رمز محفظتي',              useEn: 'The hand-off screen between the customer act and the counter act.', useAr: 'شاشة التسليم بين فصل العميل وفصل الصندوق.' },
        { f: 'qr-scanner.html',          en: 'QR scanner',            ar: 'ماسح الرموز',             useEn: 'Branch check-in. Useful for gyms and venues with a door, less so for retail.', useAr: 'تسجيل الدخول للفرع. مفيدة للأندية والأماكن ذات المدخل، أقل للتجزئة.' },
        { f: 'campaigns.html',           en: 'Offers feed',           ar: 'تدفّق العروض',            useEn: 'What a campaign looks like from the customer side — pair with the business campaign builder.', useAr: 'شكل الحملة من جهة العميل — تُقرن بمنشئ الحملات لدى النشاط.' },
        { f: 'notifications.html',       en: 'Notifications inbox',   ar: 'صندوق الإشعارات',         useEn: 'Proves messages have a home in the app, not just a push that vanishes.', useAr: 'تثبت أن للرسائل مكانًا في التطبيق لا مجرد إشعار يختفي.' },
        { f: 'birthday-rewards.html',    en: 'Birthday rewards',      ar: 'مكافآت عيد الميلاد',      useEn: 'The single easiest campaign to sell — set it once, it runs all year.', useAr: 'أسهل حملة على الإطلاق للبيع — تُضبط مرة وتعمل طوال العام.' },
        { f: 'referral.html',            en: 'Referral',              ar: 'الإحالة',                 useEn: 'Growth argument: existing members bringing new ones. Later phase — don\'t promise a date.', useAr: 'حجّة النمو: أعضاء حاليون يجلبون جددًا. مرحلة لاحقة — لا تعِد بموعد.' },
        { f: 'profile.html',             en: 'Profile',               ar: 'الملف الشخصي',            useEn: 'Skip unless asked what data the customer gives you.', useAr: 'تجاوزها إلا إذا سُئلت عن البيانات التي يقدّمها العميل.' },
        { f: 'edit-profile.html',        en: 'Edit profile',          ar: 'تعديل الملف',             useEn: 'Completeness only.', useAr: 'للاكتمال فقط.' },
        { f: 'settings.html',            en: 'Settings',              ar: 'الإعدادات',               useEn: 'Notification channel opt-outs and language — good answer to privacy questions.', useAr: 'إيقاف قنوات الإشعار واللغة — إجابة جيدة لأسئلة الخصوصية.' },
        { f: 'help.html',                en: 'Help',                  ar: 'المساعدة',                useEn: 'Completeness only.', useAr: 'للاكتمال فقط.' },
      ],
    },
    {
      id: 'business',
      kind: 'desk',
      dir: '../business/',
      en: 'Business portal', ar: 'بوابة النشاط',
      enSub: '17 screens · what the prospect themselves would use every day',
      arSub: '١٧ شاشة · ما سيستخدمه العميل المحتمل نفسه يوميًا',
      screens: [
        { f: 'login.html',             en: 'Login',                 ar: 'تسجيل الدخول',         useEn: 'Only as the transition into Act 2. Don\'t dwell.', useAr: 'فقط للانتقال إلى الفصل الثاني. لا تُطِل.' },
        { f: 'dashboard.html',         en: 'Dashboard',             ar: 'لوحة التحكم',          useEn: 'The Monday-morning screen. Active members, points issued vs. redeemed, live campaigns.', useAr: 'شاشة صباح الاثنين: الأعضاء النشطون، والممنوح مقابل المصروف، والحملات النشطة.', star: true },
        { f: 'analytics.html',         en: 'Analytics',             ar: 'التحليلات',            useEn: 'Redemption rate and branch comparison — the program-health argument.', useAr: 'معدل الاستبدال ومقارنة الفروع — حجّة صحة البرنامج.', star: true },
        { f: 'customers.html',         en: 'Customer list',         ar: 'قائمة العملاء',        useEn: '"At risk" and "churned" attached to real names — usually the biggest reaction in the demo.', useAr: '«معرّض للفقد» و«مفقود» مرتبطتان بأسماء حقيقية — غالبًا أكبر تفاعل في العرض.', star: true },
        { f: 'customer-details.html',  en: 'Customer detail',       ar: 'تفاصيل العميل',        useEn: 'One member\'s full picture, plus audited manual point adjustment.', useAr: 'صورة كاملة لعضو واحد، مع تعديل يدوي للنقاط خاضع للتدقيق.' },
        { f: 'points-management.html', en: 'Award points (POS)',    ar: 'منح النقاط (نقطة البيع)', useEn: 'Two steps at the counter, plus the earning rules and tiers behind them.', useAr: 'خطوتان عند الصندوق، مع قواعد الكسب والمستويات خلفهما.', star: true },
        { f: 'rewards.html',           en: 'Rewards management',    ar: 'إدارة المكافآت',       useEn: 'Show a prospect creating a reward that fits THEIR business — customise the example.', useAr: 'اعرض إنشاء مكافأة تناسب نشاطه هو — خصّص المثال.' },
        { f: 'campaigns.html',         en: 'Campaigns',             ar: 'الحملات',              useEn: 'The win-back draft plus segment preview. Highest-value minute of the whole demo.', useAr: 'مسوّدة الاستعادة مع معاينة الشريحة. أثمن دقيقة في العرض كله.', star: true },
        { f: 'coupons.html',           en: 'Coupons audit',         ar: 'تدقيق القسائم',        useEn: 'Which employee redeemed what, where, when — closes the fraud conversation.', useAr: 'من صرف ماذا وأين ومتى — تُغلق حديث الاحتيال.', star: true },
        { f: 'branches.html',          en: 'Branches',              ar: 'الفروع',               useEn: 'Per-branch QR codes and numbers. Essential for any multi-site prospect.', useAr: 'رموز وأرقام لكل فرع. أساسية لأي عميل متعدّد المواقع.' },
        { f: 'employees.html',         en: 'Employees & roles',     ar: 'الموظفون والأدوار',    useEn: 'Owner / manager / cashier / marketing — nobody gets more access than their job needs.', useAr: 'مالك ومدير وكاشير وتسويق — لا صلاحية تتجاوز متطلّبات الوظيفة.' },
        { f: 'transactions.html',      en: 'Transactions ledger',   ar: 'سجل المعاملات',        useEn: 'The raw data behind every chart — for the prospect who distrusts dashboards.', useAr: 'البيانات الخام خلف كل رسم — لمن لا يثق بلوحات المؤشرات.' },
        { f: 'reports.html',           en: 'Reports & export',      ar: 'التقارير والتصدير',    useEn: 'Exportable reports. Say "export", not "Excel export with a download token".', useAr: 'تقارير قابلة للتصدير. قل «تصدير» لا «تصدير إكسل برمز تنزيل».' },
        { f: 'notifications.html',     en: 'Notifications',         ar: 'الإشعارات',            useEn: 'Compose, delivery log and quota used against the plan.', useAr: 'الإنشاء وسجل التسليم والحصة المستهلكة من الباقة.' },
        { f: 'subscription.html',      en: 'Subscription & limits', ar: 'الاشتراك والحدود',     useEn: 'Plan usage against quota. Show the shape — never say an amount.', useAr: 'الاستهلاك مقابل الحصة. اعرض الشكل — ولا تذكر مبلغًا أبدًا.' },
        { f: 'billing.html',           en: 'Billing & invoices',    ar: 'الفوترة والفواتير',    useEn: 'Only for a finance stakeholder. Contains no approved prices.', useAr: 'لمسؤول المالية فقط. ولا تتضمّن أسعارًا معتمدة.' },
        { f: 'settings.html',          en: 'Business settings',     ar: 'إعدادات النشاط',       useEn: 'Branding, sender identity, and the cancel/export answer.', useAr: 'الهوية البصرية وهوية المرسل، وإجابة الإلغاء والتصدير.' },
      ],
    },
    {
      id: 'admin',
      kind: 'desk',
      dir: '../admin/',
      en: 'Platform admin', ar: 'إدارة المنصة',
      enSub: '14 screens · OUR console, not the customer\'s — show with care',
      arSub: '١٤ شاشة · لوحتنا نحن لا لوحة العميل — اعرضها بحذر',
      screens: [
        { f: 'login.html',            en: 'Admin login',        ar: 'دخول الإدارة',        useEn: 'Internal. No demo use.', useAr: 'داخلية. لا تُستخدم في العرض.' },
        { f: 'dashboard.html',        en: 'Platform dashboard', ar: 'لوحة المنصة',         useEn: 'Investor and internal-team context. Not for a business prospect.', useAr: 'سياق للمستثمرين والفريق الداخلي. ليست لعميل تجاري.' },
        { f: 'businesses.html',       en: 'Businesses (tenants)', ar: 'الأنشطة',           useEn: 'Shows businesses are reviewed before going live — a trust point for enterprise.', useAr: 'تُظهر مراجعة الأنشطة قبل نشرها — نقطة ثقة للمؤسسات.' },
        { f: 'business-details.html', en: 'Business detail',    ar: 'تفاصيل النشاط',       useEn: 'Internal support context. Avoid in customer meetings.', useAr: 'سياق دعم داخلي. تجنّبها في اجتماعات العملاء.' },
        { f: 'users.html',            en: 'Users',              ar: 'المستخدمون',          useEn: 'Never show to a business prospect — it invites "can you see my customers?"', useAr: 'لا تعرضها على عميل تجاري — فهي تثير سؤال «هل ترون عملائي؟»' },
        { f: 'categories.html',       en: 'Categories',         ar: 'الفئات',              useEn: 'Only relevant if a prospect asks how they\'ll be found in search.', useAr: 'مفيدة فقط إن سأل العميل كيف سيُعثر عليه في البحث.' },
        { f: 'subscriptions.html',    en: 'Subscriptions',      ar: 'الاشتراكات',          useEn: 'Internal billing oversight.', useAr: 'إشراف داخلي على الفوترة.' },
        { f: 'payments.html',         en: 'Payments',           ar: 'المدفوعات',           useEn: 'Internal. Payment provider is not chosen yet — do not discuss specifics.', useAr: 'داخلية. لم يُختر مزوّد الدفع بعد — لا تناقش التفاصيل.' },
        { f: 'plans.html',            en: 'Plan catalog',       ar: 'كتالوج الباقات',      useEn: 'Shows the four tiers by inclusions. Useful, but never quote an amount.', useAr: 'تعرض الباقات الأربع بمحتوياتها. مفيدة، لكن لا تذكر مبلغًا.' },
        { f: 'support-tickets.html',  en: 'Support tickets',    ar: 'تذاكر الدعم',         useEn: 'Answer to "what happens when something breaks?" for enterprise buyers.', useAr: 'إجابة سؤال «ماذا يحدث عند حدوث خلل؟» لمشتري المؤسسات.' },
        { f: 'reports.html',          en: 'Platform reports',   ar: 'تقارير المنصة',       useEn: 'Internal and investor context only.', useAr: 'سياق داخلي واستثماري فقط.' },
        { f: 'feature-flags.html',    en: 'Feature flags',      ar: 'مفاتيح الميزات',      useEn: 'Explains how plan limits are actually enforced. Only for a technical buyer.', useAr: 'تشرح كيف تُفرض حدود الباقات فعليًا. لمشترٍ تقني فقط.' },
        { f: 'audit-logs.html',       en: 'Audit logs',         ar: 'سجلات التدقيق',       useEn: 'The governance close: support access is always recorded, never silent.', useAr: 'خاتمة الحوكمة: وصول الدعم مسجَّل دائمًا ولا يتم بصمت.', star: true },
        { f: 'system-settings.html',  en: 'System settings',    ar: 'إعدادات النظام',      useEn: 'Internal. No demo use.', useAr: 'داخلية. لا تُستخدم في العرض.' },
      ],
    },
  ];

  function cardHtml(group, s) {
    const href = group.dir + s.f;
    const text = `${s.en} ${s.ar} ${s.useEn} ${group.en}`.toLowerCase();
    return `
      <article class="card p-3 flex flex-col gap-3" data-catalog-card data-catalog-text="${text.replace(/"/g, '')}">
        <div class="flex justify-center">
          ${SalesKit.screenPreview({ href, kind: group.kind, label: s.en })}
        </div>
        <div class="min-w-0">
          <div class="flex items-start gap-1.5 mb-1">
            ${s.star ? `<span class="text-warning-500 flex-shrink-0 mt-0.5">${Icons.html('star-solid', { size: 13 })}</span>` : ''}
            <p class="font-bold text-sm leading-tight">
              <span data-lang="en">${s.en}</span><span data-lang="ar">${s.ar}</span>
            </p>
          </div>
          <p class="text-xs text-slate-500 dark:text-slate-400 leading-relaxed">
            <span data-lang="en">${s.useEn}</span><span data-lang="ar">${s.useAr}</span>
          </p>
          <p class="text-[11px] text-slate-400 mt-1.5 font-mono">${group.dir.replace('../', '')}${s.f}</p>
        </div>
      </article>`;
  }

  function groupHtml(group) {
    return `
      <section class="kit-section" id="cat-${group.id}">
        <div class="flex items-baseline gap-3 flex-wrap mb-1">
          <h2 class="text-xl font-extrabold tracking-tight">
            <span data-lang="en">${group.en}</span><span data-lang="ar">${group.ar}</span>
          </h2>
          <p class="text-sm text-slate-400">
            <span data-lang="en">${group.enSub}</span><span data-lang="ar">${group.arSub}</span>
          </p>
        </div>
        <div class="grid gap-4 mt-4" style="grid-template-columns:repeat(auto-fill,minmax(200px,1fr))">
          ${group.screens.map((s) => cardHtml(group, s)).join('')}
        </div>
      </section>`;
  }

  function render(mountSelector) {
    const mount = document.querySelector(mountSelector);
    if (!mount) return;
    mount.innerHTML = GROUPS.map(groupHtml).join('');

    const input = document.querySelector('[data-catalog-filter]');
    const empty = document.querySelector('[data-catalog-empty]');
    if (!input) return;
    input.addEventListener('input', () => {
      const q = input.value.trim().toLowerCase();
      let visible = 0;
      document.querySelectorAll('[data-catalog-card]').forEach((card) => {
        const match = card.getAttribute('data-catalog-text').includes(q);
        card.classList.toggle('hidden', !match);
        if (match) visible++;
      });
      // Hide a whole group heading when nothing inside it survived the filter.
      GROUPS.forEach((g) => {
        const section = document.getElementById(`cat-${g.id}`);
        if (!section) return;
        const anyVisible = section.querySelectorAll('[data-catalog-card]:not(.hidden)').length > 0;
        section.classList.toggle('hidden', !anyVisible);
      });
      if (empty) empty.classList.toggle('hidden', visible !== 0);
    });
  }

  return { render, GROUPS };
})();
