import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { ConfigStateService, LocalizationPipe, RouteBasedCultureUrlService, SessionStateService } from '@abp/ng.core';
import { DevicesService } from '../../proxy/controllers/devices.service';
import {
  CustomerNotificationPreferencesService,
  NotificationPreferencesDto,
} from '../../proxy/controllers/customer-notification-preferences.service';
import type { DeviceDto } from '../../proxy/devices/models';
import { DevicePlatform } from '../../proxy/devices/device-platform.enum';
import { SkeletonListComponent } from '../../shared/components/skeleton-list/skeleton-list.component';
import { CustomerThemeService } from '../../shared/services/customer-theme.service';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';

/**
 * Settings — deliberately minimal. The prototype's settings.html also has push/email/SMS toggles, dark
 * mode, and delete-account; none of those are backed by anything real (no notification-preference
 * endpoint exists anywhere, no self-service delete-account endpoint exists, and the customer shell has
 * no dark theme defined yet — see customer-layout.component.ts's own comment on deferring that). Only
 * Language (same `RouteBasedCultureUrlService`/`SessionStateService` pattern already used in
 * business-layout.component.ts) and Linked Devices (`DevicesService` — real push-token registrations,
 * e.g. from the mobile app) are built here, because those are the only two real capabilities.
 */
@Component({
  selector: 'app-customer-settings',
  templateUrl: './customer-settings.component.html',
  styleUrls: ['./customer-settings.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, LocalizationPipe, SkeletonListComponent, EmptyStateComponent],
})
export class CustomerSettingsComponent implements OnInit {
  private readonly configState = inject(ConfigStateService);
  private readonly sessionState = inject(SessionStateService);
  private readonly cultureUrlService = inject(RouteBasedCultureUrlService);
  private readonly devicesService = inject(DevicesService);
  protected readonly theme = inject(CustomerThemeService);
  private readonly notificationPreferences = inject(CustomerNotificationPreferencesService);

  // The three switches. Null until loaded; a switch shows nothing rather than a guess while the answer is pending.
  protected readonly preferences = signal<NotificationPreferencesDto | null>(null);

  protected readonly Platform = DevicePlatform;
  protected readonly languages = computed(() => {
    const localization = this.configState.getOne('localization') as { languages?: { cultureName: string; displayName: string }[] } | undefined;
    return localization?.languages ?? [];
  });
  protected readonly currentLanguage = toSignal(this.sessionState.getLanguage$(), {
    initialValue: this.sessionState.getLanguage(),
  });

  protected readonly devices = signal<DeviceDto[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly removingIds = signal<Set<string>>(new Set());

  // Each switch saves on change. The new value shows at once and is put back if the server refuses it.
  protected setPreference(group: keyof NotificationPreferencesDto, enabled: boolean): void {
    const current = this.preferences();
    if (!current) return;

    const next = { ...current, [group]: enabled };
    this.preferences.set(next);
    this.notificationPreferences.updateMine(next).subscribe({
      next: saved => this.preferences.set(saved),
      error: () => this.preferences.set(current),
    });
  }

  ngOnInit(): void {
    this.notificationPreferences.getMine().subscribe({
      next: prefs => this.preferences.set(prefs),
      error: () => undefined,
    });

    this.devicesService.getList().subscribe({
      next: devices => {
        this.devices.set(devices);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  protected selectLanguage(cultureName: string): void {
    this.cultureUrlService.applyLanguageSelection(cultureName);
  }

  protected platformLabelKey(platform: DevicePlatform | undefined): string {
    switch (platform) {
      case DevicePlatform.iOS:
        return '::Wallet:Settings:PlatformIos';
      case DevicePlatform.Android:
        return '::Wallet:Settings:PlatformAndroid';
      default:
        return '::Wallet:Settings:PlatformWeb';
    }
  }

  protected removeDevice(id: string): void {
    if (this.removingIds().has(id)) return;
    this.removingIds.update(ids => new Set(ids).add(id));
    this.devicesService.remove(id).subscribe({
      next: () => this.devices.update(list => list.filter(d => d.id !== id)),
      error: () => {
        this.removingIds.update(ids => {
          const next = new Set(ids);
          next.delete(id);
          return next;
        });
      },
    });
  }
}
