import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { ConfigStateService, LocalizationPipe, PermissionService } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { ReviewsService } from '../../proxy/controllers/reviews.service';
import type { ReviewDto } from '../../proxy/reviews/models';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { StarRatingComponent } from '../../shared/components/star-rating/star-rating.component';

const PAGE_SIZE = 10;

/**
 * Business Portal > Reviews — the moderation side of the customer-facing rating feature
 * (CustomerReviewAppService/ReviewsController, Eksabli.Reviews.* permissions). Reads reuse the exact
 * same public `GetListAsync` any visitor's Store page already calls — there's nothing to see here a
 * stranger couldn't already see on the business's own public page, so no separate business-only read
 * endpoint exists or is needed. Only the delete button is actually gated, on
 * `Eksabli.Reviews.Moderate`, via `ModerateDeleteAsync`. No edit action: a business can remove an
 * abusive review, never rewrite a customer's words.
 */
@Component({
  selector: 'app-business-reviews',
  templateUrl: './business-reviews.component.html',
  styleUrls: ['./business-reviews.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    DecimalPipe,
    LocalizationPipe,
    PageHeaderComponent,
    LoadingSpinnerComponent,
    ErrorStateComponent,
    EmptyStateComponent,
    PaginationComponent,
    StarRatingComponent,
  ],
})
export class BusinessReviewsComponent implements OnInit {
  private readonly reviewsService = inject(ReviewsService);
  private readonly configState = inject(ConfigStateService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly toaster = inject(ToasterService);
  private readonly permissionService = inject(PermissionService);

  private readonly pageSize = PAGE_SIZE;
  private tenantId = '';

  protected readonly canModerate = computed(() => this.permissionService.getGrantedPolicy('Eksabli.Reviews.Moderate'));

  protected readonly reviews = signal<ReviewDto[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly averageRating = signal(0);
  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly pageIndex = signal(0);
  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));

  // Per-row, not a single busy flag — staff could plausibly act on a different row while one delete
  // is still in flight, same reasoning as business-customers.component.ts's freezeBusyIds.
  protected readonly busyReviewIds = signal<Set<string>>(new Set());

  ngOnInit(): void {
    const currentTenant = this.configState.getOne('currentTenant') as { id?: string | null } | undefined;
    this.tenantId = currentTenant?.id ?? '';
    this.load();
  }

  protected retry(): void {
    this.load();
  }

  protected goToPage(index: number): void {
    if (index < 0 || index >= this.totalPages()) return;
    this.pageIndex.set(index);
    this.load();
  }

  protected deleteReview(review: ReviewDto): void {
    const id = review.id;
    if (!id || this.busyReviewIds().has(id)) return;

    this.confirmation
      .warn('::BusinessPanel:Reviews:DeleteConfirmMessage', '::BusinessPanel:Reviews:DeleteConfirmTitle')
      .subscribe(status => {
        if (status !== Confirmation.Status.confirm) return;

        this.busyReviewIds.update(ids => new Set(ids).add(id));
        this.reviewsService.moderateDelete(id).subscribe({
          next: () => {
            this.clearBusy(id);
            this.toaster.success('::BusinessPanel:Reviews:DeletedMessage');
            this.load();
          },
          error: () => {
            this.clearBusy(id);
            this.toaster.error('::BusinessPanel:Reviews:DeleteErrorMessage');
          },
        });
      });
  }

  private clearBusy(id: string): void {
    this.busyReviewIds.update(ids => {
      const next = new Set(ids);
      next.delete(id);
      return next;
    });
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);
    this.reviewsService
      .getList(this.tenantId, {
        skipCount: this.pageIndex() * this.pageSize,
        maxResultCount: this.pageSize,
        sorting: 'creationTime desc',
      })
      .subscribe({
        next: result => {
          this.reviews.set(result.items ?? []);
          this.totalCount.set(result.totalCount ?? 0);
          this.averageRating.set(result.averageRating ?? 0);
          this.isLoading.set(false);
        },
        error: () => {
          this.isLoading.set(false);
          this.loadFailed.set(true);
        },
      });
  }
}
