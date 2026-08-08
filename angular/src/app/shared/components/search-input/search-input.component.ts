import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

/** Debounced free-text filter input. Plain DOM event + a local timer (matches the pattern already
 *  proven in admin-tenants.component before this extraction) rather than FormsModule/ngModel, so pages
 *  using it don't need to pull in template-driven forms for a single field. */
@Component({
  selector: 'app-search-input',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <input
      type="text"
      class="form-control"
      [placeholder]="placeholder()"
      [value]="value()"
      (input)="onInput($event)"
    />
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
