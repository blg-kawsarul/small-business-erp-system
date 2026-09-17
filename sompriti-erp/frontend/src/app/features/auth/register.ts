import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { applyServerErrors, bdMobileValidator, controlError, passwordValidator } from '../../shared/form-errors';
import { AppBrand } from '../../shared/app-brand';

@Component({
  selector: 'app-register',
  imports: [ReactiveFormsModule, RouterLink, MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule, MatProgressBarModule, AppBrand],
  template: `
    <div class="auth-wrap">
      <form class="card auth-card" [formGroup]="form" (ngSubmit)="submit()">
        <app-brand />
        <h1>Create account</h1>
        <p class="muted">New accounts can view reports once an administrator links them to a customer or supplier.</p>

        <mat-form-field>
          <mat-label>Full name</mat-label>
          <input matInput formControlName="userName" autocomplete="name" />
          <mat-error>{{ err('userName', 'Name') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Email</mat-label>
          <input matInput type="email" formControlName="email" autocomplete="email" />
          <mat-error>{{ err('email', 'Email') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Mobile number</mat-label>
          <input matInput formControlName="phoneNumber" placeholder="01712345678" autocomplete="tel" />
          <mat-error>{{ err('phoneNumber', 'Mobile number') }}</mat-error>
        </mat-form-field>
        <mat-form-field>
          <mat-label>Password</mat-label>
          <input matInput type="password" formControlName="password" autocomplete="new-password" />
          <mat-hint>At least 8 characters with a letter and a number</mat-hint>
          <mat-error>{{ err('password', 'Password') }}</mat-error>
        </mat-form-field>

        @if (error()) { <p class="negative">{{ error() }}</p> }
        @if (busy()) { <mat-progress-bar mode="indeterminate" /> }
        <button mat-flat-button class="full-width" type="submit" [disabled]="busy()">Create account</button>
        <div class="links"><span></span><a routerLink="/login">I already have an account</a></div>
      </form>
    </div>
  `,
})
export class RegisterPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  readonly form = inject(FormBuilder).nonNullable.group({
    userName: ['', [Validators.required, Validators.maxLength(100)]],
    email: ['', [Validators.required, Validators.email]],
    phoneNumber: ['', [Validators.required, bdMobileValidator]],
    password: ['', [Validators.required, passwordValidator]],
  });
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
    this.error.set(null);
    this.auth.register(this.form.getRawValue()).subscribe({
      next: () => void this.router.navigateByUrl('/'),
      error: (e) => {
        this.error.set(applyServerErrors(this.form, e));
        this.busy.set(false);
      },
    });
  }
}
