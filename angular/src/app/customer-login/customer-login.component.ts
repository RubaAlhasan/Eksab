import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthService, ConfigStateService, LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { PhoneInputComponent } from '../shared/components/phone-input/phone-input.component';
import { OtpService } from '../proxy/controllers/otp.service';

type Step = 'phone' | 'code';

/**
 * Web login for a Host-realm customer (member) who doesn't want to install the mobile app — the
 * same phone + OTP flow the Flutter app uses (see docs/eksabli-loyalty-platform/05-flutter-
 * architecture.md#authentication and OtpLoginGrantHandler's own comment), reusing the exact
 * "otp" OpenIddict grant type the mobile app already authenticates with. Deliberately NOT the
 * password-based flow `LandingComponent.login()`/`AuthService.navigateToLogin()` sends platform
 * admins and business staff through — a customer has no password, only a verified phone number.
 *
 * Two-step form, no ReactiveForms/FormGroup (same "plain signals + native (submit)" shape as
 * business-points.component.ts's own phone-lookup form — see its onSubmit's comment for why
 * (ngSubmit) doesn't apply here): enter phone -> request a code -> enter the code -> exchange it
 * for a token via AuthService.loginUsingGrant.
 */
@Component({
  selector: 'app-customer-login',
  templateUrl: './customer-login.component.html',
  styleUrls: ['./customer-login.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LocalizationPipe, PhoneInputComponent, RouterLink],
})
export class CustomerLoginComponent {
  private readonly otpService = inject(OtpService);
  private readonly authService = inject(AuthService);
  private readonly configState = inject(ConfigStateService);
  private readonly toaster = inject(ToasterService);
  private readonly router = inject(Router);

  protected readonly step = signal<Step>('phone');
  protected readonly phoneNumber = signal('');
  protected readonly code = signal('');
  protected readonly isSubmitting = signal(false);

  protected onPhoneChange(value: string): void {
    this.phoneNumber.set(value);
  }

  protected onCodeInput(event: Event): void {
    this.code.set((event.target as HTMLInputElement).value.replace(/\D/g, ''));
  }

  // See business-points.component.ts's onSubmit for why plain (submit) + preventDefault() is used
  // instead of (ngSubmit) here too — neither step's form is a real FormGroup.
  protected onPhoneSubmit(event: Event): void {
    event.preventDefault();
    this.sendCode();
  }

  protected onCodeSubmit(event: Event): void {
    event.preventDefault();
    this.verifyCode();
  }

  protected changeNumber(): void {
    this.step.set('phone');
    this.code.set('');
  }

  protected resendCode(): void {
    this.sendCode();
  }

  private sendCode(): void {
    if (!this.phoneNumber() || this.isSubmitting()) return;
    this.isSubmitting.set(true);
    this.otpService.requestOtp({ phoneNumber: this.phoneNumber() }).subscribe({
      next: () => {
        this.isSubmitting.set(false);
        this.step.set('code');
      },
      // @abp/ng.core's RestService already surfaces a toast for a failed call on its own (see its
      // handleError, wired into every proxy service's .request() unless skipHandleError is passed) —
      // just reset the loading flag, same shape as every other component's error handlers in this app
      // (e.g. business-subscription.component.ts's changePlan()/getMyCurrentSubscription() handlers).
      error: () => this.isSubmitting.set(false),
    });
  }

  private verifyCode(): void {
    if (!this.code() || this.isSubmitting()) return;
    this.isSubmitting.set(true);

    // Unlike requestOtp above, loginUsingGrant posts straight through angular-oauth2-oidc's own
    // OAuthService.fetchTokenUsingGrant (plain HttpClient.post to the OpenIddict token endpoint) —
    // NOT through RestService.request() — so nothing auto-toasts a failure here; catch and show it
    // ourselves below.
    this.authService
      .loginUsingGrant('otp', { phone_number: this.phoneNumber(), otp_code: this.code() })
      .then(() => {
        // The access token just changed outside ABP's own login page flow (which calls this same
        // method after every successful sign-in) — currentUser/permissions/features in
        // ConfigStateService are still whatever an anonymous visitor saw. Refresh before routing
        // anywhere that reads them (the wallet view below, or permissionGuard on /admin or /business
        // for a staff account that happens to hit this page).
        this.configState.refreshAppState().subscribe(() => {
          this.isSubmitting.set(false);
          this.router.navigateByUrl('/home');
        });
      })
      .catch((err: unknown) => {
        this.isSubmitting.set(false);
        this.showLoginError(err);
      });
  }

  // OtpLoginGrantHandler.BuildErrorResult sends a plain OAuth error/error_description pair (see that
  // file) — "The code has expired."/"The code is invalid." for a bad grant, "...are required." for a
  // malformed request. Matched by content rather than the OAuth error code itself (both expired and
  // wrong-code responses use the same "invalid_grant" code, differing only in description) so this
  // still degrades to a generic message if the description ever changes shape.
  private showLoginError(err: unknown): void {
    const description = (err as { error?: { error_description?: string } })?.error?.error_description ?? '';
    const key = description.includes('expired')
      ? '::CustomerLogin:ErrorExpired'
      : description
        ? '::CustomerLogin:ErrorInvalidCode'
        : '::CustomerLogin:ErrorGeneric';
    this.toaster.error(key);
  }
}
