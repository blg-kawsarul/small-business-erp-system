import { Component, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSortModule } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { Paged, Product, Uom } from '../../core/models';
import { NotifyService } from '../../core/notify.service';
import { applyServerErrors, controlError } from '../../shared/form-errors';
import { ListState } from '../../shared/list-state';
import { MoneyPipe, QtyPipe } from '../../shared/pipes';

@Component({
  selector: 'app-products',
  imports: [MatTableModule, MatPaginatorModule, MatSortModule, MatButtonModule, MatIconModule, MatFormFieldModule, MatInputModule, MatProgressBarModule, MatTooltipModule, MatCheckboxModule, MoneyPipe, QtyPipe],
  template: `
    <div class="page">
      <div class="page-header">
        <div>
          <h1>Products</h1>
          <div class="subtitle">Prices are per piece (PCS). Stock is always counted in pieces.</div>
        </div>
        @if (auth.isAdmin()) {
          <div class="actions"><button mat-flat-button (click)="edit()"><mat-icon>add</mat-icon>New product</button></div>
        }
      </div>
      <div class="card">
        <div class="toolbar">
          <mat-form-field class="search" subscriptSizing="dynamic">
            <mat-icon matPrefix>search</mat-icon>
            <mat-label>Search name or code</mat-label>
            <input matInput (input)="list.search($any($event.target).value)" />
          </mat-form-field>
          <mat-checkbox (change)="lowOnly.set($event.checked); list.resetToFirstPage()">Low stock only</mat-checkbox>
        </div>
        @if (list.loading()) { <mat-progress-bar mode="indeterminate" /> }
        <div class="table-wrap">
          <table mat-table [dataSource]="list.items()" matSort (matSortChange)="list.onSort($event)">
            <ng-container matColumnDef="productCode"><th mat-header-cell *matHeaderCellDef mat-sort-header>Code</th><td mat-cell *matCellDef="let p" class="code">{{ p.productCode }}</td></ng-container>
            <ng-container matColumnDef="productName"><th mat-header-cell *matHeaderCellDef mat-sort-header>Name</th><td mat-cell *matCellDef="let p">{{ p.productName }}</td></ng-container>
            <ng-container matColumnDef="uom"><th mat-header-cell *matHeaderCellDef>UOM</th><td mat-cell *matCellDef="let p" class="nowrap">{{ p.uom }}@if (p.pcsPerBox) { <span class="muted"> · {{ p.pcsPerBox }}/box</span> }</td></ng-container>
            <ng-container matColumnDef="productPurchasePrice"><th mat-header-cell *matHeaderCellDef mat-sort-header class="num">Purchase / pcs</th><td mat-cell *matCellDef="let p" class="num nowrap">{{ p.productPurchasePrice | money }}</td></ng-container>
            <ng-container matColumnDef="productSalesPrice"><th mat-header-cell *matHeaderCellDef mat-sort-header class="num">Sales / pcs</th><td mat-cell *matCellDef="let p" class="num nowrap">{{ p.productSalesPrice | money }}</td></ng-container>
            <ng-container matColumnDef="currentStock"><th mat-header-cell *matHeaderCellDef mat-sort-header class="num">Stock (pcs)</th>
              <td mat-cell *matCellDef="let p" class="num" [class.negative]="p.lowStockThreshold !== null && p.currentStock <= p.lowStockThreshold">{{ p.currentStock | qty }}</td></ng-container>
            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef></th>
              <td mat-cell *matCellDef="let p" class="num nowrap">
                @if (auth.isAdmin()) {
                  <button mat-icon-button matTooltip="Edit" (click)="edit(p)"><mat-icon>edit</mat-icon></button>
                  <button mat-icon-button matTooltip="Delete" (click)="remove(p)"><mat-icon>delete</mat-icon></button>
                }
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="columns"></tr>
            <tr mat-row *matRowDef="let row; columns: columns"></tr>
          </table>
        </div>
        @if (!list.loading() && list.items().length === 0) { <div class="empty">{{ list.error() ?? 'No products found.' }}</div> }
        <mat-paginator [length]="list.total()" [pageSize]="list.query().pageSize" [pageSizeOptions]="[10, 20, 50, 100]" (page)="list.onPage($event)" />
      </div>
    </div>
  `,
})
export class ProductsPage implements OnInit {
  readonly auth = inject(AuthService);
  private readonly api = inject(ApiService);
  private readonly dialog = inject(MatDialog);
  private readonly notify = inject(NotifyService);
  readonly lowOnly = signal(false);
  readonly columns = ['productCode', 'productName', 'uom', 'productPurchasePrice', 'productSalesPrice', 'currentStock', 'actions'];
  readonly list = new ListState<Product>((q) => this.api.get<Paged<Product>>('/products', { ...q, lowStockOnly: this.lowOnly() }));

  ngOnInit(): void {
    this.list.reload();
  }

  edit(product?: Product): void {
    this.dialog.open(ProductDialog, { data: product ?? null, width: '640px' }).afterClosed().subscribe((saved) => saved && this.list.reload());
  }

  remove(p: Product): void {
    this.notify.confirm({ title: 'Delete product', message: `Delete ${p.productName} (${p.productCode})?`, confirmText: 'Delete', danger: true })
      .subscribe((ok) => {
        if (!ok) return;
        this.api.delete(`/products/${p.uuid}`, { revision: p.revision }).subscribe({
          next: () => { this.notify.success('Product deleted.'); this.list.reload(); },
          error: (e) => this.notify.error(e),
        });
      });
  }
}

@Component({
  selector: 'app-product-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule, MatSelectModule],
  template: `
    <h2 mat-dialog-title>{{ data ? 'Edit product' : 'New product' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content>
        <div class="form-grid">
          <mat-form-field class="span-2"><mat-label>Product name</mat-label><input matInput formControlName="productName" /><mat-error>{{ err('productName', 'Product name') }}</mat-error></mat-form-field>
          <mat-form-field><mat-label>Product code</mat-label><input matInput formControlName="productCode" /><mat-error>{{ err('productCode', 'Product code') }}</mat-error></mat-form-field>
          <mat-form-field>
            <mat-label>UOM</mat-label>
            <mat-select formControlName="uom"><mat-option value="PCS">PCS</mat-option><mat-option value="BOX">BOX</mat-option></mat-select>
          </mat-form-field>
          <mat-form-field><mat-label>Purchase price per pcs</mat-label><input matInput type="number" min="0" step="0.01" formControlName="productPurchasePrice" /><span matTextPrefix>Tk&nbsp;</span><mat-error>{{ err('productPurchasePrice', 'Purchase price') }}</mat-error></mat-form-field>
          <mat-form-field><mat-label>Sales price per pcs</mat-label><input matInput type="number" min="0" step="0.01" formControlName="productSalesPrice" /><span matTextPrefix>Tk&nbsp;</span><mat-error>{{ err('productSalesPrice', 'Sales price') }}</mat-error></mat-form-field>
          <mat-form-field>
            <mat-label>Pcs per box</mat-label>
            <input matInput type="number" min="1" step="1" formControlName="pcsPerBox" />
            <mat-hint>{{ form.value.uom === 'BOX' ? 'Required for BOX' : 'Optional — enables BOX entry on orders' }}</mat-hint>
            <mat-error>{{ err('pcsPerBox', 'Pcs per box') }}</mat-error>
          </mat-form-field>
          <mat-form-field><mat-label>Low stock alert at (pcs)</mat-label><input matInput type="number" min="0" step="1" formControlName="lowStockThreshold" /><mat-hint>Optional</mat-hint></mat-form-field>
        </div>
        @if (error()) { <p class="negative">{{ error() }}</p> }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="busy()">Save</button>
      </mat-dialog-actions>
    </form>
  `,
})
export class ProductDialog {
  readonly data = inject<Product | null>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<ProductDialog>);
  private readonly api = inject(ApiService);
  private readonly notify = inject(NotifyService);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly form = inject(FormBuilder).group({
    productName: [this.data?.productName ?? '', [Validators.required, Validators.maxLength(200)]],
    productCode: [this.data?.productCode ?? '', [Validators.required, Validators.maxLength(50)]],
    uom: [(this.data?.uom ?? 'PCS') as Uom, Validators.required],
    productPurchasePrice: [this.data?.productPurchasePrice ?? null as number | null, [Validators.required, Validators.min(0)]],
    productSalesPrice: [this.data?.productSalesPrice ?? null as number | null, [Validators.required, Validators.min(0)]],
    pcsPerBox: [this.data?.pcsPerBox ?? null as number | null, [Validators.min(1)]],
    lowStockThreshold: [this.data?.lowStockThreshold ?? null as number | null, [Validators.min(0)]],
  });

  constructor() {
    const syncBox = (uom: Uom | null) => {
      const ctrl = this.form.controls.pcsPerBox;
      ctrl.setValidators(uom === 'BOX' ? [Validators.required, Validators.min(1)] : [Validators.min(1)]);
      ctrl.updateValueAndValidity({ emitEvent: false });
    };
    syncBox(this.form.controls.uom.value);
    this.form.controls.uom.valueChanges.pipe(takeUntilDestroyed()).subscribe(syncBox);
  }

  err(name: string, label: string): string {
    return controlError(this.form.get(name), label);
  }

  save(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.busy.set(true);
    const body = { ...this.form.getRawValue(), revision: this.data?.revision ?? null };
    const req = this.data ? this.api.put<Product>(`/products/${this.data.uuid}`, body) : this.api.post<Product>('/products', body);
    req.subscribe({
      next: () => { this.notify.success('Product saved.'); this.ref.close(true); },
      error: (e) => { this.error.set(applyServerErrors(this.form, e)); this.busy.set(false); },
    });
  }
}
