import { Injectable, signal } from '@angular/core';

/**
 * Holds the Customer Portal's dark-mode state outside `CustomerLayoutComponent` itself, since the
 * toggle control lives on a routed child (`customer-settings.component`), not the shell — unlike
 * `BusinessLayoutComponent`/`AdminLayoutComponent`, whose own toggle button sits in their own topbar,
 * the same component that holds the signal. In-memory only, no persistence, same as those two.
 */
@Injectable({ providedIn: 'root' })
export class CustomerThemeService {
  readonly darkMode = signal(false);

  toggle(): void {
    this.darkMode.update(dark => !dark);
  }
}
