import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';
import { Meta } from '@angular/platform-browser';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs/operators';

// Authenticated portals and the pre-auth login/register pages. They must never be indexed. Matched by whole path
// segment, so '/customer' covers '/customer/home' but not '/customers'. Keep in step with PRIVATE_PATHS in
// scripts/generate-seo.mjs, which feeds robots.txt.
const PRIVATE_PATH_PREFIXES = ['/customer', '/customer-login', '/customer-register', '/admin', '/business', '/account'];

/**
 * Search-engine metadata for the client-rendered app: the configured public origin, canonical URLs, and robots
 * directives for each screen as it opens. The origin is APP_BASE_URL, written into index.html at build time by
 * scripts/generate-seo.mjs, so canonical links always point at the production domain rather than whichever host the
 * page was loaded from. Future public pages build their canonical URL with canonicalUrl().
 */
@Injectable({ providedIn: 'root' })
export class SeoService {
  private readonly document = inject(DOCUMENT);
  private readonly meta = inject(Meta);
  private readonly router = inject(Router);

  readonly baseUrl = this.resolveBaseUrl();

  constructor() {
    this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe(event => this.applyRobots(event.urlAfterRedirects));
  }

  /** Absolute public URL for an app path: canonicalUrl('/') gives https://<configured domain>/. */
  canonicalUrl(path: string): string {
    return `${this.baseUrl}${path.startsWith('/') ? path : `/${path}`}`;
  }

  /** True for the authenticated portals and the login/register pages. */
  isPrivatePath(url: string): boolean {
    const path = url.split(/[?#]/)[0];
    return PRIVATE_PATH_PREFIXES.some(prefix => path === prefix || path.startsWith(`${prefix}/`));
  }

  private applyRobots(url: string): void {
    this.meta.updateTag({ name: 'robots', content: this.isPrivatePath(url) ? 'noindex, nofollow' : 'index, follow' });
  }

  private resolveBaseUrl(): string {
    const configured = this.document.querySelector<HTMLMetaElement>('meta[name="app-base-url"]')?.content ?? '';
    // The build replaces the placeholder. It only survives on a local dev server, which has no build step.
    if (configured && !configured.startsWith('__')) return configured.replace(/\/$/, '');
    return this.document.location.origin;
  }
}
