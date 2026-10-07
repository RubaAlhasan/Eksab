import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { EnvironmentService, LocalizationPipe, PermissionService } from '@abp/ng.core';
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
  parentCategoryId: string;
}

// Mirrors BusinessProfileConsts/CategoryConsts server-side: a category icon is a small glyph, not a
// photo, so it gets a lower cap than a business logo's.
const ALLOWED_ICON_TYPES = ['image/png', 'image/jpeg', 'image/webp'];
const MAX_ICON_BYTES = 512 * 1024;

/**
 * Admin Portal > Categories — business taxonomy CRUD, first complete Admin feature built after the
 * Phase 1 foundation (layout, guards, shared components). Wired directly against the real
 * `CategoriesController` (`GetListAsync`/`GetAsync` are `[AllowAnonymous]` — public taxonomy read, per
 * admin-portal-backend-readiness.md §9 — `CreateAsync`/`UpdateAsync`/`DeleteAsync` require
 * `Eksabli.Categories.Create/.Edit/.Delete` respectively). No fields or actions here that the backend
 * doesn't actually support — no soft delete/deactivate toggle (only a real `DELETE`, no status field
 * on the entity to deactivate).
 *
 * `businessCount` (shown per-row, matching the prototype's design) required a real backend change —
 * `CategoryDto.BusinessCount` is now computed server-side by `CategoryAppService` from
 * `BusinessProfile.CategoryId` across every tenant (`IDataFilter.Disable<IMultiTenant>()`, same
 * pattern `AdminTenantAppService` uses), not invented client-side. See `CategoryAppService.cs`.
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
  private readonly environmentService = inject(EnvironmentService);

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
  protected readonly isUploadingIcon = signal(false);
  protected editingCategoryId: string | null = null;

  // Tracks the icon of whatever category the modal is currently editing, independent of the paged
  // `categories()` list — upload/remove responses update this directly so the preview reflects the
  // save without needing a full list reload.
  protected readonly editingCategoryIcon = signal<string | null>(null);

  protected readonly iconUrl = computed(() => {
    const blobName = this.editingCategoryIcon();
    if (!this.editingCategoryId || !blobName) return null;
    return `${this.environmentService.getApiUrl('default')}/api/app/category/${this.editingCategoryId}/icon?v=${encodeURIComponent(blobName)}`;
  });

  // The row thumbnail reads straight off the paged list, not `editingCategoryIcon` (that's modal-only state).
  protected iconUrlFor(category: CategoryDto): string | null {
    if (!category.id || !category.iconBlobName) return null;
    return `${this.environmentService.getApiUrl('default')}/api/app/category/${category.id}/icon?v=${encodeURIComponent(category.iconBlobName)}`;
  }

  protected readonly form = new FormGroup({
    nameAr: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(128)] }),
    nameEn: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(128)] }),
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
    this.editingCategoryIcon.set(null);
    this.modalTitle.set('::AdminPanel:Categories:NewTitle');
    this.form.reset({ nameAr: '', nameEn: '', parentCategoryId: '' });
    this.modalOpen.set(true);
  }

  protected openEditModal(category: CategoryDto): void {
    this.editingCategoryId = category.id ?? null;
    this.editingCategoryIcon.set(category.iconBlobName ?? null);
    this.modalTitle.set('::AdminPanel:Categories:EditTitle');
    this.form.reset({
      nameAr: category.nameAr ?? '',
      nameEn: category.nameEn ?? '',
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
      parentCategoryId: value.parentCategoryId || null,
    };

    this.isSaving.set(true);
    const request = this.editingCategoryId
      ? this.categoriesService.update(this.editingCategoryId, payload)
      : this.categoriesService.create(payload);

    request.subscribe({
      next: (result) => {
        this.isSaving.set(false);
        this.toaster.success('::AdminPanel:Categories:SavedMessage');
        this.load();

        if (this.editingCategoryId) {
          this.modalOpen.set(false);
        } else {
          // Stay open and switch into edit mode so a freshly-created category can get an icon
          // right away — uploading one requires an id, which doesn't exist until this save.
          this.editingCategoryId = result.id ?? null;
          this.editingCategoryIcon.set(result.iconBlobName ?? null);
          this.modalTitle.set('::AdminPanel:Categories:EditTitle');
        }
      },
      error: () => {
        this.isSaving.set(false);
        this.toaster.error('::AdminPanel:Categories:SaveErrorMessage');
      },
    });
  }

  protected onIconFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    input.value = ''; // reset so re-selecting the same file still fires a change event

    if (!file || !this.editingCategoryId) return;

    if (!ALLOWED_ICON_TYPES.includes(file.type)) {
      this.toaster.error('::AdminPanel:Categories:InvalidIconTypeMessage');
      return;
    }
    if (file.size > MAX_ICON_BYTES) {
      this.toaster.error('::AdminPanel:Categories:IconTooLargeMessage');
      return;
    }

    this.isUploadingIcon.set(true);
    this.categoriesService.uploadIcon(this.editingCategoryId, file).subscribe({
      next: (category) => {
        this.isUploadingIcon.set(false);
        this.editingCategoryIcon.set(category.iconBlobName ?? null);
        this.toaster.success('::AdminPanel:Categories:IconUploadedMessage');
        this.load();
      },
      error: () => {
        this.isUploadingIcon.set(false);
        this.toaster.error('::AdminPanel:Categories:IconUploadErrorMessage');
      },
    });
  }

  protected removeIcon(): void {
    if (!this.editingCategoryId) return;

    this.isUploadingIcon.set(true);
    this.categoriesService.removeIcon(this.editingCategoryId).subscribe({
      next: (category) => {
        this.isUploadingIcon.set(false);
        this.editingCategoryIcon.set(category.iconBlobName ?? null);
        this.toaster.success('::AdminPanel:Categories:IconRemovedMessage');
        this.load();
      },
      error: () => {
        this.isUploadingIcon.set(false);
        this.toaster.error('::AdminPanel:Categories:IconRemoveErrorMessage');
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
