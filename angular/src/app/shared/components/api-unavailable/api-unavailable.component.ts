import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { HttpErrorWrapperComponent } from '@abp/ng.theme.shared';

/**
 * Shown instead of ABP's stock error screen when an HTTP call fails with status 0 — the browser
 * never got a response at all: API down, DNS/TLS failure, or no network.
 *
 * Registered through `withHttpErrorConfig({ errorScreen: … })` in app.config.ts, scoped to
 * status 0 only; 401/403/404/500 keep ABP's own handling, which is already reasonable.
 *
 * Why it is a full-screen OPAQUE overlay rather than a card: the failure that motivated this
 * happens during ABP's APP_INITIALIZER, before AppComponent renders, so the boot splash in
 * index.html is still on screen and every failed call creates its own error component. The
 * result was several transparent "An error has occurred" blocks stacked on top of the spinner,
 * overlapping and unreadable. An opaque fixed overlay hides the splash and makes duplicates
 * indistinguishable from a single one.
 *
 * Colours carry literal fallbacks because this can paint before the app stylesheet — and so
 * before tokens.scss — has loaded.
 */
@Component({
  selector: 'eks-api-unavailable',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="eks-err">
      <svg class="eks-err__mark" viewBox="0 0 48 48" aria-hidden="true">
        <defs>
          <linearGradient id="eksMarkErr" x1="0" y1="0" x2="1" y2="1">
            <stop offset="0" stop-color="#7c6af0" />
            <stop offset="1" stop-color="#4f37c4" />
          </linearGradient>
        </defs>
        <rect width="48" height="48" rx="13" fill="url(#eksMarkErr)" />
        <rect x="15" y="14" width="19" height="5" rx="2.5" fill="#fff" />
        <rect x="15" y="21.5" width="12" height="5" rx="2.5" fill="#fff" fill-opacity=".72" />
        <rect x="15" y="29" width="19" height="5" rx="2.5" fill="#fff" />
      </svg>

      <h1 class="eks-err__title">We can't reach the server</h1>
      <p class="eks-err__body">
        Eksabli loaded, but it couldn't connect to the service that holds your data. This is
        usually a dropped connection or a short maintenance window — your data isn't affected.
      </p>

      <div class="eks-err__actions">
        <button type="button" class="eks-err__btn" (click)="retry()">Try again</button>
        <span class="eks-err__auto">Retrying automatically in {{ countdown() }}s</span>
      </div>

      @if (technicalDetail(); as detail) {
        <details class="eks-err__tech">
          <summary>Technical details</summary>
          <code>{{ detail }}</code>
        </details>
      }
    </div>
  `,
  styles: [
    `
      .eks-err {
        position: fixed;
        inset: 0;
        z-index: 2000;
        display: flex;
        flex-direction: column;
        align-items: center;
        justify-content: center;
        gap: 14px;
        padding: 24px;
        text-align: center;
        background: var(--eks-bg, #faf9f8);
        color: var(--eks-text, #191817);
        font-family: system-ui, -apple-system, 'Segoe UI', Roboto, sans-serif;
      }
      .eks-err__mark { width: 56px; height: 56px; }
      .eks-err__title { margin: 4px 0 0; font-size: 1.35rem; font-weight: 650; }
      .eks-err__body {
        margin: 0;
        max-width: 42ch;
        line-height: 1.55;
        color: var(--eks-text-muted, #726f6d);
      }
      .eks-err__actions { display: flex; flex-direction: column; align-items: center; gap: 8px; }
      .eks-err__btn {
        border: 0;
        border-radius: 10px;
        padding: 10px 22px;
        font: inherit;
        font-weight: 600;
        cursor: pointer;
        color: var(--eks-primary-fg, #fff);
        background: var(--eks-primary, #6248e3);
      }
      .eks-err__btn:hover { background: var(--eks-primary-hover, #4f37c4); }
      .eks-err__btn:focus-visible { outline: 3px solid var(--eks-primary-ring, rgba(98, 72, 227, 0.45)); outline-offset: 2px; }
      .eks-err__auto { font-size: 0.82rem; color: var(--eks-text-muted, #726f6d); }
      .eks-err__tech {
        margin-top: 6px;
        max-width: 90vw;
        font-size: 0.78rem;
        color: var(--eks-text-muted, #726f6d);
      }
      .eks-err__tech summary { cursor: pointer; }
      .eks-err__tech code { display: block; margin-top: 6px; word-break: break-all; }
      @media (prefers-color-scheme: dark) {
        .eks-err:not([data-themed]) { background: #191817; color: #f4f3f1; }
      }
    `,
  ],
})
export class ApiUnavailableComponent {
  // Optional: the component is created inside HttpErrorWrapperComponent's view, but treat that
  // as an implementation detail of ABP rather than a guarantee.
  private readonly wrapper = inject(HttpErrorWrapperComponent, { optional: true });
  private readonly destroyRef = inject(DestroyRef);

  private static readonly RETRY_SECONDS = 15;

  readonly countdown = signal(ApiUnavailableComponent.RETRY_SECONDS);
  readonly technicalDetail = signal<string | null>(
    typeof this.wrapper?.details === 'string' ? this.wrapper.details : null,
  );

  constructor() {
    // A full reload is the right retry here: the failure happened in APP_INITIALIZER, so there
    // is no initialised app state to preserve and nothing useful to re-run piecemeal.
    const timer = setInterval(() => {
      const next = this.countdown() - 1;
      if (next <= 0) {
        this.retry();
        return;
      }
      this.countdown.set(next);
    }, 1000);

    this.destroyRef.onDestroy(() => clearInterval(timer));
  }

  retry(): void {
    window.location.reload();
  }
}
