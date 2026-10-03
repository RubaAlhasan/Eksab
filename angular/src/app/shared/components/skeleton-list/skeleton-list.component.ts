import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/**
 * Content-shaped loading placeholder for a list, a tile grid or a single panel. Replaces the centred spinner on
 * list-heavy pages so the page shows its own shape while data arrives. The spinner stays for inline, button-level
 * loading. `label` is announced to screen readers; the shapes themselves are decorative.
 */
@Component({
  selector: 'app-skeleton-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div role="status">
      <span class="visually-hidden">{{ label() }}</span>
      @if (variant() === 'panel') {
        <div class="eks-skeleton eks-skeleton-panel" aria-hidden="true"></div>
      } @else if (variant() === 'grid') {
        <div class="eks-tile-grid" aria-hidden="true">
          @for (item of items(); track item) {
            <div class="eks-skeleton eks-skeleton-tile"></div>
          }
        </div>
      } @else {
        <div class="eks-list" aria-hidden="true">
          @for (item of items(); track item) {
            <div class="eks-skeleton eks-skeleton-row"></div>
          }
        </div>
      }
    </div>
  `,
})
export class SkeletonListComponent {
  readonly label = input.required<string>();
  readonly variant = input<'list' | 'grid' | 'panel'>('list');
  readonly count = input(4);

  protected readonly items = computed(() => Array.from({ length: this.count() }, (_, index) => index));
}
