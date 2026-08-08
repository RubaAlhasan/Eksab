import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LocalizationPipe, PermissionService } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { CategoriesService } from '../../proxy/controllers/categories.service';
import type { CategoryDto } from '../../proxy/platform/models';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { SearchInputComponent } from '../../shared/components/search-input/search-input.component';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { ModalComponent } from '../../shared/components/modal/modal.component';

interface CategoryFormValue {
  nameAr: string;
  nameEn: string;
  iconBlobName: string;
  parentCategoryId: string;
}

/**
 * Admin Portal > Categories — business taxonomy CRUD, first complete Admin feature built after the
 * Phase 1 foundation (layout, guards, shared components). Wired directly against the real
 * `CategoriesController` (`GetListAsync`/`GetAsync` are `[AllowAnonymous]` — public taxonomy read, per
 * admin-portal-backend-readiness.md §9 — `CreateAsync`/`UpdateAsync`/`DeleteAsync` require
 * `Eksabli.Categories.Create/.Edit/.Delete` respectively). No fields or actions here that the backend
 * doesn't actually support — no "business count" column (no such field on `CategoryDto`), no soft
 * delete/deactivate toggle (only a real `DELETE`, no status field on the entity to deactivate).
 */
@Component({
  selector: 'app-admin-categories',
  templateUrl: './admin-categories.component.html',
  styleUrls: ['./admin-categories.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    LocalizationPipe,
    PageHeaderComponent,
    SearchInputComponent,
    LoadingSpinnerComponent,
    ErrorStateComponent,
    EmptyStateComponent,
    PaginationComponent,
    ModalComponent,
  ],
})
export class AdminCategoriesComponent implements OnInit {
  private readonly categoriesService = inject(CategoriesService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly toaster = inject(ToasterService);
  private readonly permissionService = inject(PermissionService);

  private readonly pageSize = 10;

  protected readonly categories = signal<CategoryDto[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly filterText = signal('');
  protected readonly pageIndex = signal(0);
  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));

  protected readonly canCreate = computed(() => this.permissionService.getGrantedPolicy('Eksabli.Categories.Create'));
  protected readonly canEdit = computed(() => this.permissionService.getGrantedPolicy('Eksabli.Categories.Edit'));
  protected readonly canDelete = computed(() => this.permissionService.getGrantedPolicy('Eksabli.Categories.Delete'));

  protected readonly modalOpen = signal(false);
  protected readonly modalTitle = signal('');
  protected readonly isSaving = signal(false);
  private editingCategoryId: string | null = null;

  protected readonly form = new FormGroup({
    nameAr: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(128)] }),
    nameEn: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(128)] }),
    iconBlobName: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(256)] }),
    parentCategoryId: new FormControl('', { nonNullable: true }),
  });

  ngOnInit(): void {
    this.load();
  }

  protected onSearchInput(value: string): void {
    this.filterText.set(value);
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

  /** Parent-category choices for the form's dropdown. Sourced from the currently-loaded page only —
   *  `CategoryListFilterDto` has no "give me every category, unpaged" shape, so a category on a later
   *  page can't be picked as a parent from here. Acceptable for MVP given the shallow taxonomy this
   *  represents; revisit if the category list grows past a page and this becomes a real limitation. */
  protected readonly parentOptions = computed(() =>
    this.categories().filter((c) => c.id !== this.editingCategoryId),
  );

  /** Best-effort name lookup — only resolves if the parent happens to be on the currently-loaded page,
   *  same limitation as `parentOptions` above (no unpaged "all categories" endpoint exists). Falls back
   *  to the empty-parent dash rather than showing a raw GUID. */
  protected parentName(category: CategoryDto): string | null {
    if (!category.parentCategoryId) return null;
    return this.categories().find((c) => c.id === category.parentCategoryId)?.nameEn ?? null;
  }

  protected openCreateModal(): void {
    this.editingCategoryId = null;
    this.modalTitle.set('::AdminPanel:Categories:NewTitle');
    this.form.reset({ nameAr: '', nameEn: '', iconBlobName: '', parentCategoryId: '' });
    this.modalOpen.set(true);
  }

  protected openEditModal(category: CategoryDto): void {
    this.editingCategoryId = category.id ?? null;
    this.modalTitle.set('::AdminPanel:Categories:EditTitle');
    this.form.reset({
      nameAr: category.nameAr ?? '',
      nameEn: category.nameEn ?? '',
      iconBlobName: category.iconBlobName ?? '',
      parentCategoryId: category.parentCategoryId ?? '',
    });
    this.modalOpen.set(true);
  }

  protected closeModal(): void {
    this.modalOpen.set(false);
  }

  protected submitForm(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue() as CategoryFormValue;
    const payload = {
      nameAr: value.nameAr,
      nameEn: value.nameEn,
      iconBlobName: value.iconBlobName || null,
      parentCategoryId: value.parentCategoryId || null,
    };

    this.isSaving.set(true);
    const request = this.editingCategoryId
      ? this.categoriesService.update(this.editingCategoryId, payload)
      : this.categoriesService.create(payload);

    request.subscribe({
      next: () => {
        this.isSaving.set(false);
        this.modalOpen.set(false);
        this.toaster.success('::AdminPanel:Categories:SavedMessage');
        this.load();
      },
      error: () => {
        this.isSaving.set(false);
        this.toaster.error('::AdminPanel:Categories:SaveErrorMessage');
      },
    });
  }

  protected deleteCategory(category: CategoryDto): void {
    if (!category.id) return;
    this.confirmation
      .warn('::AdminPanel:Categories:DeleteConfirmMessage', '::AdminPanel:Categories:DeleteConfirmTitle')
      .subscribe((status) => {
        if (status !== Confirmation.Status.confirm || !category.id) return;
        this.categoriesService.delete(category.id).subscribe({
          next: () => {
            this.toaster.success('::AdminPanel:Categories:DeletedMessage');
            this.load();
          },
          error: () => this.toaster.error('::AdminPanel:Categories:DeleteErrorMessage'),
        });
      });
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);

    this.categoriesService
      .getList({
        filterText: this.filterText() || null,
        skipCount: this.pageIndex() * this.pageSize,
        maxResultCount: this.pageSize,
        sorting: 'nameEn asc',
      })
      .subscribe({
        next: (result) => {
          this.categories.set(result.items ?? []);
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
