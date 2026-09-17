import { Component, OnInit, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { LabelPipe } from '../../shared/pipes';

@Component({
  selector: 'app-profile',
  imports: [RouterLink, MatButtonModule, LabelPipe],
  template: `
    <div class="page">
      <div class="page-header"><h1>My profile</h1></div>
      @if (auth.user(); as u) {
        <div class="card card-pad" style="max-width: 560px">
          <dl class="kv">
            <dt>Name</dt><dd>{{ u.userName }}</dd>
            <dt>Email</dt><dd>{{ u.email }}</dd>
            <dt>Mobile</dt><dd>{{ u.phoneNumber }}</dd>
            <dt>Role</dt><dd>{{ u.role | label }}</dd>
            <dt>Linked buyer</dt><dd>{{ u.customerName ?? 'Not linked' }}</dd>
            <dt>Linked supplier</dt><dd>{{ u.supplierName ?? 'Not linked' }}</dd>
          </dl>
          @if (u.role === 'USER' && !u.customerUuid && !u.supplierUuid) {
            <p class="muted">Your account is waiting for an administrator to link it to a customer or supplier. Reports will appear after that.</p>
          }
          <a mat-stroked-button routerLink="/change-password">Change password</a>
        </div>
      }
    </div>
  `,
})
export class ProfilePage implements OnInit {
  readonly auth = inject(AuthService);
  ngOnInit(): void {
    this.auth.reloadMe().subscribe({ error: () => {} });
  }
}
