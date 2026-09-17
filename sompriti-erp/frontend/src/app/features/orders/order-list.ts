import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSortModule } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { of } from 'rxjs';
import { ApiService, dateToIso } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { DropdownItem, OrderKind, OrderListItem, Paged, PostingStatus } from '../../core/models';
import { ListState } from '../../shared/list-state';
import { LabelPipe, MoneyPipe } from '../../shared/pipes';
import { SearchSelect } from '../../shared/search-select';
import { StatusChip } from '../../shared/status-chip';
import { orderMeta } from './order-kind';

@Component({
  selector: 'app-order-list',
  imports: [
    RouterLink, DatePipe, ReactiveFormsModule, MatTableModule, MatPaginatorModule, MatSortModule, MatButtonModule, MatButtonToggleModule,
    MatIconModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatDatepickerModule, MatProgressBarModule,
    SearchSelect, StatusChip, MoneyPipe, LabelPipe,
  ],
  template: `
    <div class="page">
      <div class="page-header">
        <div>
          <h1>{{ auth.isUser() ? (kind() === 'sales' ? 'My purchases' : 'My supplies') : meta().title }}</h1>
          <div class="subtitle">
            @if (auth.isUser()) { Orders for {{ kind() === 'sales' ? auth.user()?.customerName : auth.user()?.supplierName }} }
            @else { Draft orders do not affect stock. Finalize to post stock; payments are added to final orders. }
          </div>
        </div>
        @if (canCreate()) {
          <div class="actions"><a mat-flat-button [routerLink]="meta().route + '/new'"><mat-icon>add</mat-icon>New {{ meta().singular.toLowerCase() }}</a></div>
        }
      </div>

      <div class="card">
        <div class="toolbar">
          <mat-button-toggle-group [value]="status()" (change)="setStatus($event.value)" hideSingleSelectionIndicator>
            <mat-button-toggle value="">All</mat-button-toggle>
            <mat-button-toggle value="DRAFT">Draft</mat-button-toggle>
            <mat-button-toggle value="FINAL">Final</mat-button-toggle>
            <mat-button-toggle value="VOID">Void</mat-button-toggle>
          </mat-button-toggle-group>
          <mat-form-field class="search" subscriptSizing="dynamic">
            <mat-icon matPrefix>search</mat-icon>
            <mat-label>Order no. or {{ meta().partyLabel.toLowerCase() }}</mat-label>
            <input matInput (input)="list.search($any($event.target).value)" />
          </mat-form-field>
          @if (!auth.isUser()) {
            <div style="flex: 1 1 240px; max-width: 320px">
              <app-search-select [label]="meta().partyLabel" [control]="party" [fetch]="fetchParties" [initialLabel]="partyLabel" subscript="dynamic" (selected)="list.resetToFirstPage()" />
            </div>
            <mat-form-field subscriptSizing="dynamic" style="width: 170px">
              <mat-label>Company</mat-label>
              <mat-select [formControl]="company" (selectionChange)="list.resetToFirstPage()">
                <mat-option [value]="null">All companies</mat-option>
                @for (c of companies(); track c.uuid) { <mat-option [value]="c.uuid">{{ c.name }}</mat-option> }
              </mat-select>
            </mat-form-field>
          }
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>Order date</mat-label>
            <mat-date-range-input [rangePicker]="picker">
              <input matStartDate [formControl]="from" placeholder="From" />
              <input matEndDate [formControl]="to" placeholder="To" (dateChange)="list.resetToFirstPage()" />
            </mat-date-range-input>
            <mat-datepicker-toggle matIconSuffix [for]="picker" />
            <mat-date-range-picker #picker />
          </mat-form-field>
          <button mat-button (click)="clearFilters()">Clear</button>
        </div>

        @if (list.loading()) { <mat-progress-bar mode="indeterminate" /> }
        <div class="table-wrap">
          <table mat-table [dataSource]="list.items()" matSort matSortActive="orderDate" matSortDirection="desc" (matSortChange)="list.onSort($event)">
            <ng-container matColumnDef="orderNumber"><th mat-header-cell *matHeaderCellDef mat-sort-header>Order no.</th><td mat-cell *matCellDef="let o"><a [routerLink]="[meta().route, o.uuid]">#{{ o.orderNumber }}</a></td></ng-container>
            <ng-container matColumnDef="orderDate"><th mat-header-cell *matHeaderCellDef mat-sort-header>Date</th><td mat-cell *matCellDef="let o" class="nowrap">{{ o.orderDate | date: 'dd MMM yyyy' }}</td></ng-container>
            <ng-container matColumnDef="partyName"><th mat-header-cell *matHeaderCellDef mat-sort-header>{{ meta().partyLabel }}</th><td mat-cell *matCellDef="let o">{{ o.partyName }} <span class="code">{{ o.partyCode }}</span></td></ng-container>
            <ng-container matColumnDef="company"><th mat-header-cell *matHeaderCellDef>Company</th><td mat-cell *matCellDef="let o">{{ o.companyName }}</td></ng-container>
            <ng-container matColumnDef="paymentType"><th mat-header-cell *matHeaderCellDef>Payment</th><td mat-cell *matCellDef="let o">{{ o.paymentType | label }}</td></ng-container>
            <ng-container matColumnDef="status"><th mat-header-cell *matHeaderCellDef>Status</th><td mat-cell *matCellDef="let o"><app-status [value]="o.postingStatus" /></td></ng-container>
            <ng-container matColumnDef="totalAmount"><th mat-header-cell *matHeaderCellDef mat-sort-header class="num">Total</th><td mat-cell *matCellDef="let o" class="num nowrap">{{ o.totalAmount | money }}</td></ng-container>
            <ng-container matColumnDef="paid"><th mat-header-cell *matHeaderCellDef class="num">Paid</th><td mat-cell *matCellDef="let o" class="num nowrap">{{ o.totalPaidAmount | money }}</td></ng-container>
            <ng-container matColumnDef="due"><th mat-header-cell *matHeaderCellDef class="num">Due</th>
              <td mat-cell *matCellDef="let o" class="num nowrap" [class.negative]="o.postingStatus === 'FINAL' && o.dueAmount > 0">{{ o.postingStatus === 'FINAL' ? (o.dueAmount | money) : '-' }}</td></ng-container>
            <tr mat-header-row *matHeaderRowDef="columns()"></tr>
            <tr mat-row class="clickable" *matRowDef="let row; columns: columns()" (click)="open(row)"></tr>
          </table>
        </div>
        @if (!list.loading() && list.items().length === 0) { <div class="empty">{{ list.error() ?? 'No orders found.' }}</div> }
        <mat-paginator [length]="list.total()" [pageSize]="list.query().pageSize" [pageSizeOptions]="[20, 50, 100]" (page)="list.onPage($event)" />
      </div>
    </div>
  `,
})
export class OrderListPage implements OnInit {
  readonly kind = input<OrderKind>('sales');
  readonly auth = inject(AuthService);
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly meta = computed(() => orderMeta(this.kind()));
  readonly canCreate = computed(() => (this.kind() === 'purchase' ? this.auth.isAdmin() : this.auth.canSales()));
  readonly columns = computed(() =>
    this.auth.isUser()
      ? ['orderNumber', 'orderDate', 'company', 'paymentType', 'status', 'totalAmount', 'paid', 'due']
      : ['orderNumber', 'orderDate', 'partyName', 'company', 'paymentType', 'status', 'totalAmount', 'paid', 'due'],
  );

  readonly status = signal<PostingStatus | ''>((this.route.snapshot.queryParamMap.get('status') as PostingStatus) ?? '');
  readonly party = new FormControl<string | null>(this.route.snapshot.queryParamMap.get('partyUuid'));
  readonly partyLabel = this.route.snapshot.queryParamMap.get('partyLabel');
  readonly company = new FormControl<string | null>(null);
  readonly from = new FormControl<Date | null>(null);
  readonly to = new FormControl<Date | null>(null);
  readonly companies = signal<DropdownItem[]>([]);

  readonly fetchParties = (term: string) =>
    this.auth.isUser() ? of([]) : this.api.get<DropdownItem[]>(`${this.meta().partyApi}/dropdown`, { search: term });

  readonly list = new ListState<OrderListItem>((q) =>
    this.api.get<Paged<OrderListItem>>(this.meta().api, {
      ...q,
      postingStatus: this.status() || null,
      partyUuid: this.party.value,
      companyUuid: this.company.value,
      fromDate: dateToIso(this.from.value),
      toDate: dateToIso(this.to.value),
    }),
  );

  ngOnInit(): void {
    this.list.reload();
    if (!this.auth.isUser()) this.api.get<DropdownItem[]>('/companies/dropdown').subscribe({ next: (c) => this.companies.set(c), error: () => {} });
  }

  setStatus(value: PostingStatus | ''): void {
    this.status.set(value);
    this.list.resetToFirstPage();
  }

  clearFilters(): void {
    this.party.setValue(null);
    this.company.setValue(null);
    this.from.setValue(null);
    this.to.setValue(null);
    this.status.set('');
    this.list.resetToFirstPage();
  }

  open(o: OrderListItem): void {
    void this.router.navigate([this.meta().route, o.uuid]);
  }
}
