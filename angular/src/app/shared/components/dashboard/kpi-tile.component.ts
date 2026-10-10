import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { AnimatedNumberComponent } from '../animated-number/animated-number.component';

export type KpiTone = 'primary' | 'success' | 'warning' | 'info' | 'danger';

/**
 * One headline figure on a dashboard. The number counts up to its value through the shared animated number. Currency
 * and percentages are passed as `suffix` text rather than folded into the number, so the figure stays a plain count.
 * A null value renders a dash, meaning "no data yet", which is different from zero.
 */
@Component({
  selector: 'app-kpi-tile',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [AnimatedNumberComponent],
  template: `
    <div class="card eks-dash-kpi h-100">
      <div class="card-body d-flex flex-column gap-2">
        <div class="d-flex align-items-center justify-content-between gap-2">
          <span class="eks-dash-kpi__label">{{ label() }}</span>
          <span [class]="iconClasses()"><i [class]="'fas ' + icon()"></i></span>
        </div>
        <div class="eks-dash-kpi__value">
          @if (value() === null) {
            <span class="eks-dash-kpi__empty">—</span>
          } @else {
            <app-animated-number [value]="value()!" />
          }
          @if (suffix(); as suffix) {
            <span class="eks-dash-kpi__suffix">{{ suffix }}</span>
          }
        </div>
        @if (caption(); as caption) {
          <p class="eks-dash-kpi__caption mb-0">{{ caption }}</p>
        }
      </div>
    </div>
  `,
})
export class KpiTileComponent {
  readonly label = input.required<string>();
  readonly value = input.required<number | null>();
  readonly icon = input.required<string>();
  readonly tone = input<KpiTone>('primary');
  readonly suffix = input<string | null>(null);
  readonly caption = input<string | null>(null);

  protected readonly iconClasses = computed(
    () => `eks-dash-kpi__icon bg-${this.tone()}-subtle text-${this.tone()}-emphasis`,
  );
}
