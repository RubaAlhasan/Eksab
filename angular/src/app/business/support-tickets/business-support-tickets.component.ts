import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ConfigStateService, LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { SupportTicketsService } from '../../proxy/controllers/support-tickets.service';
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
 * Business Portal > Support Tickets — self-service: a tenant's own staff filing/viewing/replying to
 * their own tickets. This page didn't exist before this session; the backend endpoint it needs
 * (`SupportTicketsController.GetListAsync`) was previously gated `[Authorize(EksabliPermissions
 * .SupportTickets.Manage)]` — a Host-only permission (`MultiTenancySides.Host` in
 * EksabliPermissionDefinitionProvider) that no tenant-realm role can ever hold — so a business
 * literally could not list its own tickets before this session, even though `CreateAsync`/`GetAsync`/
 * `AddMessageAsync` were already reachable and correctly self-scoped
 * (`SupportTicketAppService.EnsureCanAccessAsync`). Fixed as a backend change alongside this page: the
 * `[Authorize(Manage)]` attribute moved off the controller action, and `GetListAsync` itself now
 * forces `tenantId`/`customerId` to the caller's own identity for anyone without `Manage` — the caller
 * always wins over whatever `SupportTicketFilterDto.TenantId` says, so this page can never become a
 * cross-tenant read no matter what it (or a modified request) sends.
 *
 * No `Mark Resolved` action here — that's a Support Agent capability (`Eksabli.SupportTickets.Manage`,
 * Host-only), not something a business can do to their own ticket. No "From" column either — every
 * ticket in this list is the tenant's own, so there's nothing to resolve to a name.
 */
@Component({
  selector: 'app-business-support-tickets',
  templateUrl: './business-support-tickets.component.html',
  styleUrls: ['./business-support-tickets.component.scss'],
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
export class BusinessSupportTicketsComponent implements OnInit {
  private readonly ticketsService = inject(SupportTicketsService);
  private readonly toaster = inject(ToasterService);
  private readonly configState = inject(ConfigStateService);

  protected readonly Status = SupportTicketStatus;
  protected readonly Priority = SupportTicketPriority;
  private readonly pageSize = 10;

  protected readonly tickets = signal<SupportTicketDto[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly statusFilterValue = signal('');
  protected readonly priorityFilterValue = signal('');
  protected readonly pageIndex = signal(0);
  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));

  private readonly currentUserId = computed(() => {
    const currentUser = this.configState.getOne('currentUser') as { id?: string } | undefined;
    return currentUser?.id;
  });

  protected readonly detailModalOpen = signal(false);
  protected readonly detailTicket = signal<SupportTicketDto | null>(null);
  protected readonly detailLoading = signal(false);
  protected readonly detailFailed = signal(false);
  protected readonly isSendingReply = signal(false);
  private openTicketId: string | null = null;

  protected readonly replyForm = new FormGroup({
    body: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(4000)] }),
  });

  protected readonly createModalOpen = signal(false);
  protected readonly isCreating = signal(false);
  protected readonly createForm = new FormGroup({
    subject: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(200)] }),
    body: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(4000)] }),
    priority: new FormControl(SupportTicketPriority.Medium, { nonNullable: true }),
  });

  ngOnInit(): void {
    this.load();
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
        return '::BusinessPanel:SupportTickets:StatusOpen';
      case SupportTicketStatus.InProgress:
        return '::BusinessPanel:SupportTickets:StatusInProgress';
      case SupportTicketStatus.Resolved:
        return '::BusinessPanel:SupportTickets:StatusResolved';
      default:
        return '::BusinessPanel:SupportTickets:StatusClosed';
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
        return '::BusinessPanel:SupportTickets:PriorityLow';
      case SupportTicketPriority.Medium:
        return '::BusinessPanel:SupportTickets:PriorityMedium';
      case SupportTicketPriority.High:
        return '::BusinessPanel:SupportTickets:PriorityHigh';
      default:
        return '::BusinessPanel:SupportTickets:PriorityUrgent';
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
        this.toaster.success('::BusinessPanel:SupportTickets:ReplySentMessage');
        this.loadDetail(ticketId);
        this.load();
      },
      error: () => {
        this.isSendingReply.set(false);
        this.toaster.error('::BusinessPanel:SupportTickets:ReplyErrorMessage');
      },
    });
  }

  protected openCreateModal(): void {
    this.createForm.reset({ subject: '', body: '', priority: SupportTicketPriority.Medium });
    this.createModalOpen.set(true);
  }

  protected closeCreateModal(): void {
    this.createModalOpen.set(false);
  }

  protected submitCreateForm(): void {
    if (this.createForm.invalid) {
      this.createForm.markAllAsTouched();
      return;
    }

    const value = this.createForm.getRawValue();
    this.isCreating.set(true);
    this.ticketsService.create({ subject: value.subject, body: value.body, priority: value.priority }).subscribe({
      next: () => {
        this.isCreating.set(false);
        this.createModalOpen.set(false);
        this.toaster.success('::BusinessPanel:SupportTickets:CreatedMessage');
        this.pageIndex.set(0);
        this.load();
      },
      error: () => {
        this.isCreating.set(false);
        this.toaster.error('::BusinessPanel:SupportTickets:CreateErrorMessage');
      },
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

  private load(): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);

    const status =
      this.statusFilterValue() === '' ? null : (Number(this.statusFilterValue()) as SupportTicketStatus);
    const priority =
      this.priorityFilterValue() === '' ? null : (Number(this.priorityFilterValue()) as SupportTicketPriority);

    // tenantId: null — irrelevant even if it weren't, since GetListAsync now forces this caller's own
    // scope server-side regardless of what's passed here (see the file comment above).
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
