import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, signal, inject } from '@angular/core';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { AdminSmsLogsService } from '../../proxy/controllers/admin-sms-logs.service';
import type { SmsLogDto } from '../../proxy/sms/models';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { SearchInputComponent } from '../../shared/components/search-input/search-input.component';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';

// OTP messages are composed server-side as "Your Eksabli verification code is {code}"
// (OtpAppService.SendCodeAsync) — a plain 6-digit run pulled out of the message, not a separately
// stored field. Campaign SMS (NotificationSender) rarely contains a bare 6-digit run, so this stays
// null for those rows and the row just shows the full message instead of a Code badge.
const CODE_PATTERN = /\b(\d{6})\b/;

/**
 * Admin Portal > Verification Codes — browses `SmsLog`, the table `NullSmsSender` writes to in place
 * of a real SMS provider (see that class's own comment). Exists because OTP codes were previously only
 * reachable via server log access; this makes them reachable from the Admin Portal instead, same
 * "small hand-written query surface over real data" shape as Audit Logs.
 */
@Component({
  selector: 'app-admin-sms-logs',
  templateUrl: './admin-sms-logs.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    LocalizationPipe,
    PageHeaderComponent,
    SearchInputComponent,
    LoadingSpinnerComponent,
    ErrorStateComponent,
    EmptyStateComponent,
    PaginationComponent,
  ],
})
export class AdminSmsLogsComponent implements OnInit {
  private readonly smsLogsService = inject(AdminSmsLogsService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly toaster = inject(ToasterService);

  private readonly pageSize = 20;

  protected readonly logs = signal<SmsLogDto[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly isClearing = signal(false);
  protected readonly filterText = signal('');
  protected readonly pageIndex = signal(0);
  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));

  private readonly copiedId = signal<string | null>(null);

  ngOnInit(): void {
    this.load();
  }

  protected retry(): void {
    this.load();
  }

  protected onFilterInput(value: string): void {
    this.filterText.set(value);
    this.pageIndex.set(0);
    this.load();
  }

  protected goToPage(index: number): void {
    if (index < 0 || index >= this.totalPages()) return;
    this.pageIndex.set(index);
    this.load();
  }

  protected codeOf(log: SmsLogDto): string | null {
    return log.message.match(CODE_PATTERN)?.[1] ?? null;
  }

  protected isCopied(log: SmsLogDto): boolean {
    return this.copiedId() === log.id;
  }

  protected async copyCode(log: SmsLogDto): Promise<void> {
    const code = this.codeOf(log);
    if (!code) return;
    try {
      await navigator.clipboard.writeText(code);
      this.copiedId.set(log.id);
      setTimeout(() => {
        if (this.copiedId() === log.id) this.copiedId.set(null);
      }, 1500);
    } catch {
      // Clipboard API can be unavailable (insecure context, denied permission, etc.) — the code is
      // still fully visible/selectable in the row either way, so this is a convenience, not the only
      // way to read it.
      this.toaster.error('::AdminPanel:SmsLogs:CopyFailedMessage');
    }
  }

  protected clearLog(): void {
    this.confirmation
      .warn('::AdminPanel:SmsLogs:ClearConfirmMessage', '::AdminPanel:SmsLogs:ClearConfirmTitle')
      .subscribe((status) => {
        if (status !== Confirmation.Status.confirm) return;
        this.isClearing.set(true);
        this.smsLogsService.clear().subscribe({
          next: () => {
            this.isClearing.set(false);
            this.toaster.success('::AdminPanel:SmsLogs:ClearSuccess');
            this.pageIndex.set(0);
            this.load();
          },
          error: () => {
            this.isClearing.set(false);
            this.toaster.error('::AdminPanel:SmsLogs:ClearErrorMessage');
          },
        });
      });
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);

    this.smsLogsService
      .getList({
        filterText: this.filterText() || null,
        skipCount: this.pageIndex() * this.pageSize,
        maxResultCount: this.pageSize,
      })
      .subscribe({
        next: (result) => {
          this.logs.set(result.items ?? []);
          this.totalCount.set(result.totalCount ?? 0);
          this.isLoading.set(false);
        },
        error: () => {
          this.isLoading.set(false);
          this.loadFailed.set(true);
        },
      });
  }
}
