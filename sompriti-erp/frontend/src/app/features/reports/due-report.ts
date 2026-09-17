import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { LayoutService } from '../../core/layout.service';
import { OrderKind, OrderListItem, Paged } from '../../core/models';
import { ListFooter } from '../../shared/list-footer';
import { ListState } from '../../shared/list-state';
import { MoneyPipe } from '../../shared/pipes';
import { orderMeta } from '../orders/order-kind';

/** Finalized orders that still have a due amount, oldest first (SRS 11.1). */
@Component({
  selector: 'app-due-report',
  imports: [RouterLink, DatePipe, MatTableModule, MatProgressBarModule, MatButtonToggleModule, ListFooter, MoneyPipe],
  template: `
    <div class="page">
      <div class="page-header">
        <div>
          <h1>Due report</h1>
          <div class="subtitle">Finalized orders with an outstanding balance, oldest first</div>
        </div>
        @if (kinds().length > 1) {
          <mat-button-toggle-group [value]="kind()" (change)="switchKind($event.value)" hideSingleSelectionIndicator>
            @for (k of kinds(); track k) { <mat-button-toggle [value]="k">{{ k === 'sales' ? 'Customer dues' : 'Supplier dues' }}</mat-button-toggle> }
          </mat-button-toggle-group>
        }
      </div>
      <div class="card">
        @if (list.loading()) { <mat-progress-bar mode="indeterminate" /> }

        @if (layout.isHandset()) {
          <div class="m-list">
            @for (o of list.items(); track o.uuid) {
              <a class="m-card" [routerLink]="[meta().route, o.uuid]">
                <div class="m-card-head">
                  <div>
                    <div class="m-title">{{ o.partyName }}</div>
                    <div class="m-sub">#{{ o.orderNumber }} · {{ o.orderDate | date: 'dd MMM yyyy' }} · {{ age(o.orderDate) }} days</div>
                  </div>
                  <div class="m-right">
                    <span class="m-amount negative">{{ o.dueAmount | money }}</span>
                    <span class="m-sub">due</span>
                  </div>
                </div>
                <div class="m-meta two">
                  <div><span class="k">Order total</span><span class="v">{{ o.totalAmount | money: false }}</span></div>
                  <div><span class="k">Paid</span><span class="v">{{ o.totalPaidAmount | money: false }}</span></div>
                </div>
              </a>
            }
          </div>
        } @else {
        <div class="table-wrap">

          <table mat-table [dataSource]="list.items()">
            <ng-container matColumnDef="orderNumber"><th mat-header-cell *matHeaderCellDef>Order</th><td mat-cell *matCellDef="let o"><a [routerLink]="[meta().route, o.uuid]">#{{ o.orderNumber }}</a></td></ng-container>
            <ng-container matColumnDef="orderDate"><th mat-header-cell *matHeaderCellDef>Date</th><td mat-cell *matCellDef="let o" class="nowrap">{{ o.orderDate | date: 'dd MMM yyyy' }}</td></ng-container>
            <ng-container matColumnDef="age"><th mat-header-cell *matHeaderCellDef class="num">Days</th><td mat-cell *matCellDef="let o" class="num">{{ age(o.orderDate) }}</td></ng-container>
            <ng-container matColumnDef="party"><th mat-header-cell *matHeaderCellDef>{{ meta().partyLabel }}</th><td mat-cell *matCellDef="let o">{{ o.partyName }} <span class="code">{{ o.partyCode }}</span></td></ng-container>
            <ng-container matColumnDef="company"><th mat-header-cell *matHeaderCellDef>Company</th><td mat-cell *matCellDef="let o">{{ o.companyName }}</td></ng-container>
            <ng-container matColumnDef="total"><th mat-header-cell *matHeaderCellDef class="num">Total</th><td mat-cell *matCellDef="let o" class="num nowrap">{{ o.totalAmount | money }}</td></ng-container>
            <ng-container matColumnDef="paid"><th mat-header-cell *matHeaderCellDef class="num">Paid</th><td mat-cell *matCellDef="let o" class="num nowrap">{{ o.totalPaidAmount | money }}</td></ng-container>
            <ng-container matColumnDef="due"><th mat-header-cell *matHeaderCellDef class="num">Due</th><td mat-cell *matCellDef="let o" class="num nowrap negative"><strong>{{ o.dueAmount | money }}</strong></td></ng-container>
            <tr mat-header-row *matHeaderRowDef="columns"></tr>
            <tr mat-row class="clickable" *matRowDef="let row; columns: columns" (click)="open(row)"></tr>
          </table>
        </div>
        }

        @if (!list.loading() && list.items().length === 0) { <div class="empty">{{ list.error() ?? 'Nothing is due.' }}</div> }
        <app-list-footer [list]="list" />
      </div>
    </div>
  `,
})
export class DueReportPage implements OnInit {
  readonly auth = inject(AuthService);
  readonly layout = inject(LayoutService);
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);

  readonly kinds = computed<OrderKind[]>(() => {
    const u = this.auth.user();
    if (!u) return [];
    if (u.role === 'ADMIN') return ['sales', 'purchase'];
    if (u.role === 'MANAGER') return ['sales'];
    return [...(u.customerUuid ? (['sales'] as OrderKind[]) : []), ...(u.supplierUuid ? (['purchase'] as OrderKind[]) : [])];
  });
  readonly kind = signal<OrderKind>(this.kinds()[0] ?? 'sales');
  readonly meta = computed(() => orderMeta(this.kind()));
  readonly columns = ['orderNumber', 'orderDate', 'age', 'party', 'company', 'total', 'paid', 'due'];
  readonly list = new ListState<OrderListItem>((q) =>
    this.api.get<Paged<OrderListItem>>(this.meta().api, { page: q.page, pageSize: q.pageSize, dueOnly: true, postingStatus: 'FINAL', sort: 'orderDate' }), 50);

  ngOnInit(): void {
    if (this.kinds().length > 0) this.list.reload();
  }

  switchKind(k: OrderKind): void {
    this.kind.set(k);
    this.list.resetToFirstPage();
  }

  age(orderDate: string): number {
    return Math.max(0, Math.floor((Date.now() - new Date(orderDate + 'T00:00:00+06:00').getTime()) / 86_400_000));
  }

  open(o: OrderListItem): void {
    void this.router.navigate([this.meta().route, o.uuid]);
  }
}
