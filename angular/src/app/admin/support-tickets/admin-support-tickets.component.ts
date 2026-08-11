import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ConfigStateService, LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { SupportTicketsService } from '../../proxy/controllers/support-tickets.service';
import { AdminTenantsService } from '../../proxy/controllers/admin-tenants.service';
import type { SupportTicketDto, SupportTicketMessageDto } from '../../proxy/platform/models';
import { SupportTicketStatus } from '../../proxy/platform/support-ticket-status.enum';
import { SupportTicketPriority } from '../../proxy/platform/support-ticket-priority.enum';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { StatusBadgeComponent, StatusBadgeVariant } from '../../shared/components/status-badge/status-badge.component';
import { ModalComponent } from '../../shared/components/modal/modal.component';

/**
 * Admin Portal > Support Tickets — the Support Agent queue (`SupportTicketsController.GetListAsync`,
 * gated on `Eksabli.SupportTickets.Manage`) plus the thread/detail view, built as a modal rather than a
 * separate routed page — matching admin-portal-implementation-plan.md §18's own recommendation ("this
 * repo's natural pattern is a drawer/panel rather than a separate route") and the same
 * expand-in-place shape already used by Subscriptions' invoice sub-list.
 *
 * Real gaps, documented rather than worked around:
 * - `[MISSING BACKEND CAPABILITY]` No `filterText` on `SupportTicketFilterDto` — Subject isn't
 *   searchable, only `status`/`priority`/`tenantId` filters exist. No search box on this page.
 * - `[MISSING BACKEND CAPABILITY]` No reopen / explicit in-progress transition — only `ResolveAsync`
 *   exists (`SupportTicket.Reopen()`/`.Close()` are real domain methods but neither is exposed through
 *   `ISupportTicketAppService`). "Mark Resolved" is the only status-changing action this page offers.
 * - The "From" column resolves a business ticket's `tenantId` to a name via the same bulk
 *   `AdminTenantsService.getList` lookup Subscriptions uses (best-effort — failure/lack of
 *   `Tenants.View` just falls back to the raw id, doesn't block the page). A customer ticket's
 *   `customerId` is NOT resolved to a user name — doing so would mean one `IdentityUserService.get()`
 *   call per row (N+1) against a permission (`AbpIdentity.Users`) a Support Agent may not even hold;
 *   shown as a generic "Customer" label instead, which is honest given what's actually available.
 */
@Component({
  selector: 'app-admin-support-tickets',
  templateUrl: './admin-support-tickets.component.html',
  styleUrls: ['./admin-support-tickets.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    ReactiveFormsModule,
    LocalizationPipe,
    PageHeaderComponent,
    LoadingSpinnerComponent,
    ErrorStateComponent,
    EmptyStateComponent,
    PaginationComponent,
    StatusBadgeComponent,
    ModalComponent,
  ],
})
export class AdminSupportTicketsComponent implements OnInit {
  private readonly ticketsService = inject(SupportTicketsService);
  private readonly tenantsService = inject(AdminTenantsService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly toaster = inject(ToasterService);
  private readonly configState = inject(ConfigStateService);

  protected readonly Status = SupportTicketStatus;
  private readonly pageSize = 10;

  protected readonly tickets = signal<SupportTicketDto[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly statusFilterValue = signal('');
  protected readonly priorityFilterValue = signal('');
  protected readonly pageIndex = signal(0);
  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));

  /** Bare `tenantId` -> display name, same "load a bounded batch once, fall back to the raw id on
   *  failure" approach admin-subscriptions.component.ts already established. */
  private readonly tenantNames = signal<Map<string, string>>(new Map());

  private readonly currentUserId = computed(() => {
    const currentUser = this.configState.getOne('currentUser') as { id?: string } | undefined;
    return currentUser?.id;
  });

  protected readonly detailModalOpen = signal(false);
  protected readonly detailTicket = signal<SupportTicketDto | null>(null);
  protected readonly detailLoading = signal(false);
  protected readonly detailFailed = signal(false);
  protected readonly isSendingReply = signal(false);
  protected readonly isResolving = signal(false);
  private openTicketId: string | null = null;

  protected readonly replyForm = new FormGroup({
    body: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(4000)] }),
  });

  ngOnInit(): void {
    this.loadTenantNames();
    this.load();
  }

  protected tenantName(tenantId: string): string {
    return this.tenantNames().get(tenantId) ?? tenantId;
  }

  protected onStatusFilterChange(event: Event): void {
    this.statusFilterValue.set((event.target as HTMLSelectElement).value);
    this.pageIndex.set(0);
    this.load();
  }

  protected onPriorityFilterChange(event: Event): void {
    this.priorityFilterValue.set((event.target as HTMLSelectElement).value);
    this.pageIndex.set(0);
    this.load();
  }

  protected goToPage(index: number): void {
    if (index < 0 || index >= this.totalPages()) return;
    this.pageIndex.set(index);
    this.load();
  }

  protected retry(): void {
    this.load();
  }

  protected statusLabelKey(status: SupportTicketStatus | undefined): string {
    switch (status) {
      case SupportTicketStatus.Open:
        return '::AdminPanel:SupportTickets:StatusOpen';
      case SupportTicketStatus.InProgress:
        return '::AdminPanel:SupportTickets:StatusInProgress';
      case SupportTicketStatus.Resolved:
        return '::AdminPanel:SupportTickets:StatusResolved';
      default:
        return '::AdminPanel:SupportTickets:StatusClosed';
    }
  }

  protected statusVariant(status: SupportTicketStatus | undefined): StatusBadgeVariant {
    switch (status) {
      case SupportTicketStatus.Open:
        return 'info';
      case SupportTicketStatus.InProgress:
        return 'warning';
      case SupportTicketStatus.Resolved:
        return 'success';
      default:
        return 'neutral';
    }
  }

  protected priorityLabelKey(priority: SupportTicketPriority | undefined): string {
    switch (priority) {
      case SupportTicketPriority.Low:
        return '::AdminPanel:SupportTickets:PriorityLow';
      case SupportTicketPriority.Medium:
        return '::AdminPanel:SupportTickets:PriorityMedium';
      case SupportTicketPriority.High:
        return '::AdminPanel:SupportTickets:PriorityHigh';
      default:
        return '::AdminPanel:SupportTickets:PriorityUrgent';
    }
  }

  protected priorityVariant(priority: SupportTicketPriority | undefined): StatusBadgeVariant {
    switch (priority) {
      case SupportTicketPriority.Low:
        return 'neutral';
      case SupportTicketPriority.Medium:
        return 'info';
      case SupportTicketPriority.High:
        return 'warning';
      default:
        return 'danger';
    }
  }

  protected isFromCurrentUser(message: SupportTicketMessageDto): boolean {
    const currentUserId = this.currentUserId();
    return !!currentUserId && message.senderId === currentUserId;
  }

  protected canResolve(ticket: SupportTicketDto): boolean {
    return ticket.status !== SupportTicketStatus.Resolved && ticket.status !== SupportTicketStatus.Closed;
  }

  protected openDetail(ticket: SupportTicketDto): void {
    if (!ticket.id) return;
    this.openTicketId = ticket.id;
    this.detailTicket.set(null);
    this.replyForm.reset({ body: '' });
    this.detailModalOpen.set(true);
    this.loadDetail(ticket.id);
  }

  protected closeDetail(): void {
    this.detailModalOpen.set(false);
    this.openTicketId = null;
  }

  protected retryDetail(): void {
    if (this.openTicketId) this.loadDetail(this.openTicketId);
  }

  protected sendReply(): void {
    if (this.replyForm.invalid || !this.openTicketId) {
      this.replyForm.markAllAsTouched();
      return;
    }

    const ticketId = this.openTicketId;
    this.isSendingReply.set(true);
    this.ticketsService.addMessage(ticketId, { body: this.replyForm.getRawValue().body }).subscribe({
      next: () => {
        this.isSendingReply.set(false);
        this.replyForm.reset({ body: '' });
        this.toaster.success('::AdminPanel:SupportTickets:ReplySentMessage');
        this.loadDetail(ticketId);
        this.load();
      },
      error: () => {
        this.isSendingReply.set(false);
        this.toaster.error('::AdminPanel:SupportTickets:ReplyErrorMessage');
      },
    });
  }

  protected resolveTicket(): void {
    const ticketId = this.openTicketId;
    if (!ticketId) return;

    this.confirmation
      .warn('::AdminPanel:SupportTickets:ResolveConfirmMessage', '::AdminPanel:SupportTickets:ResolveConfirmTitle')
      .subscribe((status) => {
        if (status !== Confirmation.Status.confirm) return;

        this.isResolving.set(true);
        this.ticketsService.resolve(ticketId).subscribe({
          next: (ticket) => {
            this.isResolving.set(false);
            this.detailTicket.set(ticket);
            this.toaster.success('::AdminPanel:SupportTickets:ResolvedMessage');
            this.load();
          },
          error: () => {
            this.isResolving.set(false);
            this.toaster.error('::AdminPanel:SupportTickets:ResolveErrorMessage');
          },
        });
      });
  }

  private loadDetail(ticketId: string): void {
    this.detailLoading.set(true);
    this.detailFailed.set(false);
    this.ticketsService.get(ticketId).subscribe({
      next: (ticket) => {
        this.detailTicket.set(ticket);
        this.detailLoading.set(false);
      },
      error: () => {
        this.detailLoading.set(false);
        this.detailFailed.set(true);
      },
    });
  }

  private loadTenantNames(): void {
    this.tenantsService
      .getList({ filterText: null, approvalStatus: null, skipCount: 0, maxResultCount: 500 })
      .subscribe({
        next: (result) => {
          const map = new Map<string, string>();
          for (const tenant of result.items ?? []) {
            if (tenant.tenantId) map.set(tenant.tenantId, tenant.tenantName ?? tenant.tenantId);
          }
          this.tenantNames.set(map);
        },
        // Best-effort — a viewer without Tenants.View just sees raw tenantId values, doesn't block
        // the ticket queue itself.
        error: () => undefined,
      });
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);

    const status =
      this.statusFilterValue() === '' ? null : (Number(this.statusFilterValue()) as SupportTicketStatus);
    const priority =
      this.priorityFilterValue() === '' ? null : (Number(this.priorityFilterValue()) as SupportTicketPriority);

    this.ticketsService
      .getList({
        status,
        priority,
        tenantId: null,
        sorting: 'lastModificationTime desc',
        skipCount: this.pageIndex() * this.pageSize,
        maxResultCount: this.pageSize,
      })
      .subscribe({
        next: (result) => {
          this.tickets.set(result.items ?? []);
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
