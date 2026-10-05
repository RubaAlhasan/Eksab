import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export interface TrendBar {
  label: string;
  value: number;
  // Shown on hover, so the exact figure is one pointer-move away.
  tooltip: string;
}

/**
 * A column chart over a zero-filled series. Heights are relative to the largest value in view. A zero value draws no
 * bar at all, and a small non-zero value still gets a visible sliver so it can't be mistaken for nothing.
 */
@Component({
  selector: 'app-bar-trend',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="eks-dash-bars" role="img" [attr.aria-label]="ariaLabel()">
      @for (bar of heights(); track $index) {
        <div class="eks-dash-bars__col" [title]="bar.tooltip">
          <div class="eks-dash-bars__track">
            <div class="eks-dash-bars__bar" [style.height.%]="bar.pct"></div>
          </div>
          <span class="eks-dash-bars__label">{{ bar.label }}</span>
        </div>
      }
    </div>
  `,
})
export class BarTrendComponent {
  readonly bars = input.required<TrendBar[]>();
  readonly ariaLabel = input('');

  protected readonly heights = computed(() => {
    const bars = this.bars();
    const max = Math.max(1, ...bars.map((b) => b.value));
    return bars.map((bar) => ({
      label: bar.label,
      tooltip: bar.tooltip,
      pct: bar.value > 0 ? Math.max(2, (bar.value / max) * 100) : 0,
    }));
  });
}
