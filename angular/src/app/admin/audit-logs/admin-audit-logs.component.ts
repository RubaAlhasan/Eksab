import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, signal, inject } from '@angular/core';
import { LocalizationPipe } from '@abp/ng.core';
import { AuditLogsService } from '../../proxy/controllers/audit-logs.service';
import type { AuditLogDetailDto, AuditLogDto } from '../../proxy/audit-logs/models';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { SearchInputComponent } from '../../shared/components/search-input/search-input.component';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { StatusBadgeComponent, StatusBadgeVariant } from '../../shared/components/status-badge/status-badge.component';
import { ModalComponent } from '../../shared/components/modal/modal.component';

/** ABP's Volo.Abp.Auditing.EntityChangeType — Created = 0, Updated = 1, Deleted = 2. Kept as a plain
 *  local map (localization key suffix, not display text) rather than importing the framework enum,
 *  matching AuditLogEntityChangeDto's own "don't leak a framework-internal enum through the wire DTO"
 *  treatment. */
const ENTITY_CHANGE_TYPE_KEYS: Record<number, string> = {
  0: '::AdminPanel:AuditLogs:ChangeTypeCreated',
  1: '::AdminPanel:AuditLogs:ChangeTypeUpdated',
  2: '::AdminPanel:AuditLogs:ChangeTypeDeleted',
};

const ENTITY_CHANGE_TYPE_VARIANTS: Record<number, StatusBadgeVariant> = {
  0: 'success',
  1: 'warning',
  2: 'danger',
};

/**
 * Admin Portal > Audit Logs — mirrors the INTENT of prototype/admin/audit-logs.html ("who did what,
 * across the platform"), not its literal shape. Built against a real, hand-written
 * `IAdminAuditLogAppService` (`Eksabli.AuditLogs.Default`) over ABP's own OSS `IAuditLogRepository`
 * (`Volo.Abp.AuditLogging.Domain`) — this session's own investigation found that ABP's *browsable* UI
 * layer for audit logs (`Volo.Abp.AuditLogging.Application[.Contracts]`/`.HttpApi`, the packages every
 * other Admin Portal identity/tenant-management page reuses) does not exist on nuget.org at all; it's
 * exclusively part of ABP Commercial (a paid product this repo has no license/feed for — confirmed
 * directly against the NuGet Search API, not just a failed local restore). The DATA itself has been
 * real and live since day one (`EksabliDbContext.ConfigureAuditLogging()`, `AbpAuditLogs` table
 * present since the very first migration) — only the query surface was missing, so this page is built
 * on a small, hand-written app service over the same real repository instead, not a commercial
 * dependency and not fabricated data.
 *
 * Real fields — genuinely different shape from the prototype's own "Actor / Action / Target /
 * Timestamp" narrative-log columns, because ABP's `AuditLog` is a raw HTTP REQUEST log (method/URL/
 * status/duration/exception), not a domain-event log:
 * - Actor: `UserName` (real; shows "System" when null — an unauthenticated/background request).
 * - Request: `HttpMethod` + `Url` combined (real).
 * - Status: real `HttpStatusCode`, color-coded 2xx/4xx/5xx (no fabricated status categories).
 * - Duration: real `ExecutionDuration` (ms).
 * - Exception: real `HasException` badge (derived server-side from a non-empty `Exceptions` string,
 *   not from re-parsing the raw text client-side).
 * - Filters: User search + HasException toggle + a single-day date range (`StartTime`/`EndTime` set to
 *   that day's bounds) are all real, straight `AdminAuditLogFilterDto` fields mirroring
 *   `IAuditLogRepository.GetListAsync`'s own real filter parameters — not invented.
 *
 * Detail drill-down (View button → modal, `IAdminAuditLogAppService.GetAsync`/`AuditLogDetailDto`):
 * - "Request payload" is real — `AuditLogAction.Parameters`, the JSON-serialized argument set ABP's
 *   own auditing interceptor captures per application-service method call within the request, one
 *   card per action (a single request can call more than one service method).
 * - There is genuinely no "response body" anywhere in ABP's OSS auditing data (confirmed by reading
 *   `Volo.Abp.AuditLogging.AuditLog` — no ReturnValue/response field exists at all; it audits the
 *   request and its side effects, not a response payload capture). `EntityChanges` — the actual
 *   before/after property values of every entity the request created/updated/deleted — is shown in
 *   that slot instead, as the closest real "what happened as a result" data that exists, labeled as
 *   Entity Changes rather than mislabeled as a response body that was never captured.
 * - Full `Exceptions` stack trace (not just the list's `HasException` badge) and `Comments` (ABP's own
 *   free-text field, usually empty) are shown too, when present.
 */
@Component({
  selector: 'app-admin-audit-logs',
  templateUrl: './admin-audit-logs.component.html',
  styleUrls: ['./admin-audit-logs.component.scss'],
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
    StatusBadgeComponent,
    ModalComponent,
  ],
})
export class AdminAuditLogsComponent implements OnInit {
  private readonly auditLogsService = inject(AuditLogsService);

  private readonly pageSize = 20;

  protected readonly logs = signal<AuditLogDto[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly userNameFilter = signal('');
  protected readonly dateFilter = signal('');
  protected readonly hasExceptionOnly = signal(false);
  protected readonly pageIndex = signal(0);
  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));

  protected readonly detailModalOpen = signal(false);
  protected readonly detailLoading = signal(false);
  protected readonly detailLoadFailed = signal(false);
  protected readonly selectedLog = signal<AuditLogDetailDto | null>(null);
  private selectedLogId: string | null = null;

  ngOnInit(): void {
    this.load();
  }

  protected retry(): void {
    this.load();
  }

  protected onUserNameInput(value: string): void {
    this.userNameFilter.set(value);
    this.pageIndex.set(0);
    this.load();
  }

  protected onDateChange(event: Event): void {
    this.dateFilter.set((event.target as HTMLInputElement).value);
    this.pageIndex.set(0);
    this.load();
  }

  protected toggleHasExceptionOnly(): void {
    this.hasExceptionOnly.update((value) => !value);
    this.pageIndex.set(0);
    this.load();
  }

  protected goToPage(index: number): void {
    if (index < 0 || index >= this.totalPages()) return;
    this.pageIndex.set(index);
    this.load();
  }

  protected statusVariant(statusCode: number | null | undefined): StatusBadgeVariant {
    if (!statusCode) return 'neutral';
    if (statusCode >= 500) return 'danger';
    if (statusCode >= 400) return 'warning';
    return 'success';
  }

  protected changeTypeKey(changeType: number | undefined): string {
    return ENTITY_CHANGE_TYPE_KEYS[changeType ?? -1] ?? '::AdminPanel:AuditLogs:ChangeTypeUpdated';
  }

  protected changeTypeVariant(changeType: number | undefined): StatusBadgeVariant {
    return ENTITY_CHANGE_TYPE_VARIANTS[changeType ?? -1] ?? 'neutral';
  }

  /** `AuditLogAction.Parameters`/entity-change values are raw strings as ABP itself stored them — JSON
   *  for Parameters, but not guaranteed for property change values (could be a plain scalar's
   *  ToString()). Pretty-print only when it genuinely parses as JSON; show the raw string otherwise
   *  rather than a broken/misleading reformat. */
  protected prettyPrint(raw: string | null | undefined): string {
    if (!raw) return '';
    try {
      return JSON.stringify(JSON.parse(raw), null, 2);
    } catch {
      return raw;
    }
  }

  protected viewDetails(id: string | undefined): void {
    if (!id) return;
    this.selectedLogId = id;
    this.detailModalOpen.set(true);
    this.loadDetail();
  }

  protected retryDetail(): void {
    this.loadDetail();
  }

  protected closeDetailModal(): void {
    this.detailModalOpen.set(false);
    this.selectedLog.set(null);
    this.selectedLogId = null;
  }

  private loadDetail(): void {
    if (!this.selectedLogId) return;
    this.detailLoading.set(true);
    this.detailLoadFailed.set(false);
    this.selectedLog.set(null);

    this.auditLogsService.get(this.selectedLogId).subscribe({
      next: (detail) => {
        this.selectedLog.set(detail);
        this.detailLoading.set(false);
      },
      error: () => {
        this.detailLoading.set(false);
        this.detailLoadFailed.set(true);
      },
    });
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);

    const date = this.dateFilter();
    const startTime = date ? new Date(`${date}T00:00:00`).toISOString() : null;
    const endTime = date ? new Date(`${date}T23:59:59`).toISOString() : null;

    this.auditLogsService
      .getList({
        userName: this.userNameFilter() || null,
        startTime,
        endTime,
        hasException: this.hasExceptionOnly() ? true : null,
        httpMethod: null,
        url: null,
        httpStatusCode: null,
        sorting: 'executionTime desc',
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
