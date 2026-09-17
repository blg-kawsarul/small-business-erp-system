import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ApiService, dateToIso } from '../../core/api.service';
import { Paged, ProductDropdownItem, StockLedgerEntry } from '../../core/models';
import { ListState } from '../../shared/list-state';
import { LabelPipe, QtyPipe } from '../../shared/pipes';
import { SearchSelect } from '../../shared/search-select';

@Component({
  selector: 'app-stock-ledger',
  imports: [RouterLink, DatePipe, ReactiveFormsModule, MatTableModule, MatPaginatorModule, MatButtonModule, MatIconModule, MatFormFieldModule, MatInputModule, MatDatepickerModule, MatProgressBarModule, SearchSelect, LabelPipe, QtyPipe],
  template: `
    <div class="page">
      <div class="page-header">
        <div>
          <h1>Stock ledger</h1>
          <div class="subtitle">Every stock movement with the balance after it</div>
        </div>
        <div class="actions"><a mat-stroked-button routerLink="/stock"><mat-icon>inventory</mat-icon>Stock balance</a></div>
      </div>
      <div class="card">
        <div class="toolbar">
          <div style="flex: 1 1 280px; max-width: 380px">
            <app-search-select label="Product" [control]="product" [fetch]="fetchProducts" [initialLabel]="productLabel" subscript="dynamic" (selected)="list.resetToFirstPage()" />
          </div>
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>Date range</mat-label>
            <mat-date-range-input [rangePicker]="picker">
              <input matStartDate [formControl]="from" placeholder="From" />
              <input matEndDate [formControl]="to" placeholder="To" (dateChange)="list.resetToFirstPage()" />
            </mat-date-range-input>
            <mat-datepicker-toggle matIconSuffix [for]="picker" />
            <mat-date-range-picker #picker />
          </mat-form-field>
          <button mat-button (click)="clear()">Clear</button>
        </div>
        @if (list.loading()) { <mat-progress-bar mode="indeterminate" /> }
        <div class="table-wrap">
          <table mat-table [dataSource]="list.items()">
            <ng-container matColumnDef="date"><th mat-header-cell *matHeaderCellDef>Date</th><td mat-cell *matCellDef="let l" class="nowrap">{{ l.createdDate | date: 'dd MMM yyyy, h:mm a' }}</td></ng-container>
            <ng-container matColumnDef="product"><th mat-header-cell *matHeaderCellDef>Product</th><td mat-cell *matCellDef="let l">{{ l.productName }} <span class="code">{{ l.productCode }}</span></td></ng-container>
            <ng-container matColumnDef="movement"><th mat-header-cell *matHeaderCellDef>Movement</th><td mat-cell *matCellDef="let l" class="nowrap">{{ l.movementType | label }}</td></ng-container>
            <ng-container matColumnDef="reference"><th mat-header-cell *matHeaderCellDef>Reference</th>
              <td mat-cell *matCellDef="let l" class="nowrap">
                @if (refLink(l); as link) { <a [routerLink]="link">#{{ l.referenceNumber }}</a> } @else { #{{ l.referenceNumber }} }
              </td></ng-container>
            <ng-container matColumnDef="change"><th mat-header-cell *matHeaderCellDef class="num">Change</th>
              <td mat-cell *matCellDef="let l" class="num" [class.positive]="l.quantityChange > 0" [class.negative]="l.quantityChange < 0">{{ l.quantityChange > 0 ? '+' : '' }}{{ l.quantityChange | qty }}</td></ng-container>
            <ng-container matColumnDef="balance"><th mat-header-cell *matHeaderCellDef class="num">Balance</th><td mat-cell *matCellDef="let l" class="num"><strong>{{ l.balanceAfter | qty }}</strong></td></ng-container>
            <ng-container matColumnDef="user"><th mat-header-cell *matHeaderCellDef>By</th><td mat-cell *matCellDef="let l" class="muted">{{ l.createdByUserName }}</td></ng-container>
            <tr mat-header-row *matHeaderRowDef="columns"></tr>
            <tr mat-row *matRowDef="let row; columns: columns"></tr>
          </table>
        </div>
        @if (!list.loading() && list.items().length === 0) { <div class="empty">{{ list.error() ?? 'No stock movements found.' }}</div> }
        <mat-paginator [length]="list.total()" [pageSize]="list.query().pageSize" [pageSizeOptions]="[20, 50, 100]" (page)="list.onPage($event)" />
      </div>
    </div>
  `,
})
export class StockLedgerPage implements OnInit {
  private readonly api = inject(ApiService);
  private readonly route = inject(ActivatedRoute);
  readonly product = new FormControl<string | null>(this.route.snapshot.queryParamMap.get('productUuid'));
  readonly productLabel = this.route.snapshot.queryParamMap.get('productLabel');
  readonly from = new FormControl<Date | null>(null);
  readonly to = new FormControl<Date | null>(null);
  readonly columns = ['date', 'product', 'movement', 'reference', 'change', 'balance', 'user'];
  readonly fetchProducts = (term: string) => this.api.get<ProductDropdownItem[]>('/products/dropdown', { search: term });
  readonly list = new ListState<StockLedgerEntry>((q) =>
    this.api.get<Paged<StockLedgerEntry>>('/stock/ledger', {
      ...q, productUuid: this.product.value, fromDate: dateToIso(this.from.value), toDate: dateToIso(this.to.value),
    }), 50);

  ngOnInit(): void {
    this.list.reload();
  }

  clear(): void {
    this.product.setValue(null);
    this.from.setValue(null);
    this.to.setValue(null);
    this.list.resetToFirstPage();
  }

  refLink(l: StockLedgerEntry): string | null {
    if (l.referenceType === 'SALES_ORDER') return `/sales-orders/${l.referenceUuid}`;
    if (l.referenceType === 'PURCHASE_ORDER') return `/purchase-orders/${l.referenceUuid}`;
    return null;
  }
}
