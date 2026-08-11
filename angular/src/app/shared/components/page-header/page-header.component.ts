import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

export interface BreadcrumbItem {
  label: string;
  link?: string;
}

/** Title + subtitle + breadcrumb trail, with an `<ng-content>` slot for page-level action buttons
 *  (e.g. "New Category") so this stays a layout component, not a form/action-aware one. */
@Component({
  selector: 'app-page-header',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (breadcrumbs().length) {
      <nav aria-label="breadcrumb" class="mb-2">
        <ol class="breadcrumb mb-0 small">
          @for (item of breadcrumbs(); track item.label; let last = $last) {
            <li class="breadcrumb-item" [class.active]="last">
              @if (item.link && !last) {
                <a [routerLink]="item.link">{{ item.label }}</a>
              } @else {
                {{ item.label }}
              }
            </li>
          }
        </ol>
      </nav>
    }
    <div class="d-flex align-items-center justify-content-between flex-wrap gap-2 mb-4">
      <div>
        <h3 class="mb-1">{{ title() }}</h3>
        @if (subtitle()) {
          <p class="text-muted mb-0">{{ subtitle() }}</p>
        }
      </div>
      <ng-content></ng-content>
    </div>
  `,
  imports: [RouterLink],
})
export class PageHeaderComponent {
  readonly title = input.required<string>();
  readonly subtitle = input<string>();
  readonly breadcrumbs = input<BreadcrumbItem[]>([]);
}
