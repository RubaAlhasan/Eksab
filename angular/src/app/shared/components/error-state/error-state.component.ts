import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

@Component({
  selector: 'app-error-state',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="text-center py-5">
      <p class="text-danger mb-2">{{ message() }}</p>
      <button type="button" class="btn btn-sm btn-outline-secondary" (click)="retry.emit()">
        {{ retryLabel() }}
      </button>
    </div>
  `,
})
export class ErrorStateComponent {
  readonly message = input.required<string>();
  readonly retryLabel = input('Try again');
  readonly retry = output<void>();
}
