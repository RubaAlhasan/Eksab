import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Used inside a page's own `@if` chain for the "list loaded, zero rows" case — not a wrapper around
 *  the list itself, so it composes with any table/grid markup a page already has. */
@Component({
  selector: 'app-empty-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="text-center py-5">
      <i class="fas {{ icon() }} text-muted mb-3" style="font-size: 2rem"></i>
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
