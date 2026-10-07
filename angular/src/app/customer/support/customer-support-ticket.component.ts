import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { SupportTicketsService } from '../../proxy/controllers/support-tickets.service';
import type { SupportTicketDto, SupportTicketMessageDto } from '../../proxy/platform/models';
import { SupportTicketStatus } from '../../proxy/platform/support-ticket-status.enum';
import { SupportTicketPriority } from '../../proxy/platform/support-ticket-priority.enum';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { SkeletonListComponent } from '../../shared/components/skeleton-list/skeleton-list.component';
import { StatusBadgeComponent, StatusBadgeVariant } from '../../shared/components/status-badge/status-badge.component';

// Mirrors SupportTicketConsts / SupportTicketMessageConsts on the backend, so the form refuses what the server would refuse.
const MAX_SUBJECT_LENGTH = 200;
const MAX_BODY_LENGTH = 4000;

/**
 * One page for two jobs, picked by the route: `/customer/support/new` opens the request form, and
 * `/customer/support/:ticketId` shows one of the customer's tickets with its replies and a reply box.
 */
@Component({
  selector: 'app-customer-support-ticket',
  templateUrl: './customer-support-ticket.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    ReactiveFormsModule,
    LocalizationPipe,
    ErrorStateComponent,
    SkeletonListComponent,
    StatusBadgeComponent,
  ],
})
export class CustomerSupportTicketComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly supportTicketsService = inject(SupportTicketsService);

  protected readonly maxSubjectLength = MAX_SUBJECT_LENGTH;
  protected readonly maxBodyLength = MAX_BODY_LENGTH;

  protected readonly ticketId = signal<string | null>(null);
  protected readonly isNew = computed(() => this.ticketId() === null);
  protected readonly ticket = signal<SupportTicketDto | null>(null);
  protected readonly messages = signal<SupportTicketMessageDto[]>([]);
  protected readonly isLoading = signal(false);
  protected readonly loadFailed = signal(false);
  protected readonly isSaving = signal(false);

  protected readonly newForm = new FormGroup({
    subject: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(MAX_SUBJECT_LENGTH)],
    }),
    body: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(MAX_BODY_LENGTH)],
    }),
  });

  protected readonly replyForm = new FormGroup({
    body: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(MAX_BODY_LENGTH)],
    }),
  });

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      const id = params.get('ticketId');
      this.ticketId.set(id);
      if (id) {
        // Angular reuses this component instance across a same-route, different-ticket navigation (see
        // customer-points.component.ts's identical comment on why paramMap is read live rather than once). A draft
        // reply for the ticket just left behind must not still be sitting in the box for the one just opened.
        this.replyForm.reset();
        this.load(id);
      }
    });
  }

  protected retry(): void {
    const id = this.ticketId();
    if (id) this.load(id);
  }

  // Replies from the customer are sent by the customer's own user id, which is also the ticket's CustomerId.
  protected isMine(message: SupportTicketMessageDto): boolean {
    return !!message.senderId && message.senderId === this.ticket()?.customerId;
  }

  protected statusLabelKey(status: SupportTicketStatus | undefined): string {
    switch (status) {
      case SupportTicketStatus.InProgress:
        return '::Wallet:Support:StatusInProgress';
      case SupportTicketStatus.Resolved:
        return '::Wallet:Support:StatusResolved';
      case SupportTicketStatus.Closed:
        return '::Wallet:Support:StatusClosed';
      default:
        return '::Wallet:Support:StatusOpen';
    }
  }

  protected statusVariant(status: SupportTicketStatus | undefined): StatusBadgeVariant {
    switch (status) {
      case SupportTicketStatus.InProgress:
        return 'info';
      case SupportTicketStatus.Resolved:
        return 'success';
      case SupportTicketStatus.Closed:
        return 'neutral';
      default:
        return 'warning';
    }
  }

  protected submitNew(): void {
    if (this.newForm.invalid || this.isSaving()) {
      this.newForm.markAllAsTouched();
      return;
    }

    const value = this.newForm.getRawValue();
    this.isSaving.set(true);
    this.supportTicketsService
      .create({ subject: value.subject.trim(), body: value.body.trim(), priority: SupportTicketPriority.Medium })
      .subscribe({
        next: ticket => {
          this.isSaving.set(false);
          void this.router.navigate(['/customer/support', ticket.id]);
        },
        // The interceptor already shows the server's own message.
        error: () => this.isSaving.set(false),
      });
  }

  protected sendReply(): void {
    const id = this.ticketId();
    if (!id || this.replyForm.invalid || this.isSaving()) {
      this.replyForm.markAllAsTouched();
      return;
    }

    this.isSaving.set(true);
    this.supportTicketsService.addMessage(id, { body: this.replyForm.getRawValue().body.trim() }).subscribe({
      next: message => {
        this.isSaving.set(false);
        this.messages.update(list => [...list, message]);
        this.replyForm.reset();
      },
      error: () => this.isSaving.set(false),
    });
  }

  private load(id: string): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);
    this.ticket.set(null);
    this.messages.set([]);
    this.supportTicketsService.get(id).subscribe({
      next: ticket => {
        this.ticket.set(ticket);
        this.messages.set(ticket.messages ?? []);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.loadFailed.set(true);
      },
    });
  }
}
