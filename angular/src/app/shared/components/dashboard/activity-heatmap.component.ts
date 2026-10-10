import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export interface ActivityCell {
  // 0 = Sunday ... 6 = Saturday, matching System.DayOfWeek on the server.
  dayOfWeek: number;
  hour: number;
  activity: number;
}

/**
 * A 7 x 24 grid of activity by weekday and hour. Intensity is relative to the busiest cell. Any cell with activity gets
 * a floor, so a quiet hour still reads as "something happened" rather than "nothing happened".
 */
@Component({
  selector: 'app-activity-heatmap',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="eks-dash-heat" role="img" [attr.aria-label]="ariaLabel()">
      <div class="eks-dash-heat__grid">
        <span></span>
        @for (hour of hours; track hour) {
          <span class="eks-dash-heat__hour">{{ hour % 3 === 0 ? hour : '' }}</span>
        }
        @for (row of rows(); track row.dayOfWeek) {
          <span class="eks-dash-heat__day">{{ dayLabels()[row.dayOfWeek] }}</span>
          @for (cell of row.cells; track cell.hour) {
            <span
              class="eks-dash-heat__cell"
              [style.--eks-heat]="cell.intensity"
              [title]="dayLabels()[row.dayOfWeek] + ' ' + pad(cell.hour) + ':00 · ' + cell.activity"
            ></span>
          }
        }
      </div>
    </div>
  `,
})
export class ActivityHeatmapComponent {
  readonly cells = input.required<ActivityCell[]>();
  readonly dayLabels = input.required<string[]>();
  readonly ariaLabel = input('');

  protected readonly hours = Array.from({ length: 24 }, (_, hour) => hour);

  protected readonly rows = computed(() => {
    const cells = this.cells();
    const max = Math.max(1, ...cells.map((c) => c.activity));
    return Array.from({ length: 7 }, (_, dayOfWeek) => ({
      dayOfWeek,
      cells: this.hours.map((hour) => {
        const activity = cells.find((c) => c.dayOfWeek === dayOfWeek && c.hour === hour)?.activity ?? 0;
        return {
          hour,
          activity,
          intensity: activity > 0 ? Math.max(0.12, activity / max) : 0,
        };
      }),
    }));
  });

  protected pad(hour: number): string {
    return hour.toString().padStart(2, '0');
  }
}
