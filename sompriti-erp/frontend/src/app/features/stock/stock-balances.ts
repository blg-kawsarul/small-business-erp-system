import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSortModule } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { LayoutService } from '../../core/layout.service';
import { Paged, StockBalance } from '../../core/models';
import { ListFooter } from '../../shared/list-footer';
import { ListState } from '../../shared/list-state';
import { QtyPipe } from '../../shared/pipes';

@Component({
  selector: 'app-stock-balances',
  imports: [RouterLink, DatePipe, MatTableModule, MatSortModule, MatButtonModule, MatIconModule, MatFormFieldModule, MatInputModule, MatProgressBarModule, MatCheckboxModule, MatTooltipModule, ListFooter, QtyPipe],
  template: `
    <div class="page">
      <div class="page-header">
        <div>
          <h1>Stock balance</h1>
          @if (!layout.isHandset()) {
            <div class="subtitle">Current stock per product in pieces. Stock changes only when orders are finalized or voided, or by adjustments.</div>
          }
        </div>
        <div class="actions">
          @if (!layout.isHandset()) {
            <a mat-stroked-button routerLink="/stock/ledger"><mat-icon>receipt_long</mat-icon>Ledger</a>
            @if (auth.isAdmin()) { <a mat-flat-button routerLink="/stock/adjustments"><mat-icon>tune</mat-icon>Adjust stock</a> }
          } @else {
            <a mat-icon-button routerLink="/stock/ledger" aria-label="Stock ledger"><mat-icon>receipt_long</mat-icon></a>
          }
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

        @if (layout.isHandset()) {
          <div class="m-list">
            @for (s of list.items(); track s.productUuid) {
              <a class="m-card" routerLink="/stock/ledger" [queryParams]="{ productUuid: s.productUuid, productLabel: s.productName + ' (' + s.productCode + ')' }">
                <div class="m-card-head">
                  <div>
                    <div class="m-title">{{ s.productName }}</div>
                    <div class="m-sub">{{ s.productCode }}@if (s.pcsPerBox) { · {{ boxes(s) }} boxes }</div>
                  </div>
                  <div class="m-right">
                    <span class="m-amount" [class.negative]="s.isLowStock">{{ s.currentStockBalance | qty }} <span class="unit">pcs</span></span>
                    @if (s.isLowStock) { <span class="low">Low stock</span> }
                  </div>
                </div>
              </a>
            }
          </div>
        } @else {
          <div class="table-wrap">
            <table mat-table [dataSource]="list.items()" matSort (matSortChange)="list.onSort($event)">
              <ng-container matColumnDef="productCode"><th mat-header-cell *matHeaderCellDef mat-sort-header>Code</th><td mat-cell *matCellDef="let s" class="code">{{ s.productCode }}</td></ng-container>
              <ng-container matColumnDef="productName"><th mat-header-cell *matHeaderCellDef mat-sort-header>Product</th><td mat-cell *matCellDef="let s">{{ s.productName }}</td></ng-container>
              <ng-container matColumnDef="currentStockBalance"><th mat-header-cell *matHeaderCellDef mat-sort-header class="num">Stock (pcs)</th>
                <td mat-cell *matCellDef="let s" class="num" [class.negative]="s.isLowStock"><strong>{{ s.currentStockBalance | qty }}</strong></td></ng-container>
              <ng-container matColumnDef="boxes"><th mat-header-cell *matHeaderCellDef class="num">Boxes</th>
                <td mat-cell *matCellDef="let s" class="num muted">@if (s.pcsPerBox) { {{ boxes(s) }} }</td></ng-container>
              <ng-container matColumnDef="threshold"><th mat-header-cell *matHeaderCellDef class="num">Alert at</th><td mat-cell *matCellDef="let s" class="num muted">{{ s.lowStockThreshold ?? '' }}</td></ng-container>
              <ng-container matColumnDef="updated"><th mat-header-cell *matHeaderCellDef>Last change</th><td mat-cell *matCellDef="let s" class="muted nowrap">{{ s.updatedDate | date: 'dd MMM yyyy, h:mm a' }}</td></ng-container>
              <ng-container matColumnDef="actions"><th mat-header-cell *matHeaderCellDef></th>
                <td mat-cell *matCellDef="let s" class="num"><a mat-icon-button matTooltip="Movements" routerLink="/stock/ledger" [queryParams]="{ productUuid: s.productUuid, productLabel: s.productName + ' (' + s.productCode + ')' }"><mat-icon>history</mat-icon></a></td></ng-container>
              <tr mat-header-row *matHeaderRowDef="columns"></tr>
              <tr mat-row *matRowDef="let row; columns: columns"></tr>
            </table>
          </div>
        }

        @if (!list.loading() && list.items().length === 0) { <div class="empty">{{ list.error() ?? 'No products found.' }}</div> }
        <app-list-footer [list]="list" />
      </div>

      @if (layout.isHandset() && auth.isAdmin()) {
        <a mat-fab class="fab" routerLink="/stock/adjustments" aria-label="Adjust stock"><mat-icon>tune</mat-icon></a>
      }
    </div>
  `,
  styles: `
    .unit { font-size: 11px; font-weight: 500; color: var(--erp-muted); }
    .low { font-size: 11px; font-weight: 600; color: var(--erp-chip-danger-fg); background: var(--erp-chip-danger-bg); border-radius: 999px; padding: 2px 8px; }
  `,
})
export class StockBalancesPage implements OnInit {
  readonly auth = inject(AuthService);
  readonly layout = inject(LayoutService);
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
