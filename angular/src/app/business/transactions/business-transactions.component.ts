import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ReportsService } from '../../proxy/controllers/reports.service';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';

function startOfMonth(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth(), 1);
}

function toDateInputValue(date: Date): string {
  return date.toISOString().substring(0, 10);
}

/**
 * Business Portal > Transactions — mirrors prototype/business/transactions.html's INTENT ("the detail
 * behind Analytics"), but not its shape. `[MISSING BACKEND CAPABILITY]`: there is no live, paged,
 * tenant-wide points-transaction list endpoint anywhere in this codebase (confirmed while building
 * Dashboard earlier this session — `IWalletAppService.GetMyTransactionHistoryAsync` is scoped to the
 * calling CUSTOMER's own wallet, self-service only; `IReportsAppService`'s only tenant-wide
 * transaction read is `GetTransactionsAsExcelFileAsync`, a bulk Excel *export*, not a live list — see
 * CLAUDE.md's Excel-export pattern). So the prototype's own filterable/paginated 4,820-row ledger
 * table (Type/Branch/Staff/Date filters, live grid) cannot be built for real — none of those filters
 * exist on `TransactionsExcelDownloadDto` (confirmed: it's just `{ DownloadToken, From, To }`, no
 * Type/Branch/Staff fields at all).
 *
 * What IS real, and is this page's entire content: a date-range picker + the real two-step
 * token-gated Excel export (`GetTransactionsDownloadTokenAsync()` → `GetTransactionsAsExcelFileAsync()`,
 * same pattern as Coupons' export, `[AllowAnonymous]` on the file-stream endpoint itself since the
 * token is the real auth check). This directly fulfills the "reasonable follow-up" this session's own
 * Dashboard page comment flagged ("wiring a real Export Transactions action") rather than leaving it
 * unbuilt. Default range is the current calendar month, matching `ReportsAppService`'s own "this
 * month" framing used elsewhere (Notifications' stat tiles).
 */
@Component({
  selector: 'app-business-transactions',
  templateUrl: './business-transactions.component.html',
  styleUrls: ['./business-transactions.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, LocalizationPipe, PageHeaderComponent],
})
export class BusinessTransactionsComponent {
  private readonly reportsService = inject(ReportsService);
  private readonly toaster = inject(ToasterService);

  protected readonly isExporting = signal(false);

  protected readonly form = new FormGroup({
    from: new FormControl(toDateInputValue(startOfMonth(new Date())), { nonNullable: true, validators: [Validators.required] }),
    to: new FormControl(toDateInputValue(new Date()), { nonNullable: true, validators: [Validators.required] }),
  });

  protected exportExcel(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.isExporting.set(true);
    this.reportsService.getTransactionsDownloadToken().subscribe({
      next: (tokenResult) => {
        this.reportsService
          .getTransactionsAsExcelFile({
            downloadToken: tokenResult.token ?? '',
            from: new Date(value.from).toISOString(),
            // Include the whole "to" day, not just its midnight instant.
            to: new Date(`${value.to}T23:59:59`).toISOString(),
          })
          .subscribe({
            next: (blob) => {
              this.isExporting.set(false);
              this.downloadBlob(blob, `Transactions_${value.from}_${value.to}.xlsx`);
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

  private downloadBlob(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    URL.revokeObjectURL(url);
  }
}
