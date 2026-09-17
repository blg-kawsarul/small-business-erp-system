import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, map, of, shareReplay, tap, throwError } from 'rxjs';
import { API_BASE } from './api.service';
import { AuthResponse, CurrentUser, Role } from './models';

const STORAGE_KEY = 'sompriti.auth';

interface StoredSession {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  user: CurrentUser;
}

/** Holds the session (tokens + current user) and exposes role helpers as signals. */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly session = signal<StoredSession | null>(readSession());
  private refreshInFlight: Observable<string> | null = null;

  readonly user = computed(() => this.session()?.user ?? null);
  readonly isLoggedIn = computed(() => this.session() !== null);
  readonly role = computed<Role | null>(() => this.user()?.role ?? null);
  readonly isAdmin = computed(() => this.role() === 'ADMIN');
  readonly isManager = computed(() => this.role() === 'MANAGER');
  readonly isUser = computed(() => this.role() === 'USER');
  readonly canSales = computed(() => this.role() === 'ADMIN' || this.role() === 'MANAGER');
  readonly mustChangePassword = computed(() => this.user()?.mustChangePassword === true);

  get accessToken(): string | null {
    return this.session()?.accessToken ?? null;
  }

  hasRole(...roles: Role[]): boolean {
    const r = this.role();
    return r !== null && roles.includes(r);
  }

  login(email: string, password: string): Observable<CurrentUser> {
    return this.http.post<AuthResponse>(`${API_BASE}/auth/login`, { email, password }).pipe(
      tap((r) => this.store(r)),
      map((r) => r.user),
    );
  }

  register(body: { userName: string; email: string; phoneNumber: string; password: string }): Observable<CurrentUser> {
    return this.http.post<AuthResponse>(`${API_BASE}/auth/register`, body).pipe(
      tap((r) => this.store(r)),
      map((r) => r.user),
    );
  }

  /** Refreshes the access token once for all concurrent callers. */
  refresh(): Observable<string> {
    const current = this.session();
    if (!current) return throwError(() => new Error('Not logged in'));
    if (!this.refreshInFlight) {
      this.refreshInFlight = this.http
        .post<AuthResponse>(`${API_BASE}/auth/refresh`, { refreshToken: current.refreshToken })
        .pipe(
          tap((r) => this.store(r)),
          map((r) => r.accessToken),
          catchError((err) => {
            this.clear();
            return throwError(() => err);
          }),
          finalize(() => (this.refreshInFlight = null)),
          shareReplay(1),
        );
    }
    return this.refreshInFlight;
  }

  /** Reloads the current user (role, links, must-change-password flag). */
  reloadMe(): Observable<CurrentUser | null> {
    if (!this.session()) return of(null);
    return this.http.get<CurrentUser>(`${API_BASE}/auth/me`).pipe(
      tap((user) => {
        const s = this.session();
        if (s) this.persist({ ...s, user });
      }),
    );
  }

  logout(redirect = true): void {
    const refreshToken = this.session()?.refreshToken;
    this.clear();
    if (refreshToken) this.http.post(`${API_BASE}/auth/logout`, { refreshToken }).subscribe({ error: () => {} });
    if (redirect) void this.router.navigate(['/login']);
  }

  /** Called when the session can no longer be refreshed. */
  expire(): void {
    this.clear();
    void this.router.navigate(['/login'], { queryParams: { expired: 1 } });
  }

  private store(r: AuthResponse): void {
    this.persist({ accessToken: r.accessToken, accessTokenExpiresAt: r.accessTokenExpiresAt, refreshToken: r.refreshToken, user: r.user });
  }

  private persist(s: StoredSession): void {
    this.session.set(s);
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(s));
    } catch {
      /* storage unavailable: session lives in memory only */
    }
  }

  private clear(): void {
    this.session.set(null);
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      /* ignore */
    }
  }
}

function readSession(): StoredSession | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as StoredSession) : null;
  } catch {
    return null;
  }
}
