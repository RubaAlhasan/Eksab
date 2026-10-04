import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { LocalizationPipe } from '@abp/ng.core';

export type SmartTimelineTone = 'default' | 'live' | 'invalid';

/** One stage on the 24-hour track. Minutes since local midnight, end exclusive, so 24:00 is 1440. */
export interface SmartTimelineSegment {
  startMinute: number;
  endMinute: number;
  label: string;
  tone?: SmartTimelineTone;
}

/**
 * A 24-hour track showing when each stage runs. It only lays out times the caller already has; it does not decide
 * which price applies at any moment. That is the server's job, and the live/now marker is supplied by the caller.
 */
@Component({
  selector: 'app-smart-timeline',
  templateUrl: './smart-timeline.component.html',
  styleUrls: ['./smart-timeline.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LocalizationPipe],
})
export class SmartTimelineComponent {
  readonly segments = input<SmartTimelineSegment[]>([]);

  /** Minute of day to mark on the track, or null for no marker. */
  readonly nowMinute = input<number | null>(null);

  protected readonly positioned = computed(() =>
    this.segments()
      .filter(segment => segment.endMinute > segment.startMinute)
      .map(segment => ({
        left: (segment.startMinute / 1440) * 100,
        width: ((segment.endMinute - segment.startMinute) / 1440) * 100,
        label: segment.label,
        tone: segment.tone ?? 'default',
      })),
  );

  protected readonly nowPercent = computed(() => {
    const minute = this.nowMinute();
    return minute === null ? null : (minute / 1440) * 100;
  });
}
