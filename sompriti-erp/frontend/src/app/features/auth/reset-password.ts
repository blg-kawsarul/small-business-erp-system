import { Component, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ApiService, errorMessage } from '../../core/api.service';
import { controlError, passwordValidator } from '../../shared/form-errors';

export function matchValidator(group: AbstractControl) {
  const a = group.get('newPassword')?.value;
  const b = group.get('confirmPassword');
  if (b && b.value && a !== b.value) b.setErrors({ ...(b.errors ?? {}), mismatch: true });
  return null;
}

@Component({
  selector: 'app-reset-password',
  imports: [ReactiveFormsModule, RouterLink, MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule],
  template: `
    <div class="auth-wrap">
      <form class="card auth-card" [formGroup]="form" (ngSubmit)="submit()">
        <div class="brand"><mat-icon>inventory_2</mat-icon> Sompriti ERP</div>
        <h1>Set a new password</h1>
        @if (done()) {
          <p>Your password has been reset. You can now log in with the new password.</p>
          <a mat-flat-button class="full-width" routerLink="/login">Log in</a>
        } @else if (!token) {
          <p class="negative">This reset link is invalid. Request a new one.</p>
          <a mat-button routerLink="/forgot-password">Request a new link</a>
        } @else {
          <mat-form-field>
            <mat-label>New password</mat-label>
            <input matInput type="password" formControlName="newPassword" autocomplete="new-password" />
            <mat-hint>At least 8 characters with a letter and a number</mat-hint>
            <mat-error>{{ err('newPassword') }}</mat-error>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Confirm new password</mat-label>
            <input matInput type="password" formControlName="confirmPassword" autocomplete="new-password" />
            <mat-error>{{ err('confirmPassword') }}</mat-error>
          </mat-form-field>
          @if (error()) { <p class="negative">{{ error() }}</p> }
          <button mat-flat-button class="full-width" type="submit" [disabled]="busy()">Reset password</button>
        }
      </form>
    </div>
  `,
})
export class ResetPasswordPage {
  private readonly api = inject(ApiService);
  readonly token = inject(ActivatedRoute).snapshot.queryParamMap.get('token');
  readonly form = inject(FormBuilder).nonNullable.group(
    {
      newPassword: ['', [Validators.required, passwordValidator]],
      confirmPassword: ['', Validators.required],
    },
    { validators: matchValidator },
  );
  readonly busy = signal(false);
  readonly done = signal(false);
  readonly error = signal<string | null>(null);

  err(name: string): string {
    return controlError(this.form.get(name), 'Password');
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.busy.set(true);
    this.api.post('/auth/reset-password', { token: this.token, newPassword: this.form.getRawValue().newPassword }).subscribe({
      next: () => this.done.set(true),
      error: (e) => {
        this.error.set(errorMessage(e));
        this.busy.set(false);
      },
    });
  }
}
