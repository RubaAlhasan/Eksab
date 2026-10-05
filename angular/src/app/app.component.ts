import { Component, inject } from '@angular/core';
import { DynamicLayoutComponent } from '@abp/ng.core';
import { LoaderBarComponent } from '@abp/ng.theme.shared';
import { SeoService } from './shared/services/seo.service';

@Component({
  selector: 'app-root',
  template: `
    <abp-loader-bar />
    <abp-dynamic-layout />
  `,
  imports: [LoaderBarComponent, DynamicLayoutComponent],
})
export class AppComponent {
  constructor() {
    // Starts tracking navigation, so each private screen is marked noindex as it opens.
    inject(SeoService);
  }
}
