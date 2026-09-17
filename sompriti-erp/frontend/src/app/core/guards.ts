import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';
import { Role } from './models';

/** Requires a session; sends users who must change their password to that page first. */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (!auth.isLoggedIn()) return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
  if (auth.mustChangePassword() && !state.url.startsWith('/change-password')) return router.createUrlTree(['/change-password']);
  return true;
};

/** Only for guests (login, register...). */
export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isLoggedIn() ? inject(Router).createUrlTree(['/']) : true;
};

/** Restricts a route to roles listed in route data: { roles: ['ADMIN'] }. */
export const roleGuard: CanActivateFn = (route) => {
  const auth = inject(AuthService);
  const roles = (route.data?.['roles'] as Role[] | undefined) ?? [];
  if (roles.length === 0 || auth.hasRole(...roles)) return true;
  return inject(Router).createUrlTree(['/']);
};
