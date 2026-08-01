import { LocalizationPipe } from '@abp/ng.core';
import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'abp-footer',
  template: `
    <div class="lpx-footbar-container end-0">
      <div class="lpx-footbar">
        <div class="lpx-footbar-copyright">
          <span>{{ currentYear }}© Eksabli</span>
        </div>
        <div class="lpx-footbar-solo-links">
          <a href="#">{{ '::About' | abpLocalization }}</a>
          <a href="#">{{ '::Privacy' | abpLocalization }}</a>
          <a href="#">{{ '::Contact' | abpLocalization }}</a>
        </div>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LocalizationPipe],
})
export class FooterComponent {
  currentYear = new Date().getFullYear();
}
