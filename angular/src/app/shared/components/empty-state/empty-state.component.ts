import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Used inside a page's own `@if` chain for the "list loaded, zero rows" case — not a wrapper around
 *  the list itself, so it composes with any table/grid markup a page already has.
 *
 *  Icon box matches the prototype's own `.empty-state-icon` (design-system.css) — a shared,
 *  page-agnostic style used identically across every prototype page's empty state, so fixing it here
 *  once (rather than per-page) is the correct translation, same as `.eks-filter-select`/`.eks-stat-card`
 *  being promoted to global utilities. Uses Bootstrap's `bg-primary-subtle`/`text-primary-emphasis`
 *  convention instead of the prototype's literal `#F4F3FF`/`#6248E3` hex, matching every other colored
 *  icon treatment already translated that way in this app (stat card icons, quick-link icons). */
@Component({
  selector: 'app-empty-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="text-center py-5">
      <div
        class="d-inline-flex align-items-center justify-content-center rounded-3 bg-primary-subtle text-primary-emphasis mb-3"
        style="width: 3.5rem; height: 3.5rem; font-size: 1.5rem"
      >
        <i class="fas {{ icon() }}"></i>
      </div>
      <p class="fw-semibold mb-1">{{ title() }}</p>
      @if (description()) {
        <p class="text-muted small mb-3">{{ description() }}</p>
      }
      <ng-content></ng-content>
    </div>
  `,
})
export class EmptyStateComponent {
  readonly icon = input('fa-inbox');
  readonly title = input.required<string>();
  readonly description = input<string>();
}
