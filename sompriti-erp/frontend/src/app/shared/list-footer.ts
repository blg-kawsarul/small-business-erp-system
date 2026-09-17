import { Component, inject, input } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatPaginatorModule } from '@angular/material/paginator';
import { LayoutService } from '../core/layout.service';
import { ListState } from './list-state';

/** Paginator on desktop, "Load more" on phones. */
@Component({
  selector: 'app-list-footer',
  imports: [MatPaginatorModule, MatButtonModule],
  template: `
    @if (layout.isHandset()) {
      @if (list().items().length > 0) {
        <div class="m-load-more">
          @if (list().hasMore()) {
            <button mat-stroked-button (click)="list().loadMore()" [disabled]="list().loading()">
              {{ list().loading() ? 'Loading…' : 'Load more' }}
            </button>
          }
          <span class="m-count">Showing {{ list().items().length }} of {{ list().total() }}</span>
        </div>
      }
    } @else {
      <mat-paginator
        [length]="list().total()"
        [pageSize]="list().query().pageSize"
        [pageIndex]="list().query().page - 1"
        [pageSizeOptions]="pageSizes()"
        (page)="list().onPage($event)" />
    }
  `,
})
export class ListFooter {
  readonly list = input.required<ListState<any>>();
  readonly pageSizes = input<number[]>([20, 50, 100]);
  readonly layout = inject(LayoutService);
}
