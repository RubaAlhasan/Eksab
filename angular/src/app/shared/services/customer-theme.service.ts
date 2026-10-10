import { Injectable, signal } from '@angular/core';

/**
 * Holds the Customer Portal's dark-mode state outside `CustomerLayoutComponent` itself, since the
 * toggle control lives on a routed child (`customer-settings.component`), not the shell — unlike
 * `BusinessLayoutComponent`/`AdminLayoutComponent`, whose own toggle button sits in their own topbar,
 * the same component that holds the signal. In-memory only, no persistence, same as those two.
 */
@Injectable({ providedIn: 'root' })
export class CustomerThemeService {
  // The choice is remembered on this device. Storage can be blocked (private windows, cleared site data); without it the
  // app simply starts in light mode, so it is only a convenience.
  readonly darkMode = signal(readStoredChoice());

  toggle(): void {
    this.darkMode.update(dark => !dark);
    writeStoredChoice(this.darkMode());
  }
}

const DARK_MODE_STORAGE_KEY = 'eks-customer-dark-mode';

function readStoredChoice(): boolean {
  try {
    return localStorage.getItem(DARK_MODE_STORAGE_KEY) === '1';
  } catch {
    return false;
  }
}

function writeStoredChoice(dark: boolean): void {
  try {
    localStorage.setItem(DARK_MODE_STORAGE_KEY, dark ? '1' : '0');
  } catch {
    // Storage unavailable: the in-memory choice still applies for this session.
  }
}
