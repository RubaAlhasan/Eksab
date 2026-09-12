// Captures the customer-side redemption screen from the real Flutter web build.
//
// Flutter renders to a canvas, so there are no DOM selectors to drive — interaction is coordinate
// clicks and the verification is visual. What matters here is that the QR actually renders (staff
// have to scan it) and that the code under it matches the one the server issued.
//
// Needs the API on :44330 and `build/web` served on :4200 (the only origin in CorsOrigins).

process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';

const { chromium } = require('playwright-core');
const { execFileSync } = require('child_process');
const path = require('path');

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

(async () => {
  // Clear any coupon left pending by an earlier run, so this starts from the confirm screen rather
  // than jumping straight to a live code.
  const stale = sql(`SELECT "Id" FROM "AppCoupons" WHERE "Status" = 4;`);
  if (stale) console.log(`  (${stale.split('\n').length} pending coupon(s) already open)`);

  const browser = await chromium.launch({ channel: 'msedge', args: ['--ignore-certificate-errors'] });
  const page = await browser.newPage({
    viewport: { width: 420, height: 900 },
    deviceScaleFactor: 2,
    ignoreHTTPSErrors: true,
  });

  const errors = [];
  page.on('console', m => m.type() === 'error' && errors.push(m.text()));
  page.on('pageerror', e => errors.push(String(e)));

  const shot = async name => {
    await page.screenshot({ path: path.join(OUT, `m-${name}.png`) });
    console.log(`  shot: m-${name}.png`);
  };

  console.log('loading the app...');
  await page.goto(APP, { waitUntil: 'networkidle' });
  await page.waitForTimeout(6000);
  await shot('01-landing');

  // --- sign in (OTP) ---------------------------------------------------------------------------
  // Canvas, so every control is reached by clicking where it renders. Coordinates are CSS pixels
  // (the shots are 2x, so they are half the pixel position you read off the image).
  console.log('signing in...');
  // A first visit opens on the three-slide onboarding carousel; Skip lands on Register, and the
  // login screen is one more hop from there.
  await page.mouse.click(386, 22);           // Skip
  await page.waitForTimeout(2500);
  await page.mouse.click(294, 690);          // "Log in" on the register screen
  await page.waitForTimeout(2500);
  await shot('01b-login');

  await page.mouse.click(210, 225);          // phone field
  await page.waitForTimeout(600);
  await page.keyboard.type(PHONE, { delay: 40 });
  await page.waitForTimeout(400);
  await shot('02-phone');

  await page.mouse.click(210, 311);          // Send code
  await page.waitForTimeout(4000);
  await shot('03-otp-screen');

  const otp = sql(`SELECT "Message" FROM "AppSmsLogs" ORDER BY "CreationTime" DESC LIMIT 1;`).replace(/\D/g, '');
  console.log(`  otp: ${otp}`);
  await page.keyboard.type(otp, { delay: 120 });
  await page.waitForTimeout(5000);
  await shot('04-signed-in');

  // --- straight to the redemption screen -------------------------------------------------------
  // go_router honours deep links, so there is no need to click through the reward list.
  console.log('opening the redemption screen...');
  await page.goto(`${APP}/#/points/${TENANT}/rewards/${REWARD}/redeem`, { waitUntil: 'networkidle' });
  await page.waitForTimeout(4000);
  await shot('05-confirm');

  console.log(errors.length ? `\nPAGE ERRORS:\n${errors.slice(0, 10).join('\n')}` : '\nno page errors');
  console.log('\nleaving the browser open state captured; inspect m-*.png');
  await browser.close();
})().catch(e => { console.error('FAILED:', e.message); process.exit(1); });
