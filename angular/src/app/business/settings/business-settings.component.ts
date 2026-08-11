import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ConfigStateService, LocalizationPipe, PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { BusinessService } from '../../proxy/controllers/business.service';
import { CategoriesService } from '../../proxy/controllers/categories.service';
import type { BusinessProfileDto } from '../../proxy/business-profiles/models';
import type { CategoryDto } from '../../proxy/platform/models';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';

/** `BusinessProfile.SocialLinksJson` has no fixed schema (freeform blob, confirmed by reading
 *  `BusinessProfile.cs`) — "instagram"/"facebook" are just the two keys `BusinessAppService.RegisterAsync`
 *  itself happens to populate at signup (`BuildSocialLinksJson`), not a schema the column enforces.
 *  Same "known keys, preserve the rest" convention as `admin-plans.component.ts`'s `FeatureLimitsJson`
 *  parsing, at a smaller scale. */
function parseSocialLinks(json: string | null | undefined): { instagram: string; facebook: string; rest: Record<string, unknown> } {
  let raw: Record<string, unknown> = {};
  try {
    raw = json ? (JSON.parse(json) as Record<string, unknown>) : {};
  } catch {
    raw = {};
  }
  const instagram = typeof raw['instagram'] === 'string' ? (raw['instagram'] as string) : '';
  const facebook = typeof raw['facebook'] === 'string' ? (raw['facebook'] as string) : '';
  const rest = { ...raw };
  delete rest['instagram'];
  delete rest['facebook'];
  return { instagram, facebook, rest };
}

function serializeSocialLinks(instagram: string, facebook: string, rest: Record<string, unknown>): string | null {
  const merged: Record<string, unknown> = { ...rest };
  if (instagram.trim()) merged['instagram'] = instagram.trim();
  if (facebook.trim()) merged['facebook'] = facebook.trim();
  return Object.keys(merged).length > 0 ? JSON.stringify(merged) : null;
}

/**
 * Business Portal > Settings — mirrors ONLY the real portion of prototype/business/settings.html's
 * "Profile & Branding" tab, built against the real `IBusinessAppService.GetProfileAsync`/
 * `UpdateProfileAsync` (`Eksabli.BusinessProfile.Default`/`.Edit`) + the same public
 * `CategoriesService.getList()` catalog Admin Categories/Business registration already use. No backend
 * changes needed; every endpoint and its proxy already existed.
 *
 * The prototype's other 3 tabs are dropped entirely, not stubbed, because each is
 * `[MISSING BACKEND CAPABILITY]`:
 * - **Notification Sender** — no sender-name/reply-to-email/custom-domain concept exists anywhere in
 *   `Eksabli.Notifications` (confirmed by reading `Notification.cs`/`NotificationAppService.cs`);
 *   `NotificationSender`/`NullPushNotificationSender` are hardcoded platform-level senders, not
 *   per-tenant configurable.
 * - **Integrations** (Stripe/FCM/SMS aggregator "Connected" badges) — pure decoration in the
 *   prototype itself (no click handler even simulates connecting); no integration/webhook-credential
 *   entity exists anywhere in this codebase.
 * - **Danger Zone** ("Cancel subscription") — duplicates Subscription page's own Danger Zone, which is
 *   itself dropped there for the same reason (`TenantSubscription.Cancel()` is unreachable via any API
 *   — see `business-subscription.component.ts`'s file comment).
 *
 * What's real and kept, in "Profile & Branding":
 * - **Business name is shown read-only**, from `ConfigStateService`'s own `currentTenant.name` (the
 *   same real tenant-resolution signal `business.guard.ts` already relies on for `.id`) — not
 *   editable, because `UpdateBusinessProfileDto` has no `Name` field at all (confirmed by reading it);
 *   the tenant's name lives on ABP's own `Tenant` entity, with no self-service rename endpoint exposed
 *   anywhere (`TenantManager.CreateAsync` sets it once, at registration).
 * - **Category, Website, Description (bilingual)** are real, straight `UpdateBusinessProfileDto`
 *   fields. Category options come from the real public catalog (`CategoriesService.getList`, the same
 *   `[AllowAnonymous]` read Admin Categories/registration use).
 * - **Instagram/Facebook** map to the real (but schema-free) `SocialLinksJson`, matching the exact two
 *   keys `RegisterAsync` itself populates at signup — see `parseSocialLinks`/`serializeSocialLinks`
 *   above. Any other keys already present are preserved on save, not silently dropped.
 * - **No logo upload** — `LogoBlobName` is real and shown as a placeholder/name only; no blob-upload
 *   widget exists anywhere in this app yet (same established gap as Rewards' `imageBlobName`/
 *   Categories' `iconBlobName`).
 */
@Component({
  selector: 'app-business-settings',
  templateUrl: './business-settings.component.html',
  styleUrls: ['./business-settings.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, LocalizationPipe, PageHeaderComponent, LoadingSpinnerComponent, ErrorStateComponent],
})
export class BusinessSettingsComponent implements OnInit {
  private readonly businessService = inject(BusinessService);
  private readonly categoriesService = inject(CategoriesService);
  private readonly configState = inject(ConfigStateService);
  private readonly toaster = inject(ToasterService);
  private readonly permissionService = inject(PermissionService);

  protected readonly canEdit = computed(() => this.permissionService.getGrantedPolicy('Eksabli.BusinessProfile.Edit'));

  protected readonly businessName = computed(() => {
    const currentTenant = this.configState.getOne('currentTenant') as { name?: string } | undefined;
    return currentTenant?.name ?? '—';
  });

  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly isSaving = signal(false);
  protected readonly profile = signal<BusinessProfileDto | null>(null);
  protected readonly categories = signal<CategoryDto[]>([]);
  private socialLinksRest: Record<string, unknown> = {};

  protected readonly form = new FormGroup({
    categoryId: new FormControl<string | null>(null),
    descriptionEn: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(2000)] }),
    descriptionAr: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(2000)] }),
    website: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(256)] }),
    instagram: new FormControl('', { nonNullable: true }),
    facebook: new FormControl('', { nonNullable: true }),
  });

  ngOnInit(): void {
    this.load();
    this.loadCategories();
  }

  protected retry(): void {
    this.load();
  }

  protected submitForm(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.isSaving.set(true);
    this.businessService
      .updateProfile({
        categoryId: value.categoryId || null,
        descriptionEn: value.descriptionEn || null,
        descriptionAr: value.descriptionAr || null,
        website: value.website || null,
        socialLinksJson: serializeSocialLinks(value.instagram, value.facebook, this.socialLinksRest),
      })
      .subscribe({
        next: (profile) => {
          this.isSaving.set(false);
          this.profile.set(profile);
          this.toaster.success('::BusinessPanel:Settings:SavedMessage');
        },
        error: () => {
          this.isSaving.set(false);
          this.toaster.error('::BusinessPanel:Settings:SaveErrorMessage');
        },
      });
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);
    this.businessService.getProfile().subscribe({
      next: (profile) => {
        this.profile.set(profile);
        const { instagram, facebook, rest } = parseSocialLinks(profile.socialLinksJson);
        this.socialLinksRest = rest;
        this.form.reset({
          categoryId: profile.categoryId ?? null,
          descriptionEn: profile.descriptionEn ?? '',
          descriptionAr: profile.descriptionAr ?? '',
          website: profile.website ?? '',
          instagram,
          facebook,
        });
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.loadFailed.set(true);
      },
    });
  }

  private loadCategories(): void {
    this.categoriesService.getList({ parentCategoryId: null, filterText: null, sorting: 'nameEn asc', skipCount: 0, maxResultCount: 200 }).subscribe({
      next: (result) => this.categories.set(result.items ?? []),
      error: () => undefined,
    });
  }
}
