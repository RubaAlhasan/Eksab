import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { UserNotificationsService } from '../../proxy/controllers/user-notifications.service';
import { AdminTenantsService } from '../../proxy/controllers/admin-tenants.service';
import type { SendUserNotificationDto } from '../../proxy/user-notifications/models';
import { UserNotificationType } from '../../proxy/user-notifications/user-notification-type.enum';
import { NotificationTargetType } from '../../proxy/user-notifications/notification-target-type.enum';
import type { AdminTenantDto } from '../../proxy/businesses/models';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';

type RecipientMode = 'tenant' | 'broadcast';

interface ComposeFormValue {
  type: UserNotificationType;
  title: string;
  message: string;
}

/**
 * Admin Portal > Notifications — sends a real-time Notification Hub message to one business, or to
 * every business at once. Distinct from the Business Portal's Notifications page
 * (business-notifications.component.ts), which sends a campaign-channel message to one customer/
 * Membership via a completely different controller (`NotificationsController`,
 * `Eksabli.Notifications.Send`).
 *
 * Wired against the real `IUserNotificationAppService.SendAsync` (`POST /api/app/user-notifications`,
 * gated on `Eksabli.Notifications.Broadcast` — confirmed in `UserNotificationsController.cs`).
 *
 * - **Recipient = "One Business"**: `targetType = Tenant`, `tenantId` from a real business search-and-
 *   pick against `AdminTenantsService.getList({ filterText })`. Fans out to every staff `IdentityUser`
 *   of that tenant (Owner/Manager/Cashier/Marketing).
 * - **Recipient = "All Businesses"**: **deliberately NOT** `SendUserNotificationDto.TargetType.Broadcast`
 *   — confirmed live (this session, by querying `AppNotificationMessages`/`AppUserNotifications`
 *   directly) that `Broadcast` fans out to `NotificationPublisher.PublishToAllAsync`, which explicitly
 *   includes the Host realm (`FanOutToCurrentRealmAsync(null, ...)` before looping tenants) — i.e. it
 *   reaches platform admins too, per `NotificationTargetType.Broadcast`'s own doc comment ("every user,
 *   across every tenant AND the Host realm"). That's correct behavior for a true platform-wide
 *   broadcast, but wrong for what "All Businesses" promises: only business staff, never platform admins.
 *   So this mode instead lists every tenant (`AdminTenantsService.getList`) and fires one real
 *   `targetType = Tenant` send per tenant — same call as "One Business," looped. This means N
 *   `NotificationMessage` rows get created (one per tenant) instead of one shared broadcast row — an
 *   acceptable MVP-scale trade-off for real per-business scoping; genuinely queueing/rate-limiting this
 *   fan-out is a Phase-2 concern if the platform ever has enough tenants for N concurrent requests to
 *   matter (see the campaign-notification fan-out's own per-tenant rate-limit note in
 *   docs/eksabli-loyalty-platform/02-system-architecture.md for the same reasoning applied there).
 *   Tenant list is fetched with `maxResultCount: 1000` — fine for admin-tooling scale, not a true
 *   "unpaged" endpoint; revisit if this platform ever has more tenants than that.
 * - No delivery log on this page: `IUserNotificationAppService.GetListAsync` returns the *caller's own*
 *   feed (`CurrentUser.GetId()`), not a queryable send history across recipients — there is no backend
 *   endpoint that lists what an admin has sent, so this page doesn't fabricate one.
 */
@Component({
  selector: 'app-admin-notifications',
  templateUrl: './admin-notifications.component.html',
  styleUrls: ['./admin-notifications.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, LocalizationPipe, PageHeaderComponent],
})
export class AdminNotificationsComponent {
  private readonly userNotificationsService = inject(UserNotificationsService);
  private readonly adminTenantsService = inject(AdminTenantsService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly toaster = inject(ToasterService);

  protected readonly Type = UserNotificationType;

  protected readonly recipientMode = signal<RecipientMode>('tenant');
  protected readonly isSending = signal(false);

  protected readonly tenantQuery = signal('');
  protected readonly tenantResults = signal<AdminTenantDto[]>([]);
  protected readonly isSearchingTenants = signal(false);
  protected readonly selectedTenant = signal<AdminTenantDto | null>(null);
  private tenantSearchTimer?: ReturnType<typeof setTimeout>;

  protected readonly form = new FormGroup({
    type: new FormControl(UserNotificationType.Info, { nonNullable: true, validators: [Validators.required] }),
    title: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(128)] }),
    message: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(1000)] }),
  });

  protected readonly canSubmit = computed(() => {
    if (this.recipientMode() === 'tenant' && !this.selectedTenant()) return false;
    return true;
  });

  protected setRecipientMode(mode: RecipientMode): void {
    this.recipientMode.set(mode);
    if (mode === 'broadcast') {
      this.changeTenant();
    }
  }

  protected onTenantQueryInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.tenantQuery.set(value);
    clearTimeout(this.tenantSearchTimer);

    if (value.trim().length < 2) {
      this.tenantResults.set([]);
      return;
    }

    this.tenantSearchTimer = setTimeout(() => {
      this.isSearchingTenants.set(true);
      this.adminTenantsService
        .getList({ filterText: value, approvalStatus: null, sorting: undefined, skipCount: 0, maxResultCount: 8 })
        .subscribe({
          next: (result) => {
            this.tenantResults.set(result.items ?? []);
            this.isSearchingTenants.set(false);
          },
          error: () => {
            this.isSearchingTenants.set(false);
          },
        });
    }, 300);
  }

  protected selectTenant(tenant: AdminTenantDto): void {
    this.selectedTenant.set(tenant);
    this.tenantResults.set([]);
    this.tenantQuery.set('');
  }

  protected changeTenant(): void {
    this.selectedTenant.set(null);
    this.tenantQuery.set('');
    this.tenantResults.set([]);
  }

  protected submitSend(): void {
    if (this.form.invalid || !this.canSubmit()) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();

    if (this.recipientMode() === 'broadcast') {
      this.confirmation
        .warn('::AdminPanel:Notifications:BroadcastConfirmMessage', '::AdminPanel:Notifications:BroadcastConfirmTitle')
        .subscribe((status) => {
          if (status === Confirmation.Status.confirm) this.sendToEveryBusiness(value);
        });
      return;
    }

    this.sendToOneBusiness(value);
  }

  private sendToOneBusiness(value: ComposeFormValue): void {
    const tenantId = this.selectedTenant()?.tenantId;
    if (!tenantId) return;

    const payload: SendUserNotificationDto = {
      targetType: NotificationTargetType.Tenant,
      tenantId,
      userId: null,
      type: value.type,
      title: value.title,
      message: value.message,
    };

    this.isSending.set(true);
    this.userNotificationsService.send(payload).subscribe({
      next: () => {
        this.isSending.set(false);
        this.toaster.success('::AdminPanel:Notifications:SentMessage');
        this.resetForm();
      },
      error: () => {
        this.isSending.set(false);
        this.toaster.error('::AdminPanel:Notifications:SendErrorMessage');
      },
    });
  }

  // Real per-tenant fan-out (see this file's own top-of-file comment for why this is NOT
  // `targetType: Broadcast`) — one real `Tenant`-targeted send per business, never touching the Host
  // realm.
  private sendToEveryBusiness(value: ComposeFormValue): void {
    this.isSending.set(true);
    this.adminTenantsService
      .getList({ filterText: null, approvalStatus: null, sorting: undefined, skipCount: 0, maxResultCount: 1000 })
      .subscribe({
        next: (result) => {
          const tenantIds = (result.items ?? [])
            .map((tenant) => tenant.tenantId)
            .filter((id): id is string => !!id);

          if (tenantIds.length === 0) {
            this.isSending.set(false);
            this.toaster.warn('::AdminPanel:Notifications:NoBusinessesMessage');
            return;
          }

          forkJoin(
            tenantIds.map((tenantId) =>
              this.userNotificationsService.send({
                targetType: NotificationTargetType.Tenant,
                tenantId,
                userId: null,
                type: value.type,
                title: value.title,
                message: value.message,
              }),
            ),
          ).subscribe({
            next: () => {
              this.isSending.set(false);
              this.toaster.success('::AdminPanel:Notifications:SentMessage');
              this.resetForm();
            },
            error: () => {
              this.isSending.set(false);
              this.toaster.error('::AdminPanel:Notifications:SendErrorMessage');
            },
          });
        },
        error: () => {
          this.isSending.set(false);
          this.toaster.error('::AdminPanel:Notifications:SendErrorMessage');
        },
      });
  }

  private resetForm(): void {
    this.form.reset({ type: UserNotificationType.Info, title: '', message: '' });
    this.changeTenant();
  }
}
