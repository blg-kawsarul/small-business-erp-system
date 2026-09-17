import { DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { PageEvent } from '@angular/material/paginator';
import { Sort } from '@angular/material/sort';
import { Observable, Subject, debounceTime, finalize, switchMap, catchError, EMPTY } from 'rxjs';
import { errorMessage } from '../core/api.service';
import { Paged } from '../core/models';

export interface ListQuery {
  page: number;
  pageSize: number;
  search: string;
  sort: string;
}

/**
 * Signal-based state for a server-paged list: items, total, paging, search, sort and loading.
 * Must be created in an injection context (a component field initializer).
 */
export class ListState<T> {
  readonly items = signal<T[]>([]);
  readonly total = signal(0);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly query = signal<ListQuery>({ page: 1, pageSize: 20, search: '', sort: '' });

  private readonly reload$ = new Subject<void>();
  private readonly search$ = new Subject<string>();

  constructor(fetch: (q: ListQuery) => Observable<Paged<T>>, pageSize = 20) {
    this.query.update((q) => ({ ...q, pageSize }));
    const destroyRef = inject(DestroyRef);

    this.reload$
      .pipe(
        switchMap(() => {
          this.loading.set(true);
          this.error.set(null);
          return fetch(this.query()).pipe(
            catchError((e) => {
              this.error.set(errorMessage(e));
              return EMPTY;
            }),
            finalize(() => this.loading.set(false)),
          );
        }),
        takeUntilDestroyed(destroyRef),
      )
      .subscribe((result) => {
        this.items.set(result.items);
        this.total.set(result.totalCount);
      });

    this.search$.pipe(debounceTime(300), takeUntilDestroyed(destroyRef)).subscribe((search) => {
      this.query.update((q) => ({ ...q, search, page: 1 }));
      this.reload();
    });
  }

  reload(): void {
    this.reload$.next();
  }

  search(term: string): void {
    this.search$.next(term);
  }

  resetToFirstPage(): void {
    this.query.update((q) => ({ ...q, page: 1 }));
    this.reload();
  }

  onPage(e: PageEvent): void {
    this.query.update((q) => ({ ...q, page: e.pageIndex + 1, pageSize: e.pageSize }));
    this.reload();
  }

  onSort(s: Sort): void {
    this.query.update((q) => ({ ...q, sort: s.direction ? (s.direction === 'desc' ? '-' : '') + s.active : '', page: 1 }));
    this.reload();
  }
}
