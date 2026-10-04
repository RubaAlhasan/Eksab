import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Subject, debounceTime } from 'rxjs';
import { LocalizationPipe, PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { downloadBlob } from '../../shared/utils/download-blob';
import { ReportsService } from '../../proxy/controllers/reports.service';
import { BranchesService } from '../../proxy/controllers/branches.service';
import { EmployeeAssignmentsService } from '../../proxy/controllers/employee-assignments.service';
import type { SmartDealSaleDto } from '../../proxy/reports/models';
import { Currency } from '../../proxy/shared/currency.enum';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { ModalComponent } from '../../shared/components/modal/modal.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { SmartSaleDetailsComponent, SmartSaleDetailsView } from '../../shared/components/smart-sale-details/smart-sale-details.component';
import { startOfMonth, toDateInputValue } from '../../shared/utils/date-input.util';

/**
 * Business Portal > Transactions > "Smart deal sales" tab. Lists completed Buy Now sales at the counter, filtered
 * by branch, staff and completion date, against `ReportsAppService.GetSmartDealSalesAsync`.
 *
 * - Sales are money in the deal's own currency, not points, so this list never mixes with the points ledger tab.
 * - Branch and staff names come from the server row, so only the filter dropdowns need the bulk lookups below.
 * - Open orders are not listed. They are still on the counter, and the counter page shows them.
 */
@Component({
  selector: 'app-business-smart-deal-sales',
  templateUrl: './business-smart-deal-sales.component.html',
  styleUrls: ['./business-smart-deal-sales.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    DecimalPipe,
    ReactiveFormsModule,
    LocalizationPipe,
    LoadingSpinnerComponent,
    ErrorStateComponent,
    EmptyStateComponent,
    PaginationComponent,
    ModalComponent,
    SmartSaleDetailsComponent,
  ],
})
export class BusinessSmartDealSalesComponent implements OnInit {
  private readonly reportsService = inject(ReportsService);
  private readonly branchesService = inject(BranchesService);
  private readonly employeeAssignmentsService = inject(EmployeeAssignmentsService);
  private readonly permissionService = inject(PermissionService);
  private readonly toaster = inject(ToasterService);

  private readonly pageSize = 10;

  // A Deal ID or sale code typed in the search box. Debounced, so a fast typist sends one request, not one per key.
  protected readonly searchValue = signal('');
  private readonly searchInput$ = new Subject<string>();

  protected readonly canExport = computed(() => this.permissionService.getGrantedPolicy('Eksabli.Reports.Export'));
  protected readonly isExporting = signal(false);

  protected readonly sales = signal<SmartDealSaleDto[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly branchFilterValue = signal('');
  protected readonly staffFilterValue = signal('');
  protected readonly pageIndex = signal(0);
  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));

  protected readonly branches = signal<{ id: string; name: string }[]>([]);
  protected readonly employees = signal<{ userId: string; userEmail: string }[]>([]);

  // Built once when a row is opened, so the details view gets a stable object rather than a new one per change check.
  protected readonly selectedSale = signal<SmartSaleDetailsView | null>(null);

  protected readonly form = new FormGroup({
    from: new FormControl(toDateInputValue(startOfMonth(new Date())), { nonNullable: true, validators: [Validators.required] }),
    to: new FormControl(toDateInputValue(new Date()), { nonNullable: true, validators: [Validators.required] }),
  });

  constructor() {
    this.searchInput$
      .pipe(debounceTime(300), takeUntilDestroyed())
      .subscribe(term => {
        this.searchValue.set(term);
        this.pageIndex.set(0);
        this.load();
      });
  }

  protected onSearchInput(event: Event): void {
    this.searchInput$.next((event.target as HTMLInputElement).value.trim());
  }

  ngOnInit(): void {
    this.loadBranches();
    this.loadEmployees();
    this.load();
  }

  protected retry(): void {
    this.load();
  }

  protected onBranchFilterChange(event: Event): void {
    this.branchFilterValue.set((event.target as HTMLSelectElement).value);
    this.pageIndex.set(0);
    this.load();
  }

  protected onStaffFilterChange(event: Event): void {
    this.staffFilterValue.set((event.target as HTMLSelectElement).value);
    this.pageIndex.set(0);
    this.load();
  }

  protected onDateRangeChange(): void {
    if (this.form.invalid) return;
    this.pageIndex.set(0);
    this.load();
  }

  protected goToPage(index: number): void {
    if (index < 0 || index >= this.totalPages()) return;
    this.pageIndex.set(index);
    this.load();
  }

  // The same two-step token export as the points ledger: a short-lived token first, then the file with the filters the
  // table is showing. The file holds every matching sale, not only the page on screen.
  protected exportExcel(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.isExporting.set(true);
    this.reportsService.getSmartDealSalesDownloadToken().subscribe({
      next: tokenResult => {
        this.reportsService
          .getSmartDealSalesAsExcelFile({
            downloadToken: tokenResult.token ?? '',
            search: this.searchValue() || null,
            branchId: this.branchFilterValue() || null,
            staffId: this.staffFilterValue() || null,
            from: new Date(value.from).toISOString(),
            // Include the whole "to" day, not just its midnight instant, as the table does.
            to: new Date(`${value.to}T23:59:59`).toISOString(),
          })
          .subscribe({
            next: blob => {
              this.isExporting.set(false);
              downloadBlob(blob, `SmartDealSales_${value.from}_${value.to}.xlsx`);
            },
            error: () => {
              this.isExporting.set(false);
              this.toaster.error('::BusinessPanel:Transactions:ExportErrorMessage');
            },
          });
      },
      error: () => {
        this.isExporting.set(false);
        this.toaster.error('::BusinessPanel:Transactions:ExportErrorMessage');
      },
    });
  }

  protected openSale(sale: SmartDealSaleDto): void {
    const name = [sale.customerFirstName, sale.customerLastName].filter(Boolean).join(' ').trim();
    this.selectedSale.set({
      smartOfferId: sale.smartOfferId,
      code: sale.code,
      offerTitleEn: sale.offerTitleEn,
      offerTitleAr: sale.offerTitleAr,
      offerDescriptionEn: sale.offerDescriptionEn,
      offerDescriptionAr: sale.offerDescriptionAr,
      quantity: sale.quantity,
      unitPrice: sale.unitPrice,
      basePrice: sale.basePrice,
      totalAmount: sale.totalAmount,
      currency: sale.currency,
      serviceDate: sale.serviceDate,
      placedAt: sale.placedAt,
      completedAt: sale.completedAt,
      customerName: name || null,
      branchName: sale.branchName,
      staffEmail: sale.staffEmail,
    });
  }

  protected closeSale(): void {
    this.selectedSale.set(null);
  }

  protected customerName(sale: SmartDealSaleDto): string {
    const name = [sale.customerFirstName, sale.customerLastName].filter(Boolean).join(' ').trim();
    return name || '—';
  }

  protected currencyCode(currency: Currency | null | undefined): string {
    return currency === Currency.Usd ? 'USD' : 'SYP';
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);

    const value = this.form.getRawValue();

    this.reportsService
      .getSmartDealSales({
        search: this.searchValue() || null,
        branchId: this.branchFilterValue() || null,
        staffId: this.staffFilterValue() || null,
        from: value.from ? new Date(value.from).toISOString() : null,
        // Include the whole "to" day, not just its midnight instant — same reasoning as the points tab.
        to: value.to ? new Date(`${value.to}T23:59:59`).toISOString() : null,
        sorting: undefined,
        skipCount: this.pageIndex() * this.pageSize,
        maxResultCount: this.pageSize,
      })
      .subscribe({
        next: (result) => {
          this.sales.set(result.items ?? []);
          this.totalCount.set(result.totalCount ?? 0);
          this.isLoading.set(false);
        },
        error: () => {
          this.isLoading.set(false);
          this.loadFailed.set(true);
        },
      });
  }

  private loadBranches(): void {
    this.branchesService.getList(
      { sorting: 'name asc', skipCount: 0, maxResultCount: 100 },
      { skipHandleError: true },
    ).subscribe({
      next: (result) => {
        const items = (result.items ?? []).filter((b) => b.id && b.name) as { id: string; name: string }[];
        this.branches.set(items);
      },
      // Best-effort — a MarketingManager (no Eksabli.Branches) just gets no branch filter.
      // skipHandleError: true is load-bearing — see business-branches.component.ts's loadUsage().
      error: () => undefined,
    });
  }

  private loadEmployees(): void {
    this.employeeAssignmentsService.getList({ skipCount: 0, maxResultCount: 500 }, { skipHandleError: true }).subscribe({
      next: (result) => {
        const items = (result.items ?? []).filter((e) => e.userId && e.userEmail) as { userId: string; userEmail: string }[];
        this.employees.set(items);
      },
      // Best-effort — a BranchManager/MarketingManager (no Eksabli.EmployeeAssignments) just gets no staff filter.
      // skipHandleError: true is load-bearing — see business-branches.component.ts's loadUsage() for why.
      error: () => undefined,
    });
  }
}
