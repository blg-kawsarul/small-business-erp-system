import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
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
import { Paged, Party } from '../../core/models';
import { NotifyService } from '../../core/notify.service';
import { applyServerErrors, bdMobileValidator, controlError } from '../../shared/form-errors';
import { ListState } from '../../shared/list-state';

type PartyKind = 'customer' | 'supplier';

/** Customers and suppliers share this page; the kind comes from route data. */
@Component({
  selector: 'app-parties',
  imports: [RouterLink, MatTableModule, MatPaginatorModule, MatSortModule, MatButtonModule, MatIconModule, MatFormFieldModule, MatInputModule, MatProgressBarModule, MatTooltipModule],
  template: `
    <div class="page">
      <div class="page-header">
        <div>
          <h1>{{ title() }}</h1>
          <div class="subtitle">Codes are generated automatically. SMS payment notices go to the mobile number.</div>
        </div>
        <div class="actions"><button mat-flat-button (click)="edit()"><mat-icon>add</mat-icon>New {{ kind() }}</button></div>
      </div>
      <div class="card">
        <div class="toolbar">
          <mat-form-field class="search" subscriptSizing="dynamic">
            <mat-icon matPrefix>search</mat-icon>
            <mat-label>Search name, code or mobile</mat-label>
            <input matInput (input)="list.search($any($event.target).value)" />
          </mat-form-field>
        </div>
        @if (list.loading()) { <mat-progress-bar mode="indeterminate" /> }
        <div class="table-wrap">
          <table mat-table [dataSource]="list.items()" matSort (matSortChange)="list.onSort($event)">
            <ng-container matColumnDef="code"><th mat-header-cell *matHeaderCellDef mat-sort-header>Code</th><td mat-cell *matCellDef="let p" class="code">{{ p.code }}</td></ng-container>
            <ng-container matColumnDef="name"><th mat-header-cell *matHeaderCellDef mat-sort-header>Name</th><td mat-cell *matCellDef="let p">{{ p.name }}</td></ng-container>
            <ng-container matColumnDef="mobile"><th mat-header-cell *matHeaderCellDef>Mobile</th><td mat-cell *matCellDef="let p" class="nowrap">{{ p.mobileNumber }}</td></ng-container>
            <ng-container matColumnDef="city"><th mat-header-cell *matHeaderCellDef mat-sort-header>City</th><td mat-cell *matCellDef="let p">{{ p.city }}</td></ng-container>
            <ng-container matColumnDef="address"><th mat-header-cell *matHeaderCellDef>Address</th><td mat-cell *matCellDef="let p">{{ p.address }}</td></ng-container>
            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef></th>
              <td mat-cell *matCellDef="let p" class="num nowrap">
                <a mat-icon-button matTooltip="Orders" [routerLink]="ordersLink()" [queryParams]="{ partyUuid: p.uuid, partyLabel: p.name + ' (' + p.code + ')' }"><mat-icon>receipt_long</mat-icon></a>
                <button mat-icon-button matTooltip="Edit" (click)="edit(p)"><mat-icon>edit</mat-icon></button>
                @if (auth.isAdmin()) {
                  <button mat-icon-button matTooltip="Delete" (click)="remove(p)"><mat-icon>delete</mat-icon></button>
                }
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="columns"></tr>
            <tr mat-row *matRowDef="let row; columns: columns"></tr>
          </table>
        </div>
        @if (!list.loading() && list.items().length === 0) { <div class="empty">{{ list.error() ?? 'No ' + kind() + 's found.' }}</div> }
        <mat-paginator [length]="list.total()" [pageSize]="list.query().pageSize" [pageSizeOptions]="[10, 20, 50, 100]" (page)="list.onPage($event)" />
      </div>
    </div>
  `,
})
export class PartiesPage implements OnInit {
  readonly kind = input<PartyKind>('customer');
  readonly auth = inject(AuthService);
  private readonly api = inject(ApiService);
  private readonly dialog = inject(MatDialog);
  private readonly notify = inject(NotifyService);

  readonly title = computed(() => (this.kind() === 'customer' ? 'Customers' : 'Suppliers'));
  readonly path = computed(() => (this.kind() === 'customer' ? '/customers' : '/suppliers'));
  readonly ordersLink = computed(() => (this.kind() === 'customer' ? '/sales-orders' : '/purchase-orders'));
  readonly columns = ['code', 'name', 'mobile', 'city', 'address', 'actions'];
  readonly list = new ListState<Party>((q) => this.api.get<Paged<Party>>(this.path(), { ...q }));

  ngOnInit(): void {
    this.list.reload();
  }

  edit(party?: Party): void {
    this.dialog.open(PartyDialog, { data: { kind: this.kind(), party: party ?? null }, width: '640px' }).afterClosed()
      .subscribe((saved) => saved && this.list.reload());
  }

  remove(p: Party): void {
    this.notify.confirm({ title: `Delete ${this.kind()}`, message: `Delete ${p.name} (${p.code})? Existing orders keep their history.`, confirmText: 'Delete', danger: true })
      .subscribe((ok) => {
        if (!ok) return;
        this.api.delete(`${this.path()}/${p.uuid}`, { revision: p.revision }).subscribe({
          next: () => { this.notify.success('Deleted.'); this.list.reload(); },
          error: (e) => this.notify.error(e),
        });
      });
  }
}

@Component({
  selector: 'app-party-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>{{ data.party ? 'Edit' : 'New' }} {{ data.kind }} @if (data.party) { <span class="code">#{{ data.party.code }}</span> }</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content>
        <div class="form-grid">
          <mat-form-field class="span-2"><mat-label>{{ data.kind === 'customer' ? 'Customer' : 'Supplier' }} name</mat-label><input matInput formControlName="name" /><mat-error>{{ err('name', 'Name') }}</mat-error></mat-form-field>
          <mat-form-field><mat-label>Mobile number</mat-label><input matInput formControlName="mobileNumber" placeholder="01712345678" /><mat-error>{{ err('mobileNumber', 'Mobile number') }}</mat-error></mat-form-field>
          <mat-form-field><mat-label>NID</mat-label><input matInput formControlName="nid" /></mat-form-field>
          <mat-form-field><mat-label>TIN</mat-label><input matInput formControlName="tin" /></mat-form-field>
          <mat-form-field><mat-label>City</mat-label><input matInput formControlName="city" /></mat-form-field>
          <mat-form-field class="span-2"><mat-label>Address</mat-label><input matInput formControlName="address" /></mat-form-field>
          <mat-form-field><mat-label>State / Division</mat-label><input matInput formControlName="state" /></mat-form-field>
          <mat-form-field><mat-label>Postal code</mat-label><input matInput formControlName="postalCode" /></mat-form-field>
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
export class PartyDialog {
  readonly data = inject<{ kind: PartyKind; party: Party | null }>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<PartyDialog>);
  private readonly api = inject(ApiService);
  private readonly notify = inject(NotifyService);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  private readonly p = this.data.party;
  readonly form = inject(FormBuilder).group({
    name: [this.p?.name ?? '', [Validators.required, Validators.maxLength(150)]],
    mobileNumber: [this.p?.mobileNumber ?? '', [Validators.required, bdMobileValidator]],
    nid: [this.p?.nid ?? ''],
    tin: [this.p?.tin ?? ''],
    address: [this.p?.address ?? ''],
    city: [this.p?.city ?? ''],
    state: [this.p?.state ?? ''],
    postalCode: [this.p?.postalCode ?? ''],
  });

  err(name: string, label: string): string {
    return controlError(this.form.get(name), label);
  }

  save(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.busy.set(true);
    const path = this.data.kind === 'customer' ? '/customers' : '/suppliers';
    const body = { ...this.form.getRawValue(), revision: this.p?.revision ?? null };
    const req = this.p ? this.api.put<Party>(`${path}/${this.p.uuid}`, body) : this.api.post<Party>(path, body);
    req.subscribe({
      next: (saved) => { this.notify.success(`Saved ${saved.name} (${saved.code}).`); this.ref.close(saved); },
      error: (e) => { this.error.set(applyServerErrors(this.form, e)); this.busy.set(false); },
    });
  }
}
