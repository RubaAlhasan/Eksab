// Writes robots.txt and sitemap.xml into the Angular build output, and fills the __APP_BASE_URL__ placeholders in
// index.html. Everything is derived from one configured value, APP_BASE_URL (for example https://example.com).
// No domain is guessed: a missing or malformed value fails the build instead of shipping a wrong canonical URL.
//
// Run through `npm run build` / `npm run build:prod`, which call this after the Angular build.

import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { join } from 'node:path';

const OUTPUT_DIR = fileURLToPath(new URL('../dist/Eksabli/browser/', import.meta.url));
const PLACEHOLDER = '__APP_BASE_URL__';

// Authenticated portals and the pre-auth login/register pages. Disallowed for crawlers, and marked noindex by the
// SeoService at runtime. Keep this list in step with PRIVATE_PATH_PREFIXES in shared/services/seo.service.ts.
export const PRIVATE_PATHS = ['/customer', '/customer-login', '/customer-register', '/admin', '/business', '/account'];

// Public paths listed in the sitemap. Future public Business, Offer and Reward pages add their paths here, or are
// fetched from the API at build time, so the sitemap is regenerated on every deploy.
export function publicPaths() {
  return ['/'];
}

function fail(message) {
  console.error(`generate-seo: ${message}`);
  process.exit(1);
}

function resolveBaseUrl() {
  const raw = process.env.APP_BASE_URL?.trim();
  if (!raw) {
    fail('APP_BASE_URL is not set. Set it to the public site address, for example https://example.com');
  }

  let url;
  try {
    url = new URL(raw);
  } catch {
    fail(`APP_BASE_URL is not a valid absolute URL: ${raw}`);
  }

  if (url.protocol !== 'https:' && url.protocol !== 'http:') {
    fail(`APP_BASE_URL must use http or https: ${raw}`);
  }
  if (url.pathname !== '/' || url.search || url.hash) {
    fail('APP_BASE_URL must be the origin only, with no path, query or hash.');
  }

  // url.origin has no trailing slash, so `${base}${path}` always produces exactly one slash.
  return url.origin;
}

function escapeXml(value) {
  return value.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

function robotsTxt(base) {
  return [
    'User-agent: *',
    ...PRIVATE_PATHS.map(path => `Disallow: ${path}`),
    '',
    `Sitemap: ${base}/sitemap.xml`,
    '',
  ].join('\n');
}

function sitemapXml(base) {
  const lastmod = new Date().toISOString().slice(0, 10);
  const entries = publicPaths()
    .map(path => `  <url>\n    <loc>${escapeXml(`${base}${path}`)}</loc>\n    <lastmod>${lastmod}</lastmod>\n  </url>`)
    .join('\n');
  return `<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n${entries}\n</urlset>\n`;
}

const base = resolveBaseUrl();

const indexPath = join(OUTPUT_DIR, 'index.html');
if (!existsSync(indexPath)) {
  fail(`Build output not found at ${OUTPUT_DIR}. Run the Angular build first.`);
}

const html = readFileSync(indexPath, 'utf8');
if (!html.includes(PLACEHOLDER)) {
  fail(`index.html has no ${PLACEHOLDER} placeholder, so the canonical URLs would be wrong.`);
}
writeFileSync(indexPath, html.replaceAll(PLACEHOLDER, base));
writeFileSync(join(OUTPUT_DIR, 'robots.txt'), robotsTxt(base));
writeFileSync(join(OUTPUT_DIR, 'sitemap.xml'), sitemapXml(base));

console.log(`generate-seo: wrote robots.txt, sitemap.xml and index.html for ${base}`);
