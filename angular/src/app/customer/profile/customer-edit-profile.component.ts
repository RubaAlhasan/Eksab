import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CustomerProfileService } from '../../proxy/controllers/customer-profile.service';
import { CustomerGender } from '../../proxy/customer-profiles/customer-gender.enum';
import { SkeletonListComponent } from '../../shared/components/skeleton-list/skeleton-list.component';

/** No avatar/photo upload — no blob-upload UI pattern exists anywhere in this app yet outside the
 *  Business Portal's logo uploader (a different, staff-only flow), and `UpdateCustomerProfileDto` has no
 *  photo field regardless. */
@Component({
  selector: 'app-customer-edit-profile',
  templateUrl: './customer-edit-profile.component.html',
  styleUrls: ['./customer-edit-profile.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, LocalizationPipe, SkeletonListComponent],
})
export class CustomerEditProfileComponent implements OnInit {
  private readonly customerProfileService = inject(CustomerProfileService);
  private readonly toaster = inject(ToasterService);
  private readonly router = inject(Router);

  protected readonly Gender = CustomerGender;
  protected readonly isLoading = signal(true);
  protected readonly isSaving = signal(false);

  protected readonly form = new FormGroup({
    firstName: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(64)] }),
    lastName: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(64)] }),
    dateOfBirth: new FormControl('', { nonNullable: true }),
    gender: new FormControl(CustomerGender.Unspecified, { nonNullable: true }),
  });

  ngOnInit(): void {
    this.customerProfileService.getMyProfile().subscribe({
      next: profile => {
        this.form.reset({
          firstName: profile.firstName ?? '',
          lastName: profile.lastName ?? '',
          dateOfBirth: profile.dateOfBirth ? profile.dateOfBirth.substring(0, 10) : '',
          gender: profile.gender ?? CustomerGender.Unspecified,
        });
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  protected submit(): void {
    if (this.form.invalid || this.isSaving()) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.isSaving.set(true);
    this.customerProfileService
      .updateMyProfile({
        firstName: value.firstName || null,
        lastName: value.lastName || null,
        dateOfBirth: value.dateOfBirth ? new Date(value.dateOfBirth).toISOString() : null,
        gender: value.gender,
      })
      .subscribe({
        next: () => {
          this.isSaving.set(false);
          this.toaster.success('::Wallet:Profile:SavedMessage');
          void this.router.navigateByUrl('/customer/profile');
        },
        error: () => this.isSaving.set(false),
      });
  }
}
