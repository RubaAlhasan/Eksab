import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'app-loading-spinner',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="text-center py-5 text-muted">
      <span class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>
      {{ label() }}
    </div>
  `,
})
export class LoadingSpinnerComponent {
  readonly label = input('Loading…');
}
