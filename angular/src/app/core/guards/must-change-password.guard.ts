import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';
import { AccountStatusService } from '../../proxy/controllers/account-status.service';

/**
 * Gates the `/business` subtree (same slot/shape as `businessApprovalGuard` — see business.guard.ts)
 * on `IdentityUser.ShouldChangePasswordOnNextLogin`, a real, built-in ABP Identity column (confirmed
 * in the EF model). Set to `true` for every newly invited staff account
 * (`EmployeeAssignmentAppService.InviteAsync`) — the temp password shown once in the invite modal is
 * meant to get someone in exactly once, not become their permanent credential. Redirects to
 * `/account/manage` (ABP's own stock change-password page — already wired up, nothing new to build
 * there) until they set a real password; ABP's own change-password flow clears this flag as part of
 * that same operation, so this guard naturally stops redirecting on the next navigation once they have.
 *
 * "Fail open" (allow navigation) if the status call itself errors — same reasoning
 * `businessApprovalGuard`'s own comment gives: this is a UX nudge layered on top of the real permission
 * checks on every endpoint, not the actual security boundary, so a transient network failure here
 * shouldn't lock staff out entirely.
 */
export const mustChangePasswordGuard: CanActivateFn = () => {
  const accountStatusService = inject(AccountStatusService);
  const router = inject(Router);
  return accountStatusService.getMustChangePassword().pipe(
    map((mustChange) => (mustChange ? router.createUrlTree(['/account/manage']) : true)),
    catchError(() => of(true)),
  );
};
