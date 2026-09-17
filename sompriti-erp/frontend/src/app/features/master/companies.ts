import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSortModule } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ApiService } from '../../core/api.service';
import { LayoutService } from '../../core/layout.service';
import { Company, Paged } from '../../core/models';
import { NotifyService } from '../../core/notify.service';
import { applyServerErrors, controlError } from '../../shared/form-errors';
import { ListFooter } from '../../shared/list-footer';
import { ListState } from '../../shared/list-state';

@Component({
  selector: 'app-companies',
  imports: [MatTableModule, MatSortModule, MatButtonModule, MatIconModule, MatFormFieldModule, MatInputModule, MatProgressBarModule, MatTooltipModule, MatMenuModule, ListFooter],
  template: `
    <div class="page">
      <div class="page-header">
        <div>
          <h1>Companies</h1>
          @if (!layout.isHandset()) {
            <div class="subtitle">Companies shown on purchase and sales orders and printed documents</div>
          }
        </div>
        @if (!layout.isHandset()) {
          <div class="actions"><button mat-flat-button (click)="edit()"><mat-icon>add</mat-icon>New company</button></div>
        }
      </div>

      <div class="card">
        <div class="toolbar">
          <mat-form-field class="search" subscriptSizing="dynamic">
            <mat-icon matPrefix>search</mat-icon>
            <mat-label>Search name or code</mat-label>
            <input matInput (input)="list.search($any($event.target).value)" />
          </mat-form-field>
        </div>
        @if (list.loading()) { <mat-progress-bar mode="indeterminate" /> }

        @if (layout.isHandset()) {
          <div class="m-list">
            @for (c of list.items(); track c.uuid) {
              <div class="m-card">
                <div class="m-card-head">
                  <div>
                    <div class="m-title">{{ c.companyName }}</div>
                    <div class="m-sub">{{ c.companyCode }}@if (c.phoneNumber) { · {{ c.phoneNumber }} }</div>
                  </div>
                  <button mat-icon-button [matMenuTriggerFor]="menu" aria-label="Actions"><mat-icon>more_vert</mat-icon></button>
                  <mat-menu #menu="matMenu">
                    <button mat-menu-item (click)="edit(c)"><mat-icon>edit</mat-icon>Edit</button>
                    <button mat-menu-item (click)="remove(c)"><mat-icon>delete</mat-icon>Delete</button>
                  </mat-menu>
                </div>
                @if (c.addressLine || c.city) { <div class="m-sub address">{{ c.addressLine }}@if (c.addressLine && c.city) {, }{{ c.city }}</div> }
                @if (c.licenseNumber) { <div class="m-sub">License: {{ c.licenseNumber }}</div> }
              </div>
            }
          </div>
        } @else {
          <div class="table-wrap">
            <table mat-table [dataSource]="list.items()" matSort (matSortChange)="list.onSort($event)">
              <ng-container matColumnDef="companyCode"><th mat-header-cell *matHeaderCellDef mat-sort-header>Code</th><td mat-cell *matCellDef="let c" class="code">{{ c.companyCode }}</td></ng-container>
              <ng-container matColumnDef="companyName"><th mat-header-cell *matHeaderCellDef mat-sort-header>Name</th><td mat-cell *matCellDef="let c">{{ c.companyName }}</td></ng-container>
              <ng-container matColumnDef="city"><th mat-header-cell *matHeaderCellDef mat-sort-header>City</th><td mat-cell *matCellDef="let c">{{ c.city }}</td></ng-container>
              <ng-container matColumnDef="phone"><th mat-header-cell *matHeaderCellDef>Phone</th><td mat-cell *matCellDef="let c">{{ c.phoneNumber }}</td></ng-container>
              <ng-container matColumnDef="license"><th mat-header-cell *matHeaderCellDef>License</th><td mat-cell *matCellDef="let c">{{ c.licenseNumber }}</td></ng-container>
              <ng-container matColumnDef="actions">
                <th mat-header-cell *matHeaderCellDef></th>
                <td mat-cell *matCellDef="let c" class="num nowrap">
                  <button mat-icon-button matTooltip="Edit" (click)="edit(c)"><mat-icon>edit</mat-icon></button>
                  <button mat-icon-button matTooltip="Delete" (click)="remove(c)"><mat-icon>delete</mat-icon></button>
                </td>
              </ng-container>
              <tr mat-header-row *matHeaderRowDef="columns"></tr>
              <tr mat-row *matRowDef="let row; columns: columns"></tr>
            </table>
          </div>
        }

        @if (!list.loading() && list.items().length === 0) { <div class="empty">{{ list.error() ?? 'No companies yet. Add your first company to start creating orders.' }}</div> }
        <app-list-footer [list]="list" [pageSizes]="[10, 20, 50]" />
      </div>

      @if (layout.isHandset()) {
        <button mat-fab class="fab" aria-label="New company" (click)="edit()"><mat-icon>add</mat-icon></button>
      }
    </div>
  `,
  styles: `.address { margin-top: 8px; }`,
})
export class CompaniesPage implements OnInit {
  readonly layout = inject(LayoutService);
  private readonly api = inject(ApiService);
  private readonly dialog = inject(MatDialog);
  private readonly notify = inject(NotifyService);
  readonly columns = ['companyCode', 'companyName', 'city', 'phone', 'license', 'actions'];
  readonly list = new ListState<Company>((q) => this.api.get<Paged<Company>>('/companies', { ...q }));

  ngOnInit(): void {
    this.list.reload();
  }

  edit(company?: Company): void {
    this.dialog.open(CompanyDialog, this.layout.dialog(company ?? null)).afterClosed().subscribe((saved) => {
      if (saved) this.list.resetToFirstPage();
    });
  }

  remove(c: Company): void {
    this.notify.confirm({ title: 'Delete company', message: `Delete ${c.companyName} (${c.companyCode})? It will be hidden from lists and dropdowns.`, confirmText: 'Delete', danger: true })
      .subscribe((ok) => {
        if (!ok) return;
        this.api.delete(`/companies/${c.uuid}`, { revision: c.revision }).subscribe({
          next: () => { this.notify.success('Company deleted.'); this.list.resetToFirstPage(); },
          error: (e) => this.notify.error(e),
        });
      });
  }
}

@Component({
  selector: 'app-company-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>{{ data ? 'Edit company' : 'New company' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content>
        <div class="form-grid">
          <mat-form-field><mat-label>Company name</mat-label><input matInput formControlName="companyName" /><mat-error>{{ err('companyName', 'Company name') }}</mat-error></mat-form-field>
          <mat-form-field><mat-label>Company code</mat-label><input matInput formControlName="companyCode" /><mat-error>{{ err('companyCode', 'Company code') }}</mat-error></mat-form-field>
          <mat-form-field class="span-2"><mat-label>Address</mat-label><input matInput formControlName="addressLine" /></mat-form-field>
          <mat-form-field><mat-label>City</mat-label><input matInput formControlName="city" /></mat-form-field>
          <mat-form-field><mat-label>State / Division</mat-label><input matInput formControlName="state" /></mat-form-field>
          <mat-form-field><mat-label>Postal code</mat-label><input matInput formControlName="postalCode" /></mat-form-field>
          <mat-form-field><mat-label>Phone</mat-label><input matInput formControlName="phoneNumber" /></mat-form-field>
          <mat-form-field><mat-label>Email</mat-label><input matInput formControlName="email" /><mat-error>{{ err('email', 'Email') }}</mat-error></mat-form-field>
          <mat-form-field><mat-label>License number</mat-label><input matInput formControlName="licenseNumber" /></mat-form-field>
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
export class CompanyDialog {
  readonly data = inject<Company | null>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<CompanyDialog>);
  private readonly api = inject(ApiService);
  private readonly notify = inject(NotifyService);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly form = inject(FormBuilder).group({
    companyName: [this.data?.companyName ?? '', [Validators.required, Validators.maxLength(150)]],
    companyCode: [this.data?.companyCode ?? '', [Validators.required, Validators.maxLength(20)]],
    addressLine: [this.data?.addressLine ?? ''],
    city: [this.data?.city ?? ''],
    state: [this.data?.state ?? ''],
    postalCode: [this.data?.postalCode ?? ''],
    phoneNumber: [this.data?.phoneNumber ?? ''],
    email: [this.data?.email ?? '', Validators.email],
    licenseNumber: [this.data?.licenseNumber ?? ''],
  });

  err(name: string, label: string): string {
    return controlError(this.form.get(name), label);
  }

  save(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.busy.set(true);
    const body = { ...this.form.getRawValue(), revision: this.data?.revision ?? null };
    const req = this.data ? this.api.put<Company>(`/companies/${this.data.uuid}`, body) : this.api.post<Company>('/companies', body);
    req.subscribe({
      next: () => { this.notify.success('Company saved.'); this.ref.close(true); },
      error: (e) => { this.error.set(applyServerErrors(this.form, e)); this.busy.set(false); },
    });
  }
}
