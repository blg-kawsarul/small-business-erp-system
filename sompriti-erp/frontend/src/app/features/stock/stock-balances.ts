import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSortModule } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { Paged, StockBalance } from '../../core/models';
import { ListState } from '../../shared/list-state';
import { QtyPipe } from '../../shared/pipes';

@Component({
  selector: 'app-stock-balances',
  imports: [RouterLink, DatePipe, MatTableModule, MatPaginatorModule, MatSortModule, MatButtonModule, MatIconModule, MatFormFieldModule, MatInputModule, MatProgressBarModule, MatCheckboxModule, MatTooltipModule, QtyPipe],
  template: `
    <div class="page">
      <div class="page-header">
        <div>
          <h1>Stock balance</h1>
          <div class="subtitle">Current stock per product in pieces. Stock changes only when orders are finalized or voided, or by adjustments.</div>
        </div>
        <div class="actions">
          <a mat-stroked-button routerLink="/stock/ledger"><mat-icon>receipt_long</mat-icon>Ledger</a>
          @if (auth.isAdmin()) { <a mat-flat-button routerLink="/stock/adjustments"><mat-icon>tune</mat-icon>Adjust stock</a> }
        </div>
      </div>
      <div class="card">
        <div class="toolbar">
          <mat-form-field class="search" subscriptSizing="dynamic">
            <mat-icon matPrefix>search</mat-icon>
            <mat-label>Search product</mat-label>
            <input matInput (input)="list.search($any($event.target).value)" />
          </mat-form-field>
          <mat-checkbox (change)="lowOnly.set($event.checked); list.resetToFirstPage()">Low stock only</mat-checkbox>
        </div>
        @if (list.loading()) { <mat-progress-bar mode="indeterminate" /> }
        <div class="table-wrap">
          <table mat-table [dataSource]="list.items()" matSort (matSortChange)="list.onSort($event)">
            <ng-container matColumnDef="productCode"><th mat-header-cell *matHeaderCellDef mat-sort-header>Code</th><td mat-cell *matCellDef="let s" class="code">{{ s.productCode }}</td></ng-container>
            <ng-container matColumnDef="productName"><th mat-header-cell *matHeaderCellDef mat-sort-header>Product</th><td mat-cell *matCellDef="let s">{{ s.productName }}</td></ng-container>
            <ng-container matColumnDef="currentStockBalance"><th mat-header-cell *matHeaderCellDef mat-sort-header class="num">Stock (pcs)</th>
              <td mat-cell *matCellDef="let s" class="num" [class.negative]="s.isLowStock"><strong>{{ s.currentStockBalance | qty }}</strong></td></ng-container>
            <ng-container matColumnDef="boxes"><th mat-header-cell *matHeaderCellDef class="num">≈ Boxes</th>
              <td mat-cell *matCellDef="let s" class="num muted">@if (s.pcsPerBox) { {{ boxes(s) }} }</td></ng-container>
            <ng-container matColumnDef="threshold"><th mat-header-cell *matHeaderCellDef class="num">Alert at</th><td mat-cell *matCellDef="let s" class="num muted">{{ s.lowStockThreshold ?? '' }}</td></ng-container>
            <ng-container matColumnDef="updated"><th mat-header-cell *matHeaderCellDef>Last change</th><td mat-cell *matCellDef="let s" class="muted nowrap">{{ s.updatedDate | date: 'dd MMM yyyy, h:mm a' }}</td></ng-container>
            <ng-container matColumnDef="actions"><th mat-header-cell *matHeaderCellDef></th>
              <td mat-cell *matCellDef="let s" class="num"><a mat-icon-button matTooltip="Movements" routerLink="/stock/ledger" [queryParams]="{ productUuid: s.productUuid, productLabel: s.productName + ' (' + s.productCode + ')' }"><mat-icon>history</mat-icon></a></td></ng-container>
            <tr mat-header-row *matHeaderRowDef="columns"></tr>
            <tr mat-row *matRowDef="let row; columns: columns"></tr>
          </table>
        </div>
        @if (!list.loading() && list.items().length === 0) { <div class="empty">{{ list.error() ?? 'No products found.' }}</div> }
        <mat-paginator [length]="list.total()" [pageSize]="list.query().pageSize" [pageSizeOptions]="[20, 50, 100]" (page)="list.onPage($event)" />
      </div>
    </div>
  `,
})
export class StockBalancesPage implements OnInit {
  readonly auth = inject(AuthService);
  private readonly api = inject(ApiService);
  readonly lowOnly = signal(false);
  readonly columns = ['productCode', 'productName', 'currentStockBalance', 'boxes', 'threshold', 'updated', 'actions'];
  readonly list = new ListState<StockBalance>((q) => this.api.get<Paged<StockBalance>>('/stock/balances', { ...q, lowStockOnly: this.lowOnly() }));

  ngOnInit(): void {
    this.list.reload();
  }

  boxes(s: StockBalance): string {
    const full = Math.floor(s.currentStockBalance / (s.pcsPerBox ?? 1));
    const loose = s.currentStockBalance % (s.pcsPerBox ?? 1);
    return loose ? `${full} + ${loose} pcs` : `${full}`;
  }
}
