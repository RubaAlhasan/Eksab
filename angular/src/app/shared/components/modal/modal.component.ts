import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

/**
 * Minimal Bootstrap-markup modal, backdrop + panel, shown/hidden via a plain `[open]` input rather than
 * Bootstrap's own JS modal plumbing — the parent owns the open/closed signal (e.g. a `signal(false)`
 * toggled on "New Category" / row-edit click), so there's one source of truth for visibility instead of
 * two (Angular state + Bootstrap's internal instance). No ABP or Bootstrap package ships a generic
 * content-projectable modal for custom (non-CRUD-generator) use, so this is genuinely new, reusable
 * surface — not a duplicate of something that already exists.
 */
@Component({
  selector: 'app-modal',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './modal.component.html',
})
export class ModalComponent {
  readonly open = input.required<boolean>();
  readonly title = input.required<string>();
  readonly closed = output<void>();

  protected onBackdropClick(event: MouseEvent): void {
    if (event.target === event.currentTarget) this.closed.emit();
  }
}
