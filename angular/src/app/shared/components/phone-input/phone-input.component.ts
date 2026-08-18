import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { SessionStateService } from '@abp/ng.core';
import {
  getCountries,
  getCountryCallingCode,
  getExampleNumber,
  parsePhoneNumberFromString,
  type CountryCode,
} from 'libphonenumber-js/min';
import examples from 'libphonenumber-js/examples.mobile.json';

interface CountryOption {
  code: CountryCode;
  name: string;
  callingCode: string;
}

// Default dial-in country per product request (Syria, +963) — a sensible default, not a hard
// assumption; every other country is still one dropdown click away.
const DEFAULT_COUNTRY: CountryCode = 'SY';

/**
 * Country-code select + local-number field, combined into the single normalized
 * "+<callingCode><digits>" string `PhoneNumberNormalizer.cs`/`IdentityUser.PhoneNumber` already expect
 * (see that file's own comment for why the backend treats phone number as one canonical string — this
 * component only changes how a human *types* that string, not how it's stored/validated/looked-up).
 *
 * Country list + calling codes come from `libphonenumber-js` (metadata only — no bundled UI, so it
 * doesn't drag in a second component/CSS framework the way `ngx-intl-tel-input` (needs `ngx-bootstrap`)
 * or `ngx-mat-intl-tel-input` (needs Angular Material) would; this app is plain-Bootstrap/Lepton-X
 * throughout, not either of those). Country *display names* come from the browser's own
 * `Intl.DisplayNames` — localizes to 'ar' for free (no bundled name-translation data file needed),
 * matching the current app language via `SessionStateService`.
 *
 * The local-number field is forced `dir="ltr"` even under an Arabic layout — matches the documented RTL
 * gotcha in `docs/eksabli-loyalty-platform/05-flutter-architecture.md#localization`: digits stay
 * LTR-shaped even when the surrounding layout mirrors, and letting the input itself flip produces a
 * confusing cursor/typing experience for a field that's pure digits.
 */
@Component({
  selector: 'app-phone-input',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="d-flex gap-2">
      <select
        class="form-select"
        style="max-width: 10rem"
        (change)="onCountryChange($event)"
        [attr.aria-label]="countryAriaLabel()"
      >
        @for (country of countries(); track country.code) {
          <!-- [selected] on each <option>, not [value] on the <select> itself — a plain (non-ngModel)
               [value] binding on a native <select> races the @for block's own rendering of its
               <option> children: the browser can't select a value that has no matching <option> yet,
               so it silently falls back to the first option (alphabetically "Afghanistan") instead of
               the intended default once the real options land a tick later. [selected] on the option
               that actually matches sidesteps that timing entirely. -->
          <option [value]="country.code" [selected]="country.code === selectedCountry()">
            {{ country.name }} (+{{ country.callingCode }})
          </option>
        }
      </select>
      <input
        type="tel"
        class="form-control"
        dir="ltr"
        [value]="nationalNumber()"
        (input)="onNumberInput($event)"
        [placeholder]="numberPlaceholder()"
      />
    </div>
  `,
})
export class PhoneInputComponent {
  private readonly sessionState = inject(SessionStateService);

  readonly placeholder = input('');
  readonly countryAriaLabel = input('Country code');
  readonly valueChange = output<string>();

  protected readonly countries = computed<CountryOption[]>(() => {
    const displayNames = new Intl.DisplayNames([this.sessionState.getLanguage() || 'en'], { type: 'region' });
    return getCountries()
      .map((code) => ({ code, name: displayNames.of(code) ?? code, callingCode: getCountryCallingCode(code) }))
      .sort((a, b) => a.name.localeCompare(b.name));
  });

  protected readonly selectedCountry = signal<CountryCode>(DEFAULT_COUNTRY);
  protected readonly nationalNumber = signal('');

  // A real, correctly-shaped example for whichever country is currently selected (e.g. Syria ->
  // "0944 567 890", UAE -> "050 123 4567") — generated via libphonenumber-js's own example-number
  // metadata, not a hardcoded string. Fixes the exact bug the country default previously had: a static
  // placeholder baked into localization (e.g. always showing a UAE-shaped "+971 50 123 4567") stayed on
  // screen regardless of which country was actually selected, making it look like the default hadn't
  // taken effect even though it had. Falls back to the `placeholder` input if one is explicitly passed.
  protected readonly numberPlaceholder = computed(() => {
    const override = this.placeholder();
    if (override) return override;
    try {
      return getExampleNumber(this.selectedCountry(), examples)?.formatNational() ?? '';
    } catch {
      return '';
    }
  });

  protected onCountryChange(event: Event): void {
    this.selectedCountry.set((event.target as HTMLSelectElement).value as CountryCode);
    this.emit();
  }

  protected onNumberInput(event: Event): void {
    this.nationalNumber.set((event.target as HTMLInputElement).value);
    this.emit();
  }

  // Parses the typed *national/local* number (whatever trunk-prefix convention the selected country
  // uses — e.g. a leading "0" for Saudi Arabia's "0501112222") against that country's real dialing
  // rules and produces the correct E.164 string ("+966501112222", trunk "0" stripped). Deliberately NOT
  // a naive `+callingCode + digits` concatenation — that would keep the trunk "0", producing
  // "+9660501112222" (an extra digit, a genuinely different phone number to an exact-match DB lookup),
  // which is exactly the bug this replaced: confirmed live, this session, against the real seeded
  // customer (+966501112222) — a naive concat of country "SA" + typed "0501112222" did not match it.
  private emit(): void {
    const raw = this.nationalNumber();
    const digits = raw.replace(/\D/g, '');
    if (!digits) {
      this.valueChange.emit('');
      return;
    }

    const parsed = parsePhoneNumberFromString(raw, this.selectedCountry());
    if (parsed) {
      this.valueChange.emit(parsed.number);
      return;
    }

    // Fell back only if libphonenumber-js couldn't make sense of the input at all (e.g. too short
    // while still typing) — naive concatenation as a last resort so something is still emitted rather
    // than nothing, not treated as the normal/correct path.
    this.valueChange.emit(`+${getCountryCallingCode(this.selectedCountry())}${digits}`);
  }
}
