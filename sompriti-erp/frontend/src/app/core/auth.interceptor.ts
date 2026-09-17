import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

const NO_AUTH = ['/auth/login', '/auth/register', '/auth/refresh', '/auth/forgot-password', '/auth/reset-password', '/auth/logout'];

/** Adds the bearer token, refreshes once on 401, and redirects when a password change is required. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const skip = NO_AUTH.some((p) => req.url.includes(p));
  const withToken = (r: HttpRequest<unknown>, token: string | null) =>
    token && !skip ? r.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : r;

  return next(withToken(req, auth.accessToken)).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || skip) return throwError(() => error);

      if (error.status === 401 && auth.isLoggedIn()) {
        return auth.refresh().pipe(
          switchMap((token) => next(withToken(req, token))),
          catchError((refreshError) => {
            auth.expire();
            return throwError(() => refreshError);
          }),
        );
      }

      if (error.status === 403 && error.error?.code === 'PASSWORD_CHANGE_REQUIRED') {
        void router.navigate(['/change-password']);
      }
      return throwError(() => error);
    }),
  );
};
