import { AuthService, authGuard, eLayoutType, permissionGuard } from '@abp/ng.core';
import { inject } from '@angular/core';
import { CanActivateFn, Router, Routes } from '@angular/router';

/** OAuth redirectUri always lands back on '/' — send an already-authenticated visitor on to /home instead of the public landing page. */
const redirectAuthenticatedToHomeGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);
  return authService.isAuthenticated ? router.createUrlTree(['/home']) : true;
};

export const APP_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./landing/landing.component').then(c => c.LandingComponent),
    data: { layout: eLayoutType.empty },
    canActivate: [redirectAuthenticatedToHomeGuard],
  },
  {
    path: 'home',
    loadComponent: () => import('./home/home.component').then(c => c.HomeComponent),
    canActivate: [authGuard],
  },
  {
    path: 'account',
    loadChildren: () => import('@abp/ng.account').then(c => c.createRoutes()),
  },
  {
    path: 'identity',
    loadChildren: () => import('@abp/ng.identity').then(c => c.createRoutes()),
  },
  {
    path: 'tenant-management',
    loadChildren: () => import('@abp/ng.tenant-management').then(c => c.createRoutes()),
  },
  {
    path: 'setting-management',
    loadChildren: () => import('@abp/ng.setting-management').then(c => c.createRoutes()),
  },
  {
    path: 'books',
    loadComponent: () => import('./book/book.component').then(c => c.BookComponent),
    canActivate: [authGuard, permissionGuard],
  },
  {
    path: 'authors',
    loadComponent: () => import('./author/author.component').then(c => c.AuthorComponent),
    canActivate: [authGuard, permissionGuard],
  },
];
