import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { ApiService, dateToIso } from '../../core/api.service';
import { LayoutService } from '../../core/layout.service';
import { AdjustmentReason, AdjustmentType, Paged, ProductDropdownItem, StockAdjustment } from '../../core/models';
import { NotifyService } from '../../core/notify.service';
import { applyServerErrors, controlError } from '../../shared/form-errors';
import { ListFooter } from '../../shared/list-footer';
import { ListState } from '../../shared/list-state';
import { LabelPipe, QtyPipe } from '../../shared/pipes';
import { SearchSelect } from '../../shared/search-select';
import { StatusChip } from '../../shared/status-chip';

@Component({
  selector: 'app-stock-adjustments',
  imports: [DatePipe, MatTableModule, MatButtonModule, MatIconModule, MatFormFieldModule, MatInputModule, MatProgressBarModule, ListFooter, LabelPipe, QtyPipe, StatusChip],
  template: `
    <div class="page">
      <div class="page-header">
        <div>
          <h1>Stock adjustments</h1>
          @if (!layout.isHandset()) {
            <div class="subtitle">Opening stock, damage, loss and corrections. Adjustments post immediately and cannot be edited.</div>
          }
        </div>
        @if (!layout.isHandset()) {
          <div class="actions"><button mat-flat-button (click)="create()"><mat-icon>add</mat-icon>New adjustment</button></div>
        }
      </div>

      <div class="card">
        <div class="toolbar">
          <mat-form-field class="search" subscriptSizing="dynamic">
            <mat-icon matPrefix>search</mat-icon>
            <mat-label>Search product or number</mat-label>
            <input matInput (input)="list.search($any($event.target).value)" />
          </mat-form-field>
        </div>
        @if (list.loading()) { <mat-progress-bar mode="indeterminate" /> }

        @if (layout.isHandset()) {
          <div class="m-list">
            @for (a of list.items(); track a.uuid) {
              <div class="m-card">
                <div class="m-card-head">
                  <div>
                    <div class="m-title">{{ a.productName }}</div>
                    <div class="m-sub">#{{ a.adjustmentNumber }} · {{ a.adjustmentDate | date: 'dd MMM yyyy' }}</div>
                  </div>
                  <div class="m-right">
                    <span class="m-amount" [class.positive]="a.adjustmentType === 'INCREASE'" [class.negative]="a.adjustmentType === 'DECREASE'">
                      {{ a.adjustmentType === 'INCREASE' ? '+' : '-' }}{{ a.quantityPcs | qty }}
                    </span>
                    <app-status [value]="a.adjustmentType" />
                  </div>
                </div>
                <div class="m-meta two">
                  <div><span class="k">Reason</span><span class="v">{{ a.reason | label }}</span></div>
                  <div><span class="k">By</span><span class="v">{{ a.createdByUserName }}</span></div>
                </div>
                @if (a.note) { <div class="m-sub note">{{ a.note }}</div> }
              </div>
            }
          </div>
        } @else {
          <div class="table-wrap">
            <table mat-table [dataSource]="list.items()">
              <ng-container matColumnDef="number"><th mat-header-cell *matHeaderCellDef>No.</th><td mat-cell *matCellDef="let a" class="code">{{ a.adjustmentNumber }}</td></ng-container>
              <ng-container matColumnDef="date"><th mat-header-cell *matHeaderCellDef>Date</th><td mat-cell *matCellDef="let a" class="nowrap">{{ a.adjustmentDate | date: 'dd MMM yyyy' }}</td></ng-container>
              <ng-container matColumnDef="product"><th mat-header-cell *matHeaderCellDef>Product</th><td mat-cell *matCellDef="let a">{{ a.productName }} <span class="code">{{ a.productCode }}</span></td></ng-container>
              <ng-container matColumnDef="type"><th mat-header-cell *matHeaderCellDef>Type</th><td mat-cell *matCellDef="let a"><app-status [value]="a.adjustmentType" /></td></ng-container>
              <ng-container matColumnDef="qty"><th mat-header-cell *matHeaderCellDef class="num">Qty (pcs)</th><td mat-cell *matCellDef="let a" class="num">{{ a.quantityPcs | qty }}</td></ng-container>
              <ng-container matColumnDef="reason"><th mat-header-cell *matHeaderCellDef>Reason</th><td mat-cell *matCellDef="let a">{{ a.reason | label }}@if (a.note) { <div class="muted">{{ a.note }}</div> }</td></ng-container>
              <ng-container matColumnDef="user"><th mat-header-cell *matHeaderCellDef>By</th><td mat-cell *matCellDef="let a" class="muted">{{ a.createdByUserName }}</td></ng-container>
              <tr mat-header-row *matHeaderRowDef="columns"></tr>
              <tr mat-row *matRowDef="let row; columns: columns"></tr>
            </table>
          </div>
        }

        @if (!list.loading() && list.items().length === 0) { <div class="empty">{{ list.error() ?? 'No adjustments yet. Use an Opening stock adjustment to enter existing stock.' }}</div> }
        <app-list-footer [list]="list" />
      </div>

      @if (layout.isHandset()) {
        <button mat-fab class="fab" aria-label="New adjustment" (click)="create()"><mat-icon>add</mat-icon></button>
      }
    </div>
  `,
  styles: `.note { margin-top: 8px; }`,
})
export class StockAdjustmentsPage implements OnInit {
  readonly layout = inject(LayoutService);
  private readonly api = inject(ApiService);
  private readonly dialog = inject(MatDialog);
  readonly columns = ['number', 'date', 'product', 'type', 'qty', 'reason', 'user'];
  readonly list = new ListState<StockAdjustment>((q) => this.api.get<Paged<StockAdjustment>>('/stock/adjustments', { ...q }));

  ngOnInit(): void {
    this.list.reload();
  }

  create(): void {
    this.dialog.open(AdjustmentDialog, this.layout.dialog(null, '560px')).afterClosed().subscribe((ok) => ok && this.list.resetToFirstPage());
  }
}

@Component({
  selector: 'app-adjustment-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule, MatSelectModule, MatButtonToggleModule, MatDatepickerModule, SearchSelect],
  template: `
    <h2 mat-dialog-title>New stock adjustment</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content>
        <app-search-select label="Product" [control]="form.controls.productUuid" [fetch]="fetchProducts" [hint]="stockHint" (selected)="selected.set($event)" />
        @if (selected(); as p) { <p class="muted" style="margin-top: -8px">Current stock: <strong>{{ p.currentStock }}</strong> pcs</p> }
        <mat-button-toggle-group formControlName="adjustmentType" style="margin-bottom: 16px">
          <mat-button-toggle value="INCREASE">Increase</mat-button-toggle>
          <mat-button-toggle value="DECREASE">Decrease</mat-button-toggle>
        </mat-button-toggle-group>
        <div class="form-grid">
          <mat-form-field><mat-label>Quantity (pcs)</mat-label><input matInput type="number" min="1" step="1" formControlName="quantityPcs" /><mat-error>{{ err('quantityPcs', 'Quantity') }}</mat-error></mat-form-field>
          <mat-form-field>
            <mat-label>Reason</mat-label>
            <mat-select formControlName="reason">
              @for (r of reasons; track r.value) { <mat-option [value]="r.value">{{ r.label }}</mat-option> }
            </mat-select>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Date</mat-label>
            <input matInput [matDatepicker]="dp" formControlName="adjustmentDate" [max]="today" />
            <mat-datepicker-toggle matIconSuffix [for]="dp" /><mat-datepicker #dp />
          </mat-form-field>
          <mat-form-field class="span-2"><mat-label>Note</mat-label><textarea matInput rows="2" formControlName="note"></textarea><mat-error>{{ err('note', 'Note') }}</mat-error></mat-form-field>
        </div>
        @if (error()) { <p class="negative">{{ error() }}</p> }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="busy()">Post adjustment</button>
      </mat-dialog-actions>
    </form>
  `,
})
export class AdjustmentDialog {
  private readonly api = inject(ApiService);
  private readonly ref = inject(MatDialogRef<AdjustmentDialog>);
  private readonly notify = inject(NotifyService);
  readonly today = new Date();
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly selected = signal<ProductDropdownItem | null>(null);
  readonly reasons: { value: AdjustmentReason; label: string }[] = [
    { value: 'OPENING_STOCK', label: 'Opening stock' },
    { value: 'DAMAGE', label: 'Damage' },
    { value: 'LOSS', label: 'Loss' },
    { value: 'CORRECTION', label: 'Correction' },
    { value: 'OTHER', label: 'Other' },
  ];
  readonly fetchProducts = (term: string) => this.api.get<ProductDropdownItem[]>('/products/dropdown', { search: term });
  readonly stockHint = (p: ProductDropdownItem) => `${p.currentStock} pcs`;
  readonly form = inject(FormBuilder).group({
    productUuid: [null as string | null, Validators.required],
    adjustmentType: ['INCREASE' as AdjustmentType, Validators.required],
    quantityPcs: [null as number | null, [Validators.required, Validators.min(1)]],
    reason: ['OPENING_STOCK' as AdjustmentReason, Validators.required],
    adjustmentDate: [new Date() as Date | null],
    note: [''],
  });

  err(name: string, label: string): string {
    return controlError(this.form.get(name), label);
  }

  save(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.getRawValue();
    this.busy.set(true);
    this.api.post<StockAdjustment>('/stock/adjustments', { ...v, adjustmentDate: dateToIso(v.adjustmentDate) }).subscribe({
      next: (a) => { this.notify.success(`Adjustment #${a.adjustmentNumber} posted.`); this.ref.close(true); },
      error: (e) => { this.error.set(applyServerErrors(this.form, e)); this.busy.set(false); },
    });
  }
}
