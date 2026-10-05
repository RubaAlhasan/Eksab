import { ChangeDetectorRef, OnDestroy, Pipe, PipeTransform, inject } from '@angular/core';
import { SessionStateService } from '@abp/ng.core';

/**
 * Shows a name in the app's current language: the Arabic value when the UI is Arabic, the English one otherwise.
 * Either side falls back to the other when it is empty, so a reward or campaign that has only one language still shows.
 *
 * Impure on purpose: the language can change while the page is open, and OnPush views would otherwise keep the old
 * name. A language change marks the host view for check.
 */
@Pipe({
  name: 'localizedName',
  pure: false,
})
export class LocalizedNamePipe implements PipeTransform, OnDestroy {
  private readonly sessionState = inject(SessionStateService);
  private readonly cdr = inject(ChangeDetectorRef);

  private readonly languageSubscription = this.sessionState.getLanguage$().subscribe(() => this.cdr.markForCheck());

  transform(english: string | null | undefined, arabic: string | null | undefined): string {
    const isArabic = this.sessionState.getLanguage() === 'ar';
    return isArabic ? (arabic || english || '') : (english || arabic || '');
  }

  ngOnDestroy(): void {
    this.languageSubscription.unsubscribe();
  }
}
