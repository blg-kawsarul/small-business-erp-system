import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { LayoutService } from '../../core/layout.service';
import { AppUser, DropdownItem, Paged, Role } from '../../core/models';
import { NotifyService } from '../../core/notify.service';
import { applyServerErrors, bdMobileValidator, controlError, passwordValidator } from '../../shared/form-errors';
import { ListFooter } from '../../shared/list-footer';
import { ListState } from '../../shared/list-state';
import { SearchSelect } from '../../shared/search-select';
import { StatusChip } from '../../shared/status-chip';

@Component({
  selector: 'app-users',
  imports: [DatePipe, ReactiveFormsModule, MatTableModule, MatButtonModule, MatIconModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatCheckboxModule, MatProgressBarModule, MatTooltipModule, MatMenuModule, ListFooter, StatusChip],
  template: `
    <div class="page">
      <div class="page-header">
        <div>
          <h1>Users</h1>
          @if (!layout.isHandset()) {
            <div class="subtitle">Manage roles and link USER accounts to a buyer (customer) and/or supplier so they can see their own reports.</div>
          }
        </div>
        @if (!layout.isHandset()) {
          <div class="actions"><button mat-flat-button (click)="edit()"><mat-icon>person_add</mat-icon>New user</button></div>
        }
      </div>

      <div class="card">
        <div class="toolbar">
          <mat-form-field class="search" subscriptSizing="dynamic">
            <mat-icon matPrefix>search</mat-icon>
            <mat-label>Search name, email or mobile</mat-label>
            <input matInput (input)="list.search($any($event.target).value)" />
          </mat-form-field>
          <mat-form-field subscriptSizing="dynamic" style="width: 150px">
            <mat-label>Role</mat-label>
            <mat-select [formControl]="role" (selectionChange)="list.resetToFirstPage()">
              <mat-option [value]="null">All roles</mat-option>
              <mat-option value="ADMIN">Admin</mat-option>
              <mat-option value="MANAGER">Manager</mat-option>
              <mat-option value="USER">User</mat-option>
            </mat-select>
          </mat-form-field>
          <mat-checkbox [formControl]="includeDeleted" (change)="list.resetToFirstPage()">Show deleted</mat-checkbox>
        </div>
        @if (list.loading()) { <mat-progress-bar mode="indeterminate" /> }

        @if (layout.isHandset()) {
          <div class="m-list">
            @for (u of list.items(); track u.uuid) {
              <div class="m-card">
                <div class="m-card-head">
                  <div>
                    <div class="m-title">{{ u.userName }} @if (u.isLocked) { <mat-icon class="lock">lock</mat-icon> }</div>
                    <div class="m-sub">{{ u.email }}</div>
                    <div class="m-sub">{{ u.phoneNumber }}</div>
                  </div>
                  <div class="m-right">
                    <app-status [value]="u.role" />
                    @if (u.status === 'ACTIVE') {
                      <button mat-icon-button [matMenuTriggerFor]="menu" aria-label="Actions"><mat-icon>more_vert</mat-icon></button>
                      <mat-menu #menu="matMenu">
                        <button mat-menu-item (click)="edit(u)"><mat-icon>edit</mat-icon>Edit</button>
                        @if (u.isLocked) { <button mat-menu-item (click)="unlock(u)"><mat-icon>lock_open</mat-icon>Unlock</button> }
                        @if (u.uuid !== auth.user()?.uuid) { <button mat-menu-item (click)="remove(u)"><mat-icon>delete</mat-icon>Delete</button> }
                      </mat-menu>
                    } @else { <app-status [value]="u.status" /> }
                  </div>
                </div>
                @if (u.customerName || u.supplierName) {
                  <div class="m-meta two">
                    @if (u.customerName) { <div><span class="k">Buyer</span><span class="v">{{ u.customerName }}</span></div> }
                    @if (u.supplierName) { <div><span class="k">Supplier</span><span class="v">{{ u.supplierName }}</span></div> }
                  </div>
                }
              </div>
            }
          </div>
        } @else {
          <div class="table-wrap">
            <table mat-table [dataSource]="list.items()">
              <ng-container matColumnDef="name"><th mat-header-cell *matHeaderCellDef>Name</th>
                <td mat-cell *matCellDef="let u">{{ u.userName }} @if (u.isLocked) { <mat-icon class="lock" matTooltip="Locked after failed logins">lock</mat-icon> }<div class="muted">{{ u.email }}</div></td></ng-container>
              <ng-container matColumnDef="mobile"><th mat-header-cell *matHeaderCellDef>Mobile</th><td mat-cell *matCellDef="let u" class="nowrap">{{ u.phoneNumber }}</td></ng-container>
              <ng-container matColumnDef="role"><th mat-header-cell *matHeaderCellDef>Role</th><td mat-cell *matCellDef="let u"><app-status [value]="u.role" /></td></ng-container>
              <ng-container matColumnDef="links"><th mat-header-cell *matHeaderCellDef>Linked to</th>
                <td mat-cell *matCellDef="let u">
                  @if (u.customerName) { <div><span class="muted">Buyer:</span> {{ u.customerName }} <span class="code">{{ u.customerCode }}</span></div> }
                  @if (u.supplierName) { <div><span class="muted">Supplier:</span> {{ u.supplierName }} <span class="code">{{ u.supplierCode }}</span></div> }
                  @if (!u.customerName && !u.supplierName) { <span class="muted">—</span> }
                </td></ng-container>
              <ng-container matColumnDef="lastLogin"><th mat-header-cell *matHeaderCellDef>Last login</th><td mat-cell *matCellDef="let u" class="muted nowrap">{{ u.lastLoginDate ? (u.lastLoginDate | date: 'dd MMM yyyy, h:mm a') : 'Never' }}</td></ng-container>
              <ng-container matColumnDef="status"><th mat-header-cell *matHeaderCellDef>Status</th><td mat-cell *matCellDef="let u"><app-status [value]="u.status" /></td></ng-container>
              <ng-container matColumnDef="actions"><th mat-header-cell *matHeaderCellDef></th>
                <td mat-cell *matCellDef="let u" class="num nowrap">
                  @if (u.status === 'ACTIVE') {
                    @if (u.isLocked) { <button mat-icon-button matTooltip="Unlock" (click)="unlock(u)"><mat-icon>lock_open</mat-icon></button> }
                    <button mat-icon-button matTooltip="Edit" (click)="edit(u)"><mat-icon>edit</mat-icon></button>
                    @if (u.uuid !== auth.user()?.uuid) { <button mat-icon-button matTooltip="Delete" (click)="remove(u)"><mat-icon>delete</mat-icon></button> }
                  }
                </td></ng-container>
              <tr mat-header-row *matHeaderRowDef="columns"></tr>
              <tr mat-row *matRowDef="let row; columns: columns"></tr>
            </table>
          </div>
        }

        @if (!list.loading() && list.items().length === 0) { <div class="empty">{{ list.error() ?? 'No users found.' }}</div> }
        <app-list-footer [list]="list" />
      </div>

      @if (layout.isHandset()) {
        <button mat-fab class="fab" aria-label="New user" (click)="edit()"><mat-icon>person_add</mat-icon></button>
      }
    </div>
  `,
  styles: `.lock { font-size: 16px; width: 16px; height: 16px; vertical-align: middle; color: var(--erp-negative); }`,
})
export class UsersPage implements OnInit {
  readonly auth = inject(AuthService);
  readonly layout = inject(LayoutService);
  private readonly api = inject(ApiService);
  private readonly dialog = inject(MatDialog);
  private readonly notify = inject(NotifyService);
  readonly role = new FormControl<Role | null>(null);
  readonly includeDeleted = new FormControl(false, { nonNullable: true });
  readonly columns = ['name', 'mobile', 'role', 'links', 'lastLogin', 'status', 'actions'];
  readonly list = new ListState<AppUser>((q) => this.api.get<Paged<AppUser>>('/users', { ...q, role: this.role.value, includeDeleted: this.includeDeleted.value }));

  ngOnInit(): void {
    this.list.reload();
  }

  edit(user?: AppUser): void {
    this.dialog.open(UserDialog, this.layout.dialog(user ?? null)).afterClosed().subscribe((ok) => ok && this.list.resetToFirstPage());
  }

  unlock(u: AppUser): void {
    this.api.post(`/users/${u.uuid}/unlock`, { revision: u.revision }).subscribe({
      next: () => { this.notify.success('User unlocked.'); this.list.resetToFirstPage(); },
      error: (e) => this.notify.error(e),
    });
  }

  remove(u: AppUser): void {
    this.notify.confirm({ title: 'Delete user', message: `Delete ${u.userName} (${u.email})? They will be logged out and can no longer sign in.`, confirmText: 'Delete', danger: true })
      .subscribe((ok) => {
        if (!ok) return;
        this.api.delete(`/users/${u.uuid}`, { revision: u.revision }).subscribe({
          next: () => { this.notify.success('User deleted.'); this.list.resetToFirstPage(); },
          error: (e) => this.notify.error(e),
        });
      });
  }
}

@Component({
  selector: 'app-user-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule, SearchSelect],
  template: `
    <h2 mat-dialog-title>{{ data ? 'Edit user' : 'New user' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content>
        <div class="form-grid">
          <mat-form-field><mat-label>Name</mat-label><input matInput formControlName="userName" /><mat-error>{{ err('userName', 'Name') }}</mat-error></mat-form-field>
          <mat-form-field><mat-label>Email</mat-label><input matInput type="email" formControlName="email" /><mat-error>{{ err('email', 'Email') }}</mat-error></mat-form-field>
          <mat-form-field><mat-label>Mobile number</mat-label><input matInput formControlName="phoneNumber" placeholder="01712345678" /><mat-error>{{ err('phoneNumber', 'Mobile number') }}</mat-error></mat-form-field>
          <mat-form-field>
            <mat-label>Role</mat-label>
            <mat-select formControlName="role">
              <mat-option value="ADMIN">Admin — full access</mat-option>
              <mat-option value="MANAGER">Manager — sales and customers</mat-option>
              <mat-option value="USER">User — read-only own reports</mat-option>
            </mat-select>
            @if (isSelf) { <mat-hint>You cannot change your own role.</mat-hint> }
          </mat-form-field>
          <div class="span-2">
            <app-search-select label="Linked buyer (customer)" [control]="form.controls.customerUuid" [fetch]="fetchCustomers"
              [initialLabel]="data?.customerName ? data!.customerName + ' (' + data!.customerCode + ')' : null" />
          </div>
          <div class="span-2">
            <app-search-select label="Linked supplier" [control]="form.controls.supplierUuid" [fetch]="fetchSuppliers"
              [initialLabel]="data?.supplierName ? data!.supplierName + ' (' + data!.supplierCode + ')' : null" />
          </div>
          <mat-form-field class="span-2">
            <mat-label>{{ data ? 'New password (leave empty to keep)' : 'Temporary password' }}</mat-label>
            <input matInput type="password" formControlName="password" autocomplete="new-password" />
            <mat-hint>The user must change it at next login.</mat-hint>
            <mat-error>{{ err('password', 'Password') }}</mat-error>
          </mat-form-field>
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
export class UserDialog {
  readonly data = inject<AppUser | null>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<UserDialog>);
  private readonly api = inject(ApiService);
  private readonly notify = inject(NotifyService);
  private readonly auth = inject(AuthService);
  readonly isSelf = this.data?.uuid === this.auth.user()?.uuid;
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly fetchCustomers = (term: string) => this.api.get<DropdownItem[]>('/customers/dropdown', { search: term });
  readonly fetchSuppliers = (term: string) => this.api.get<DropdownItem[]>('/suppliers/dropdown', { search: term });
  readonly form = inject(FormBuilder).group({
    userName: [this.data?.userName ?? '', [Validators.required, Validators.maxLength(100)]],
    email: [this.data?.email ?? '', [Validators.required, Validators.email]],
    phoneNumber: [this.data?.phoneNumber ?? '', [Validators.required, bdMobileValidator]],
    role: [{ value: (this.data?.role ?? 'USER') as Role, disabled: this.isSelf }, Validators.required],
    customerUuid: [this.data?.customerUuid ?? null as string | null],
    supplierUuid: [this.data?.supplierUuid ?? null as string | null],
    password: ['', this.data ? [passwordValidator] : [Validators.required, passwordValidator]],
  });

  err(name: string, label: string): string {
    return controlError(this.form.get(name), label);
  }

  save(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.busy.set(true);
    const v = this.form.getRawValue();
    const body = { ...v, password: v.password || null, revision: this.data?.revision ?? null };
    const req = this.data ? this.api.put<AppUser>(`/users/${this.data.uuid}`, body) : this.api.post<AppUser>('/users', body);
    req.subscribe({
      next: () => { this.notify.success('User saved.'); this.ref.close(true); },
      error: (e) => { this.error.set(applyServerErrors(this.form, e)); this.busy.set(false); },
    });
  }
}
