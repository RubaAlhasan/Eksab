import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export type StatusBadgeVariant = 'success' | 'warning' | 'danger' | 'info' | 'neutral';

/** Thin wrapper over a Bootstrap `.badge`, matching the `-subtle` background convention already used
 *  inline in admin-tenants.component.ts before this extraction. */
@Component({
  selector: 'app-status-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="badge {{ badgeClass() }}">{{ label() }}</span>`,
})
export class StatusBadgeComponent {
  readonly label = input.required<string>();
  readonly variant = input<StatusBadgeVariant>('neutral');

  protected readonly badgeClass = computed(() => {
    switch (this.variant()) {
      case 'success':
        return 'bg-success-subtle text-success-emphasis';
      case 'warning':
        return 'bg-warning-subtle text-warning-emphasis';
      case 'danger':
        return 'bg-danger-subtle text-danger-emphasis';
      case 'info':
        return 'bg-info-subtle text-info-emphasis';
      default:
        return 'bg-secondary-subtle text-secondary-emphasis';
    }
  });
}
