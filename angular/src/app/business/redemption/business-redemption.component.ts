import { ChangeDetectionStrategy, Component, OnDestroy, computed, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { PosService } from '../../proxy/controllers/pos.service';
import type {
  RedemptionConfirmationDto,
  RedemptionLookupDto,
  RedemptionRejectionDto,
} from '../../proxy/pos/models';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { QrScannerComponent, QrScanErrorReason } from '../../shared/components/qr-scanner/qr-scanner.component';

type IdentifyMode = 'qr' | 'code';
type Phase = 'idle' | 'review' | 'approved' | 'declined';

/**
 * Business Portal > Redemption — the counter-side half of the reward redemption flow.
 *
 * **This page is the reason the flow works at all.** `PosController.confirmRedemption` existed from
 * the start but nothing in either portal ever called it, so a customer could open a redemption and no
 * staff member had any way to complete it. Every other piece (the customer app's code, the reservation,
 * the audit trail) was waiting on this screen.
 *
 * **The flow it drives** — see `CouponAppService.RedeemAsync` and `PosAppService` for the server half:
 *
 *   1. The customer taps Redeem in the mobile app. That RESERVES their points (`PointsWallet.Reserved`)
 *      and creates a `Pending` coupon. Nothing has been spent yet.
 *   2. They show the 8-character code — as a QR, or read aloud / typed from the same screen.
 *   3. This page looks it up (`lookupRedemption`, read-only) and shows staff who and what.
 *   4. Staff **Approve** — the hold becomes a real debit — or **Decline**, which hands the points
 *      straight back. Walking away is also safe: `RedemptionReservationWorker` releases the hold
 *      within ~20 minutes.
 *
 * **Why lookup is a separate step from approve.** Approval is a judgement, not a formality: staff
 * confirm the person in front of them, that the branch can actually hand the reward over, and — above
 * a reward's `ApprovalThresholdPoints` — that they are senior enough to authorise it. Scanning straight
 * into a debit removes the only moment where a mistake is still free. It also makes a mis-scan
 * harmless, which matters when the camera is pointed at a phone in someone else's hand.
 *
 * **Two input modes, one code.** The QR payload IS the code — not a separate token — so scanning and
 * typing hit the same endpoint with the same string. Scanning is a convenience, never a requirement:
 * this runs on counter hardware that may have no camera, no permission granted, or a customer whose
 * screen is too cracked or dim to read. Manual entry is a peer of the scanner here, not a buried
 * fallback, and the field accepts the spaced form (`3B02 543F`) the customer app displays.
 *
 * **Role handling is deliberately asymmetric.** `canCurrentEmployeeApprove` comes back from the lookup
 * and disables Approve with a "needs a manager" explanation rather than letting a cashier press it and
 * eat an opaque 403. Decline stays enabled for everyone: refusing to hand something over is never the
 * privileged direction, and making a cashier hunt for a manager just to give a customer their points
 * back would be perverse.
 *
 * No `data.requiredPolicy` on the route, for the same reason as Points Management: `PosController` has
 * no ABP permission at all and is gated by `PosAppService.CheckStaffRoleAsync` reading the caller's own
 * `EmployeeAssignment.Role`. Invited staff hold zero ABP permission grants, so a `permissionGuard` here
 * would lock out every real cashier. See `business-points.component.ts`'s file comment for the full
 * reasoning.
 */
@Component({
  selector: 'app-business-redemption',
  templateUrl: './business-redemption.component.html',
  styleUrls: ['./business-redemption.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, DecimalPipe, LocalizationPipe, PageHeaderComponent, QrScannerComponent],
})
export class BusinessRedemptionComponent implements OnDestroy {
  private readonly posService = inject(PosService);

  protected readonly identifyMode = signal<IdentifyMode>('qr');
  protected readonly phase = signal<Phase>('idle');

  protected readonly isLookingUp = signal(false);
  protected readonly isApproving = signal(false);
  protected readonly isDeclining = signal(false);
  protected readonly busy = computed(() => this.isLookingUp() || this.isApproving() || this.isDeclining());

  protected readonly lookup = signal<RedemptionLookupDto | null>(null);
  protected readonly approved = signal<RedemptionConfirmationDto | null>(null);
  protected readonly declined = signal<RedemptionRejectionDto | null>(null);
  protected readonly cameraErrorKey = signal<string | null>(null);

  protected readonly showDeclineReason = signal(false);

  // Codes are 8 hex characters. The pattern allows spaces so the customer app's grouped display
  // (`3B02 543F`) can be typed straight in; `normalize` strips them before the call, and the server
  // normalizes again independently rather than trusting this.
  //
  // Wrapped in a FormGroup rather than bound as a lone control because the template needs a real
  // `(ngSubmit)`: that output belongs to FormGroupDirective/NgForm, and on a bare <form> the binding
  // silently listens for an event nothing raises — leaving the button to do a NATIVE submit, which
  // reloads the whole SPA. It also makes Enter work, which is how this will actually be used.
  protected readonly codeForm = new FormGroup({
    code: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.pattern(/^\s*[0-9a-fA-F]{4}[\s-]?[0-9a-fA-F]{4}\s*$/)],
    }),
  });

  protected get codeInput(): FormControl<string> {
    return this.codeForm.controls.code;
  }

  protected readonly declineReason = new FormControl('', {
    nonNullable: true,
    validators: [Validators.maxLength(256)],
  });

  // Seconds left on the reservation. Purely informational — the server rejects a lapsed code on its
  // own, so this never gates the buttons; it exists so staff can tell a customer "this expired, tap
  // redeem again" instead of watching an approval fail for no visible reason.
  protected readonly secondsLeft = signal<number | null>(null);
  private countdownHandle: ReturnType<typeof setInterval> | null = null;

  protected readonly countdownLabel = computed(() => {
    const left = this.secondsLeft();
    if (left === null) return null;
    if (left <= 0) return '00:00';
    const minutes = Math.floor(left / 60)
      .toString()
      .padStart(2, '0');
    const seconds = (left % 60).toString().padStart(2, '0');
    return `${minutes}:${seconds}`;
  });

  protected readonly hasLapsed = computed(() => {
    const left = this.secondsLeft();
    return left !== null && left <= 0;
  });

  ngOnDestroy(): void {
    this.stopCountdown();
  }

  protected selectMode(mode: IdentifyMode): void {
    if (this.identifyMode() === mode) return;
    this.identifyMode.set(mode);
    this.cameraErrorKey.set(null);
    this.reset();
  }

  protected onScanned(payload: string): void {
    this.cameraErrorKey.set(null);
    void this.performLookup(payload);
  }

  protected onScanError(reason: QrScanErrorReason): void {
    // Steer to manual entry rather than dead-ending: a counter with no camera permission still has to
    // be able to serve the customer standing in front of it.
    this.cameraErrorKey.set(
      reason === 'permission-denied'
        ? '::BusinessPanel:Redemption:CameraDenied'
        : reason === 'not-supported'
          ? '::BusinessPanel:Redemption:CameraUnsupported'
          : '::BusinessPanel:Redemption:CameraFailed',
    );
  }

  protected submitCode(): void {
    if (this.codeInput.invalid) {
      this.codeInput.markAsTouched();
      return;
    }
    void this.performLookup(this.codeInput.value);
  }

  protected approve(): void {
    const current = this.lookup();
    if (!current?.code || this.busy()) return;

    this.isApproving.set(true);
    this.posService.confirmRedemption({ code: current.code }).subscribe({
      next: (result) => {
        this.isApproving.set(false);
        this.stopCountdown();
        this.approved.set(result);
        this.phase.set('approved');
      },
      error: () => this.isApproving.set(false),
    });
  }

  protected beginDecline(): void {
    this.showDeclineReason.set(true);
  }

  protected cancelDecline(): void {
    this.showDeclineReason.set(false);
    this.declineReason.reset();
  }

  protected confirmDecline(): void {
    const current = this.lookup();
    if (!current?.code || this.busy()) return;

    const reason = this.declineReason.value.trim();

    this.isDeclining.set(true);
    this.posService.rejectRedemption({ code: current.code, reason: reason || null }).subscribe({
      next: (result) => {
        this.isDeclining.set(false);
        this.stopCountdown();
        this.declined.set(result);
        this.phase.set('declined');
      },
      error: () => this.isDeclining.set(false),
    });
  }

  protected reset(): void {
    this.stopCountdown();
    this.phase.set('idle');
    this.lookup.set(null);
    this.approved.set(null);
    this.declined.set(null);
    this.showDeclineReason.set(false);
    this.declineReason.reset();
    this.codeForm.reset();
  }

  /** Display-only grouping — `3B02543F` reads as `3B02 543F` across a counter. */
  protected formatCode(code: string | null | undefined): string {
    if (!code) return '';
    return code.length === 8 ? `${code.slice(0, 4)} ${code.slice(4)}` : code;
  }

  protected initialsFor(name: string | null | undefined): string {
    if (!name) return '?';
    return (
      name
        .split(' ')
        .filter(Boolean)
        .map((word) => word[0])
        .join('')
        .slice(0, 2)
        .toUpperCase() || '?'
    );
  }

  private async performLookup(rawCode: string): Promise<void> {
    const code = this.normalize(rawCode);
    if (!code) return;

    this.isLookingUp.set(true);
    this.posService.lookupRedemption({ code }).subscribe({
      next: (result) => {
        this.isLookingUp.set(false);
        this.lookup.set(result);
        this.phase.set('review');
        this.startCountdown(result.reservationExpiresAt);
      },
      error: () => {
        this.isLookingUp.set(false);
        // The interceptor already surfaced the server's own message ("Invalid or already-used code",
        // "This redemption has expired"), which is more specific than anything restated here. Just
        // clear the field so the next scan or entry starts clean.
        this.codeInput.reset();
      },
    });
  }

  private normalize(code: string): string {
    return (code ?? '').replace(/[\s-]/g, '').toUpperCase();
  }

  private startCountdown(expiresAt: string | null | undefined): void {
    this.stopCountdown();
    if (!expiresAt) {
      // A legacy `Issued` coupon holds no reservation, so there is nothing to count down.
      this.secondsLeft.set(null);
      return;
    }

    // The API stores `timestamp without time zone` written from ABP's `IClock.Now`, which this
    // solution leaves at `DateTimeKind.Unspecified` — so this is the server's LOCAL time with no
    // offset. ECMAScript reads an offset-less date-time as local, which is the correct reading while
    // the till and the API share a timezone. Appending a `Z` here would shift the countdown by the
    // server's own offset.
    const deadline = Date.parse(expiresAt);
    if (Number.isNaN(deadline)) {
      this.secondsLeft.set(null);
      return;
    }

    const tick = () => this.secondsLeft.set(Math.max(0, Math.round((deadline - Date.now()) / 1000)));
    tick();
    this.countdownHandle = setInterval(tick, 1000);
  }

  private stopCountdown(): void {
    if (this.countdownHandle !== null) {
      clearInterval(this.countdownHandle);
      this.countdownHandle = null;
    }
    this.secondsLeft.set(null);
  }
}
