import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { LocalizationPipe } from '@abp/ng.core';

/** Real prev/next pagination (0-based pageIndex), replacing the hand-rolled version that lived inline
 *  in admin-tenants.component before this extraction — same behavior, now reusable. The "Page X of Y"
 *  text used to be localized there (`AdminPanel:Businesses:PageOf`, now dead/unused — confirmed by
 *  grep) but came out hardcoded English on extraction; fixed via a new cross-portal `Shared:PageOf`
 *  key rather than reusing that admin-specific one, since this component is shared by all three
 *  portals, not just Admin. */
@Component({
  selector: 'app-pagination',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LocalizationPipe],
  template: `
    @if (totalPages() > 1) {
      <div class="d-flex align-items-center justify-content-between">
        <span class="text-muted small">
          {{ '::Shared:PageOf' | abpLocalization: (pageIndex() + 1).toString() : totalPages().toString() }}
        </span>
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
