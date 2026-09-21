import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormControl, FormGroup, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { AuthService, ConfigStateService, LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { OAuthService } from 'angular-oauth2-oidc';
import { PhoneInputComponent } from '../shared/components/phone-input/phone-input.component';
import { OtpService } from '../proxy/controllers/otp.service';
import { CustomerGender } from '../proxy/customer-profiles/customer-gender.enum';

type Step = 'form' | 'code';

// Matches ASP.NET Core Identity's default password policy — confirmed empirically against the real
// backend (not documented anywhere in this repo's own code, since it's configured via ABP Setting
// Management, not a hardcoded value): a plain `Validators.minLength(8)` password like "TestPass123"
// passes this frontend check but the server rejects it with "must have at least one non alphanumeric
// character." Checking the actual policy here means a bad password fails obviously, at the field, before
// the round trip — not as a same-request-every-time RestService toast the user has to connect back to
// "which field was wrong."
const passwordPolicyValidator: ValidatorFn = control => {
  const value = (control.value as string) ?? '';
  const valid = /[a-z]/.test(value) && /[A-Z]/.test(value) && /\d/.test(value) && /[^a-zA-Z0-9]/.test(value);
  return valid ? null : { passwordPolicy: true };
};

/**
 * Web sign-up for a new Host-realm customer — matches prototype/customer/register.html's own field set
 * exactly (see `RegisterCustomerDto`'s own comment on the backend): first/last name, phone, optional
 * email, optional date of birth, optional gender, password. No "I agree to the Terms of Service/Privacy
 * Policy" checkbox — the prototype has one, but there are no real legal pages anywhere in this app to
 * link to or agree to (confirmed by search), so it would be exactly the kind of fake affordance this
 * app's own convention avoids (no star ratings, no branch address lists, etc.). Date of birth is
 * genuinely optional here, not required like the prototype's own stricter mock validation — the backend
 * DTO itself only requires first/last name, phone, and password.
 *
 * `OtpAppService.RegisterAsync` (POST /api/app/otp/register) creates the account AND sends the OTP code
 * in one call (see that DTO's own comment: "this call also sends the OTP code itself... rather than
 * requiring a separate POST /api/app/otp/request first") — so this is a two-step page like
 * customer-login.component.ts (fill in details -> enter the code), not three. The code-entry step and
 * its token exchange are intentionally a near-duplicate of customer-login.component.ts's own
 * verifyCode() (same stale-OAuth-state fix, same error-message matching) rather than a shared
 * abstraction — matches this app's own established precedent of not retrofitting already-shipped pages
 * when a second real consumer appears (see reward-display.util.ts's file comment for the same reasoning
 * applied to business-rewards.component.ts).
 */
@Component({
  selector: 'app-customer-register',
  templateUrl: './customer-register.component.html',
  styleUrls: ['./customer-register.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, LocalizationPipe, PhoneInputComponent, RouterLink],
})
export class CustomerRegisterComponent {
  private readonly otpService = inject(OtpService);
  private readonly authService = inject(AuthService);
  private readonly configState = inject(ConfigStateService);
  private readonly toaster = inject(ToasterService);
  private readonly router = inject(Router);
  private readonly oAuthService = inject(OAuthService);

  protected readonly Gender = CustomerGender;
  protected readonly step = signal<Step>('form');
  protected readonly phoneNumber = signal('');
  protected readonly code = signal('');
  protected readonly isSubmitting = signal(false);
  protected readonly showPassword = signal(false);

  protected readonly form = new FormGroup({
    firstName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(64)] }),
    lastName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(64)] }),
    email: new FormControl('', { nonNullable: true, validators: [Validators.email] }),
    dateOfBirth: new FormControl('', { nonNullable: true }),
    gender: new FormControl<CustomerGender | null>(null),
    password: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(8), passwordPolicyValidator],
    }),
  });

  protected onPhoneChange(value: string): void {
    this.phoneNumber.set(value);
  }

  protected togglePasswordVisibility(): void {
    this.showPassword.update(shown => !shown);
  }

  protected onCodeInput(event: Event): void {
    this.code.set((event.target as HTMLInputElement).value.replace(/\D/g, ''));
  }

  // See business-points.component.ts's onSubmit (and customer-login.component.ts's own identical
  // comment) for why plain (submit) + preventDefault() is used instead of (ngSubmit) for the code step —
  // that one isn't a real FormGroup.
  protected onFormSubmit(event: Event): void {
    event.preventDefault();
    this.register();
  }

  protected onCodeSubmit(event: Event): void {
    event.preventDefault();
    this.verifyCode();
  }

  protected editDetails(): void {
    this.step.set('form');
    this.code.set('');
  }

  protected resendCode(): void {
    this.register();
  }

  private register(): void {
    if (this.form.invalid || !this.phoneNumber() || this.isSubmitting()) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.isSubmitting.set(true);
    this.otpService
      .register({
        phoneNumber: this.phoneNumber(),
        firstName: value.firstName,
        lastName: value.lastName,
        email: value.email || null,
        dateOfBirth: value.dateOfBirth ? new Date(value.dateOfBirth).toISOString() : null,
        gender: value.gender ?? undefined,
        password: value.password,
      })
      .subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.step.set('code');
        },
        // RestService already surfaces a toast for a failed call on its own (e.g. "This phone number is
        // already registered") — same shape as every other component's error handlers in this app.
        error: () => this.isSubmitting.set(false),
      });
  }

  // Identical to customer-login.component.ts's own verifyCode() — see that file's own comment for the
  // full reasoning behind clearing OAuthService.state before the grant request.
  private verifyCode(): void {
    if (!this.code() || this.isSubmitting()) return;
    this.isSubmitting.set(true);

    this.oAuthService.state = '';

    this.authService
      .loginUsingGrant('otp', { phone_number: this.phoneNumber(), otp_code: this.code() })
      .then(() => {
        this.configState.refreshAppState().subscribe(() => {
          this.isSubmitting.set(false);
          this.router.navigateByUrl('/customer');
        });
      })
      .catch((err: unknown) => {
        this.isSubmitting.set(false);
        this.showVerifyError(err);
      });
  }

  private showVerifyError(err: unknown): void {
    const description = (err as { error?: { error_description?: string } })?.error?.error_description ?? '';
    const key = description.includes('expired')
      ? '::CustomerLogin:ErrorExpired'
      : description
        ? '::CustomerLogin:ErrorInvalidCode'
        : '::CustomerLogin:ErrorGeneric';
    this.toaster.error(key);
  }
}
