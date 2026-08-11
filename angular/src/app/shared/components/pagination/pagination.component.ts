import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

/** Real prev/next pagination (0-based pageIndex), replacing the hand-rolled version that lived inline
 *  in admin-tenants.component before this extraction — same behavior, now reusable. */
@Component({
  selector: 'app-pagination',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (totalPages() > 1) {
      <div class="d-flex align-items-center justify-content-between">
        <span class="text-muted small">Page {{ pageIndex() + 1 }} of {{ totalPages() }}</span>
        <div class="btn-group">
          <button
            type="button"
            class="btn btn-sm btn-outline-secondary"
            [disabled]="pageIndex() === 0"
            (click)="pageChange.emit(pageIndex() - 1)"
          >
            ‹
          </button>
          <button
            type="button"
            class="btn btn-sm btn-outline-secondary"
            [disabled]="pageIndex() >= totalPages() - 1"
            (click)="pageChange.emit(pageIndex() + 1)"
          >
            ›
          </button>
        </div>
      </div>
    }
  `,
})
export class PaginationComponent {
  readonly pageIndex = input.required<number>();
  readonly totalPages = input.required<number>();
  readonly pageChange = output<number>();
}
