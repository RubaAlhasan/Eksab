// Drives the Business Portal's Redemption screen in a real browser and captures each state.
//
// The page is only reachable behind an ABP login, and its interesting states need a live Pending
// coupon, so this script mints one through the customer API first (OTP -> redeem), then walks staff
// through lookup and the decline path in the UI.
//
// Run with the API on :44330 and `npm start` on :4200.

const { chromium } = require('playwright-core');
const { execFileSync } = require('child_process');
const path = require('path');
process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';

const API = 'https://localhost:44330';
const APP = 'http://localhost:4200';
const TENANT = '3a2319a3-1446-dca2-3307-2731b0de6fda';
const PHONE = '+971500000043';
const REWARD = '195b9bf0-1f5a-4691-9b3d-5213e650e700';
const OUT = path.join(__dirname, 'shots');
const PSQL = 'C:\\Program Files\\PostgreSQL\\16\\bin\\psql.exe';

function sql(q) {
  return execFileSync(PSQL, ['-h', 'localhost', '-U', 'postgres', '-d', 'Eksabli', '-tAc', q], {
    env: { ...process.env, PGPASSWORD: '123' },
    encoding: 'utf8',
  }).trim();
}

async function post(url, body, headers = {}, form = false) {
  const res = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': form ? 'application/x-www-form-urlencoded' : 'application/json', ...headers },
    body: form ? new URLSearchParams(body).toString() : JSON.stringify(body),
  });
  const text = await res.text();
  if (!res.ok) throw new Error(`${url} -> ${res.status} ${text.slice(0, 300)}`);
  return text ? JSON.parse(text) : null;
}

async function mintPendingCoupon() {
  await post(`${API}/api/app/otp/request`, { phoneNumber: PHONE });
  const otp = sql(`SELECT "Message" FROM "AppSmsLogs" ORDER BY "CreationTime" DESC LIMIT 1;`).replace(/\D/g, '');

  const token = (await post(`${API}/connect/token`, {
    grant_type: 'otp', client_id: 'Eksabli_App', scope: 'offline_access Eksabli',
    phone_number: PHONE, otp_code: otp,
  }, {}, true)).access_token;

  const coupon = await post(`${API}/api/app/coupon/redeem`, { tenantId: TENANT, rewardId: REWARD },
    { Authorization: `Bearer ${token}` });

  console.log(`  minted pending coupon ${coupon.code}`);
  return coupon;
}

(async () => {
  const coupon = await mintPendingCoupon();

  const browser = await chromium.launch({
    channel: 'msedge',
    args: ['--ignore-certificate-errors'],
  });
  const page = await browser.newPage({
    viewport: { width: 1280, height: 1000 },
    ignoreHTTPSErrors: true,
  });

  const errors = [];
  page.on('console', m => m.type() === 'error' && errors.push(m.text()));
  page.on('pageerror', e => errors.push(String(e)));

  const shot = async name => {
    await page.screenshot({ path: path.join(OUT, `${name}.png`), fullPage: true });
    console.log(`  shot: ${name}.png`);
  };

  console.log('\nsigning in to the business portal...');

  // There are TWO `admin` users: one in the host realm and one inside StarbucksDemo. Logging in
  // without picking a tenant lands on the host admin, whose `/business/*` routes are turned away by
  // businessRealmGuard. ABP resolves the tenant from the `__tenant` cookie, so it has to be set on
  // BOTH origins — the SPA and the auth server are separate hosts and each resolves it for itself.
  await page.context().addCookies([
    { name: '__tenant', value: TENANT, url: APP },
    { name: '__tenant', value: TENANT, url: API },
  ]);

  await page.goto(APP, { waitUntil: 'networkidle' });
  await page.locator('a,button').filter({ hasText: /^Log in$/i }).first().click();
  // The hand-off to the auth server bounces through /connect/authorize before landing on the login
  // form; waiting on the form itself is the only reliable signal that it has settled.
  await page.waitForSelector('input[name="LoginInput.UserNameOrEmailAddress"]', { timeout: 60000 });

  await page.fill('input[name="LoginInput.UserNameOrEmailAddress"]', 'admin');
  await page.fill('input[name="LoginInput.Password"]', '1q2w3E*');
  await page.click('button[name="Action"][value="Login"]');
  await page.waitForURL(/localhost:4200/, { timeout: 60000 });
  console.log('  signed in');

  await page.goto(`${APP}/business/redemption`, { waitUntil: 'networkidle' });
  await page.waitForTimeout(1500);
  await shot('01-idle-scan');

  // Manual entry, typed the way staff would read it off a phone.
  await page.getByRole('tab', { name: /Enter Code/i }).click();
  await page.waitForTimeout(400);
  await shot('02-idle-manual');

  const spaced = `${coupon.code.slice(0, 4)} ${coupon.code.slice(4)}`;
  await page.fill('#redemption-code', spaced);
  await page.waitForTimeout(300);
  await shot('03-code-entered');

  await page.click('button[type="submit"]');
  await page.waitForTimeout(2000);
  await shot('04-review');

  // Decline path, so the run leaves no points spent.
  await page.getByRole('button', { name: /^Decline$/i }).click();
  await page.waitForTimeout(500);
  await shot('05-decline-reason');

  await page.fill('#decline-reason', 'Out of oat milk');
  await page.getByRole('button', { name: /Decline & Return Points/i }).click();
  await page.waitForTimeout(2000);
  await shot('06-declined');

  // Dark mode — the page defines no dark rules of its own and inherits the shell's tokens.
  await page.evaluate(() => {
    document.querySelector('.eks-biz-shell')?.classList.add('dark');
  });
  await page.waitForTimeout(400);
  await shot('07-declined-dark');

  console.log(errors.length ? `\nPAGE ERRORS:\n${errors.join('\n')}` : '\nno page errors');
  await browser.close();
})().catch(e => { console.error('FAILED:', e.message); process.exit(1); });
