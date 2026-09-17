import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { RouterLink } from '@angular/router';
import { ApiService, errorMessage } from '../../core/api.service';

@Component({
  selector: 'app-forgot-password',
  imports: [ReactiveFormsModule, RouterLink, MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule],
  template: `
    <div class="auth-wrap">
      <form class="card auth-card" [formGroup]="form" (ngSubmit)="submit()">
        <div class="brand"><mat-icon>inventory_2</mat-icon> Sompriti ERP</div>
        <h1>Forgot password</h1>
        @if (sent()) {
          <p>If an account exists for <strong>{{ form.value.email }}</strong>, we have emailed a link to reset your password. The link expires in 30 minutes.</p>
          <a mat-flat-button class="full-width" routerLink="/login">Back to log in</a>
        } @else {
          <p class="muted">Enter your account email and we will send you a reset link.</p>
          <mat-form-field>
            <mat-label>Email</mat-label>
            <input matInput type="email" formControlName="email" autocomplete="email" />
          </mat-form-field>
          @if (error()) { <p class="negative">{{ error() }}</p> }
          <button mat-flat-button class="full-width" type="submit" [disabled]="busy()">Send reset link</button>
          <div class="links"><span></span><a routerLink="/login">Back to log in</a></div>
        }
      </form>
    </div>
  `,
})
export class ForgotPasswordPage {
  private readonly api = inject(ApiService);
  readonly form = inject(FormBuilder).nonNullable.group({ email: ['', [Validators.required, Validators.email]] });
  readonly busy = signal(false);
  readonly sent = signal(false);
  readonly error = signal<string | null>(null);

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.busy.set(true);
    this.api.post('/auth/forgot-password', this.form.getRawValue()).subscribe({
      next: () => this.sent.set(true),
      error: (e) => {
        this.error.set(errorMessage(e));
        this.busy.set(false);
      },
    });
  }
}
