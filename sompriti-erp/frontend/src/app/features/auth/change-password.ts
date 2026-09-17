import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ApiService } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { NotifyService } from '../../core/notify.service';
import { applyServerErrors, controlError, passwordValidator } from '../../shared/form-errors';
import { matchValidator } from './reset-password';

@Component({
  selector: 'app-change-password',
  imports: [ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  template: `
    <div class="page">
      <div class="page-header">
        <div>
          <h1>Change password</h1>
          @if (auth.mustChangePassword()) {
            <div class="subtitle negative">You must set a new password before you can continue.</div>
          }
        </div>
      </div>
      <form class="card card-pad" style="max-width: 460px" [formGroup]="form" (ngSubmit)="submit()">
        <mat-form-field class="full-width">
          <mat-label>Current password</mat-label>
          <input matInput type="password" formControlName="currentPassword" autocomplete="current-password" />
          <mat-error>{{ err('currentPassword', 'Current password') }}</mat-error>
        </mat-form-field>
        <mat-form-field class="full-width">
          <mat-label>New password</mat-label>
          <input matInput type="password" formControlName="newPassword" autocomplete="new-password" />
          <mat-hint>At least 8 characters with a letter and a number</mat-hint>
          <mat-error>{{ err('newPassword', 'New password') }}</mat-error>
        </mat-form-field>
        <mat-form-field class="full-width">
          <mat-label>Confirm new password</mat-label>
          <input matInput type="password" formControlName="confirmPassword" autocomplete="new-password" />
          <mat-error>{{ err('confirmPassword', 'Password') }}</mat-error>
        </mat-form-field>
        @if (error()) { <p class="negative">{{ error() }}</p> }
        <button mat-flat-button type="submit" [disabled]="busy()">Change password</button>
      </form>
    </div>
  `,
})
export class ChangePasswordPage {
  readonly auth = inject(AuthService);
  private readonly api = inject(ApiService);
  private readonly notify = inject(NotifyService);
  readonly form = inject(FormBuilder).nonNullable.group(
    {
      currentPassword: ['', Validators.required],
      newPassword: ['', [Validators.required, passwordValidator]],
      confirmPassword: ['', Validators.required],
    },
    { validators: matchValidator },
  );
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  err(name: string, label: string): string {
    return controlError(this.form.get(name), label);
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.busy.set(true);
    const { currentPassword, newPassword } = this.form.getRawValue();
    this.api.post('/auth/change-password', { currentPassword, newPassword }).subscribe({
      next: () => {
        this.notify.success('Password changed. Please log in with your new password.');
        this.auth.logout(); // all sessions are revoked by the server
      },
      error: (e) => {
        this.error.set(applyServerErrors(this.form, e));
        this.busy.set(false);
      },
    });
  }
}
