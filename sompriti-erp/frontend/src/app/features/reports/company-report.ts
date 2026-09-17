import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { ApiService, dateToIso, errorMessage } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { LayoutService } from '../../core/layout.service';
import { CompanyReportRow } from '../../core/models';
import { MoneyPipe } from '../../shared/pipes';

@Component({
  selector: 'app-company-report',
  imports: [ReactiveFormsModule, MatTableModule, MatFormFieldModule, MatDatepickerModule, MatButtonModule, MatProgressBarModule, MoneyPipe],
  styles: `
    .block { margin-top: 10px; }
    .block-title { font-size: 11px; font-weight: 700; letter-spacing: .05em; text-transform: uppercase; color: var(--erp-faint); }
    .block .m-meta { margin-top: 4px; }
  `,
  template: `
    <div class="page">
      <div class="page-header">
        <div>
          <h1>Company report</h1>
          <div class="subtitle">Sales{{ auth.isAdmin() ? ' and purchases' : '' }} per company: totals, payments and due (finalized orders).</div>
        </div>
      </div>
      <div class="card">
        <div class="toolbar">
          <mat-form-field subscriptSizing="dynamic">
            <mat-label>Order date</mat-label>
            <mat-date-range-input [rangePicker]="picker">
              <input matStartDate [formControl]="from" placeholder="From" />
              <input matEndDate [formControl]="to" placeholder="To" (dateChange)="load()" />
            </mat-date-range-input>
            <mat-datepicker-toggle matIconSuffix [for]="picker" />
            <mat-date-range-picker #picker />
          </mat-form-field>
          <button mat-button (click)="from.setValue(null); to.setValue(null); load()">Clear</button>
        </div>
        @if (loading()) { <mat-progress-bar mode="indeterminate" /> }

        @if (layout.isHandset()) {
          <div class="m-list">
            @for (r of rows(); track r.companyUuid) {
              <div class="m-card">
                <div class="m-card-head">
                  <div>
                    <div class="m-title">{{ r.companyName }}</div>
                    <div class="m-sub">{{ r.companyCode }}</div>
                  </div>
                </div>
                <div class="block">
                  <div class="block-title">Sales</div>
                  <div class="m-meta">
                    <div><span class="k">Orders</span><span class="v">{{ r.salesCount }}</span></div>
                    <div><span class="k">Total</span><span class="v">{{ r.salesTotal | money: false }}</span></div>
                    <div><span class="k">Due</span><span class="v negative">{{ r.salesDue | money: false }}</span></div>
                  </div>
                </div>
                @if (r.purchaseCount !== null) {
                  <div class="block">
                    <div class="block-title">Purchases</div>
                    <div class="m-meta">
                      <div><span class="k">Orders</span><span class="v">{{ r.purchaseCount }}</span></div>
                      <div><span class="k">Total</span><span class="v">{{ r.purchaseTotal | money: false }}</span></div>
                      <div><span class="k">Due</span><span class="v negative">{{ r.purchaseDue | money: false }}</span></div>
                    </div>
                  </div>
                }
              </div>
            }
            @if (!loading() && rows().length === 0) { <div class="empty">No data for the selected dates.</div> }
          </div>
        } @else {
        <div class="table-wrap">

          <table mat-table [dataSource]="rows()">
            <ng-container matColumnDef="company"><th mat-header-cell *matHeaderCellDef>Company</th><td mat-cell *matCellDef="let r">{{ r.companyName }} <span class="code">{{ r.companyCode }}</span></td><td mat-footer-cell *matFooterCellDef><strong>Total</strong></td></ng-container>
            <ng-container matColumnDef="salesCount"><th mat-header-cell *matHeaderCellDef class="num">Sales orders</th><td mat-cell *matCellDef="let r" class="num">{{ r.salesCount }}</td><td mat-footer-cell *matFooterCellDef class="num">{{ sum('salesCount') }}</td></ng-container>
            <ng-container matColumnDef="salesTotal"><th mat-header-cell *matHeaderCellDef class="num">Sales total</th><td mat-cell *matCellDef="let r" class="num nowrap">{{ r.salesTotal | money }}</td><td mat-footer-cell *matFooterCellDef class="num nowrap">{{ sum('salesTotal') | money }}</td></ng-container>
            <ng-container matColumnDef="salesPaid"><th mat-header-cell *matHeaderCellDef class="num">Received</th><td mat-cell *matCellDef="let r" class="num nowrap">{{ r.salesPaid | money }}</td><td mat-footer-cell *matFooterCellDef class="num nowrap">{{ sum('salesPaid') | money }}</td></ng-container>
            <ng-container matColumnDef="salesDue"><th mat-header-cell *matHeaderCellDef class="num">Customer due</th><td mat-cell *matCellDef="let r" class="num nowrap negative">{{ r.salesDue | money }}</td><td mat-footer-cell *matFooterCellDef class="num nowrap negative"><strong>{{ sum('salesDue') | money }}</strong></td></ng-container>
            <ng-container matColumnDef="purchaseCount"><th mat-header-cell *matHeaderCellDef class="num">Purchase orders</th><td mat-cell *matCellDef="let r" class="num">{{ r.purchaseCount }}</td><td mat-footer-cell *matFooterCellDef class="num">{{ sum('purchaseCount') }}</td></ng-container>
            <ng-container matColumnDef="purchaseTotal"><th mat-header-cell *matHeaderCellDef class="num">Purchase total</th><td mat-cell *matCellDef="let r" class="num nowrap">{{ r.purchaseTotal | money }}</td><td mat-footer-cell *matFooterCellDef class="num nowrap">{{ sum('purchaseTotal') | money }}</td></ng-container>
            <ng-container matColumnDef="purchasePaid"><th mat-header-cell *matHeaderCellDef class="num">Paid</th><td mat-cell *matCellDef="let r" class="num nowrap">{{ r.purchasePaid | money }}</td><td mat-footer-cell *matFooterCellDef class="num nowrap">{{ sum('purchasePaid') | money }}</td></ng-container>
            <ng-container matColumnDef="purchaseDue"><th mat-header-cell *matHeaderCellDef class="num">Supplier due</th><td mat-cell *matCellDef="let r" class="num nowrap negative">{{ r.purchaseDue | money }}</td><td mat-footer-cell *matFooterCellDef class="num nowrap negative"><strong>{{ sum('purchaseDue') | money }}</strong></td></ng-container>
            <tr mat-header-row *matHeaderRowDef="columns()"></tr>
            <tr mat-row *matRowDef="let row; columns: columns()"></tr>
            <tr mat-footer-row *matFooterRowDef="columns()"></tr>
          </table>
        </div>
        }

        @if (error()) { <div class="empty negative">{{ error() }}</div> }
      </div>
    </div>
  `,
})
export class CompanyReportPage implements OnInit {
  readonly auth = inject(AuthService);
  readonly layout = inject(LayoutService);
  private readonly api = inject(ApiService);
  readonly from = new FormControl<Date | null>(null);
  readonly to = new FormControl<Date | null>(null);
  readonly rows = signal<CompanyReportRow[]>([]);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly columns = computed(() =>
    this.auth.isAdmin()
      ? ['company', 'salesCount', 'salesTotal', 'salesPaid', 'salesDue', 'purchaseCount', 'purchaseTotal', 'purchasePaid', 'purchaseDue']
      : ['company', 'salesCount', 'salesTotal', 'salesPaid', 'salesDue'],
  );

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.get<CompanyReportRow[]>('/reports/companies', { fromDate: dateToIso(this.from.value), toDate: dateToIso(this.to.value) }).subscribe({
      next: (r) => { this.rows.set(r); this.error.set(null); this.loading.set(false); },
      error: (e) => { this.error.set(errorMessage(e)); this.loading.set(false); },
    });
  }

  sum(key: keyof CompanyReportRow): number {
    return this.rows().reduce((s, r) => s + (Number(r[key]) || 0), 0);
  }
}
