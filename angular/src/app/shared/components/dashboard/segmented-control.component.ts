import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { LocalizationPipe } from '@abp/ng.core';

export interface SegmentedOption {
  value: string | number;
  labelKey: string;
}

/**
 * A small pill switch used for the range and currency filters on the dashboards. It is a radio group, so a keyboard
 * user can arrow through it and a screen reader announces the selection.
 */
@Component({
  selector: 'app-segmented-control',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LocalizationPipe],
  template: `
    <div class="eks-dash-seg" role="radiogroup" [attr.aria-label]="ariaLabel()">
      @for (option of options(); track option.value) {
        <button
          type="button"
          role="radio"
          class="eks-dash-seg__btn"
          [class.is-active]="option.value === selected()"
          [attr.aria-checked]="option.value === selected()"
          (click)="selectionChange.emit(option.value)"
        >
          {{ option.labelKey | abpLocalization }}
        </button>
      }
    </div>
  `,
})
export class SegmentedControlComponent {
  readonly options = input.required<SegmentedOption[]>();
  readonly selected = input.required<string | number>();
  readonly ariaLabel = input('');

  readonly selectionChange = output<string | number>();
}
