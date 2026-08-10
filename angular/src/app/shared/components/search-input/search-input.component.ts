import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

/** Debounced free-text filter input. Plain DOM event + a local timer (matches the pattern already
 *  proven in admin-tenants.component before this extraction) rather than FormsModule/ngModel, so pages
 *  using it don't need to pull in template-driven forms for a single field.
 *
 *  Leading search icon matches every prototype page's own search box (`.input.pl-9` + an absolutely
 *  positioned icon, e.g. customers.html/coupons.html) — was missing here, so every one of this shared
 *  component's 5 usages (Customers, Audit Logs, Users, Businesses, Categories) rendered as a bare input. */
@Component({
  selector: 'app-search-input',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="position-relative">
      <i class="fas fa-search position-absolute top-50 translate-middle-y text-muted" style="left: 0.75rem; font-size: 0.8125rem; pointer-events: none"></i>
      <input
        type="text"
        class="form-control"
        style="padding-left: 2.25rem"
        [placeholder]="placeholder()"
        [value]="value()"
        (input)="onInput($event)"
      />
    </div>
  `,
})
export class SearchInputComponent {
  readonly placeholder = input('Search…');
  readonly value = input('');
  readonly debounceMs = input(300);
  readonly valueChange = output<string>();

  private debounceTimer?: ReturnType<typeof setTimeout>;

  protected onInput(event: Event): void {
    const next = (event.target as HTMLInputElement).value;
    clearTimeout(this.debounceTimer);
    this.debounceTimer = setTimeout(() => this.valueChange.emit(next), this.debounceMs());
  }
}
