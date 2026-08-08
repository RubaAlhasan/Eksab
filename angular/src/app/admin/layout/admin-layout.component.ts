import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService, ConfigStateService, PermissionService } from '@abp/ng.core';

interface AdminNavItem {
  label: string;
  icon: string;
  link: string;
  permission: string;
}

/** Flat for now (Businesses, Categories only) — Dashboard is deliberately not built yet per the backend
 *  readiness doc's implementation order (it composes widgets from pages that need to exist first). Add
 *  entries here as each Admin feature ships; this is the sidebar's own source of truth, independent of
 *  RoutesService's menu tree (that tree still exists, and still governs layout resolution — see
 *  route.provider.ts's comment — but a custom sidebar reading its own plain array is simpler than
 *  consuming Lepton-X's menu-rendering internals for a fully custom shell). */
const ADMIN_NAV: AdminNavItem[] = [
  { label: 'Businesses', icon: 'fa-building', link: '/admin/businesses', permission: 'Eksabli.Tenants.View' },
  { label: 'Categories', icon: 'fa-tags', link: '/admin/categories', permission: 'Eksabli.Tenants.View' },
];

@Component({
  selector: 'app-admin-layout',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './admin-layout.component.html',
  styleUrls: ['./admin-layout.component.scss'],
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
})
export class AdminLayoutComponent {
  private readonly authService = inject(AuthService);
  private readonly permissionService = inject(PermissionService);
  private readonly configState = inject(ConfigStateService);

  protected readonly mobileNavOpen = signal(false);
  protected readonly darkMode = signal(false);

  protected readonly navItems = computed(() =>
    ADMIN_NAV.filter((item) => this.permissionService.getGrantedPolicy(item.permission)),
  );

  protected readonly currentUserName = computed(() => {
    const currentUser = this.configState.getOne('currentUser') as { userName?: string } | undefined;
    return currentUser?.userName ?? 'Admin';
  });

  protected toggleMobileNav(): void {
    this.mobileNavOpen.update((open) => !open);
  }

  protected closeMobileNav(): void {
    this.mobileNavOpen.set(false);
  }

  protected toggleDarkMode(): void {
    this.darkMode.update((dark) => !dark);
  }

  protected logout(): void {
    this.authService.logout().subscribe();
  }
}
