// Walks the customer app and captures every principal screen, for design review.
//
// Logs in for real (OTP), then deep-links to each route — go_router handles hash links, so the tour
// does not depend on finding nav targets by coordinate on a canvas.
//
// Needs the API on :44330 and build/web served on :4201.

process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';

const { chromium } = require('playwright-core');
const { execFileSync } = require('child_process');
const path = require('path');
const fs = require('fs');

const API = 'https://localhost:44330';
const APP = 'http://localhost:4201';
const TENANT = '3a2319a3-1446-dca2-3307-2731b0de6fda';
const REWARD = '195b9bf0-1f5a-4691-9b3d-5213e650e700';
const PHONE = process.env.TOUR_PHONE || '+971500000043';
// Overwriting a PNG that something else still has open fails on Windows, and reviewing
// these means opening them — so each run can write somewhere fresh.
const OUT = path.join(__dirname, process.env.TOUR_OUT || 'tour');
const PSQL = 'C:\\Program Files\\PostgreSQL\\16\\bin\\psql.exe';

fs.mkdirSync(OUT, { recursive: true });

function sql(q) {
  return execFileSync(PSQL, ['-h', 'localhost', '-U', 'postgres', '-d', 'Eksabli', '-tAc', q], {
    env: { ...process.env, PGPASSWORD: '123' }, encoding: 'utf8',
  }).trim();
}

const SCREENS = [
  ['home',        '/home'],
  ['search',      '/search'],
  ['nearby',      '/nearby'],
  ['wallet',      '/wallet'],
  ['points',      `/points/${TENANT}`],
  ['history',     `/points/${TENANT}/history`],
  ['rewards',     `/points/${TENANT}/rewards`],
  ['reward',      `/points/${TENANT}/rewards/${REWARD}`],
  ['coupons',     '/coupons'],
  ['store',       `/store/${TENANT}`],
  ['qr-code',     '/qr-code'],
  ['notifications', '/notifications'],
  ['campaigns',   '/campaigns'],
  ['memberships', '/memberships'],
  ['favorites',   '/favorites'],
  ['referral',    '/referral'],
  ['profile',     '/profile'],
  ['settings',    '/settings'],
  ['help',        '/help'],
];

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', args: ['--ignore-certificate-errors'] });
  const page = await browser.newPage({
    viewport: { width: 390, height: 844 },   // iPhone 14 logical size
    deviceScaleFactor: 2,
    ignoreHTTPSErrors: true,
  });

  const errors = [];
  page.on('console', m => m.type() === 'error' && errors.push(m.text()));
  page.on('pageerror', e => errors.push(String(e)));

  console.log('signing in...');
  await page.goto(APP, { waitUntil: 'networkidle' });
  await page.waitForTimeout(6000);

  // Skip goes straight to Login — it means "I know what this is", which is a returning user. It used
  // to land on Register, needing a second hop via the Log in link underneath.
  await page.mouse.click(360, 22);          // Skip (390-wide viewport)
  await page.waitForTimeout(2500);

  await page.mouse.click(195, 225);         // phone field
  await page.waitForTimeout(500);
  await page.keyboard.type(PHONE, { delay: 40 });
  await page.mouse.click(195, 311);         // Send code
  await page.waitForTimeout(4000);

  const otp = sql(`SELECT "Message" FROM "AppSmsLogs" ORDER BY "CreationTime" DESC LIMIT 1;`).replace(/\D/g, '');
  console.log(`  otp ${otp}`);

  // Six separate boxes — the first has to be focused before any digit lands.
  await page.mouse.click(48, 255);
  await page.waitForTimeout(400);
  // No Verify tap: the form submits itself once the sixth digit lands. Reaching /home from here is
  // what proves it.
  await page.keyboard.type(otp, { delay: 150 });
  await page.waitForTimeout(7000);

  await page.screenshot({ path: path.join(OUT, '00-after-login.png') });
  console.log(`  landed on ${page.url().split('#')[1] || '/'}`);

  for (const [name, route] of SCREENS) {
    await page.goto(`${APP}/#${route}`, { waitUntil: 'networkidle' });
    await page.waitForTimeout(2600);
    await page.screenshot({ path: path.join(OUT, `${name}.png`) });
    console.log(`  ${name}`);
  }

  console.log(errors.length ? `\nPAGE ERRORS:\n${[...new Set(errors)].slice(0, 8).join('\n')}` : '\nno page errors');
  await browser.close();
})().catch(e => { console.error('FAILED:', e.message); process.exit(1); });
