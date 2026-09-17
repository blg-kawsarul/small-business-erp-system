import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { OrderKind, ProblemDetails } from './models';

/** Same-origin in the browser; the full server address in the Android app. */
export const API_BASE = `${environment.apiBaseUrl}/api/v1`;

type QueryValue = string | number | boolean | null | undefined;

/** Thin wrapper around HttpClient that prefixes the API base path and drops empty query values. */
@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);

  get<T>(path: string, query?: Record<string, QueryValue>): Observable<T> {
    return this.http.get<T>(API_BASE + path, { params: toParams(query) });
  }

  post<T>(path: string, body: unknown = {}): Observable<T> {
    return this.http.post<T>(API_BASE + path, body);
  }

  put<T>(path: string, body: unknown): Observable<T> {
    return this.http.put<T>(API_BASE + path, body);
  }

  delete<T = void>(path: string, query?: Record<string, QueryValue>): Observable<T> {
    return this.http.delete<T>(API_BASE + path, { params: toParams(query) });
  }

  blob(path: string, query?: Record<string, QueryValue>): Observable<Blob> {
    return this.http.get(API_BASE + path, { params: toParams(query), responseType: 'blob' });
  }
}

function toParams(query?: Record<string, QueryValue>): HttpParams {
  let params = new HttpParams();
  if (!query) return params;
  for (const [key, value] of Object.entries(query)) {
    if (value === null || value === undefined || value === '') continue;
    params = params.set(key, String(value));
  }
  return params;
}

/** Extracts a user-friendly message and field errors from an API error. */
export function problemOf(error: unknown): ProblemDetails {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) return { status: 0, title: 'Cannot reach the server. Check your connection.' };
    const body = error.error;
    if (body && typeof body === 'object') {
      const p = body as ProblemDetails;
      return { ...p, status: p.status ?? error.status, title: p.detail || p.title || error.message };
    }
    if (error.status === 429) return { status: 429, title: 'Too many attempts. Please wait a minute and try again.' };
    return { status: error.status, title: error.message };
  }
  return { title: error instanceof Error ? error.message : 'Something went wrong.' };
}

export function errorMessage(error: unknown): string {
  return problemOf(error).title ?? 'Something went wrong.';
}

export function orderApiPath(kind: OrderKind): string {
  return kind === 'purchase' ? '/purchase-orders' : '/sales-orders';
}

/** Today's date in Bangladesh time as yyyy-MM-dd. */
export function todayIso(): string {
  const now = new Date(Date.now() + 6 * 3600 * 1000);
  return now.toISOString().slice(0, 10);
}

/** Converts a Date (from a date picker) to yyyy-MM-dd without time-zone shifts. */
export function dateToIso(value: Date | string | null | undefined): string | null {
  if (!value) return null;
  if (typeof value === 'string') return value.slice(0, 10);
  const y = value.getFullYear();
  const m = String(value.getMonth() + 1).padStart(2, '0');
  const d = String(value.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

/** Converts yyyy-MM-dd to a local Date for date pickers. */
export function isoToDate(value: string | null | undefined): Date | null {
  if (!value) return null;
  const [y, m, d] = value.slice(0, 10).split('-').map(Number);
  return new Date(y, m - 1, d);
}
