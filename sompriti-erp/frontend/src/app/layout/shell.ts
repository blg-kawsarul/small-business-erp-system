import { BreakpointObserver } from '@angular/cdk/layout';
import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from '../core/auth.service';
import { Role } from '../core/models';
import { LabelPipe } from '../shared/pipes';

interface NavItem {
  label: string;
  icon: string;
  link: string;
  roles: Role[];
  exact?: boolean;
  /** Extra condition, e.g. USER must be linked to a buyer. */
  show?: () => boolean;
}

interface NavGroup {
  title: string;
  items: NavItem[];
}

@Component({
  selector: 'app-shell',
  imports: [
    RouterOutlet, RouterLink, RouterLinkActive, MatSidenavModule, MatToolbarModule, MatListModule,
    MatIconModule, MatButtonModule, MatMenuModule, MatDividerModule, LabelPipe,
  ],
  template: `
    <mat-sidenav-container class="container">
      <mat-sidenav #nav [mode]="isMobile() ? 'over' : 'side'" [opened]="!isMobile()" class="sidenav">
        <div class="brand">
          <mat-icon>inventory_2</mat-icon>
          <span>Sompriti ERP</span>
        </div>
        @for (group of groups(); track group.title) {
          <div class="group-title">{{ group.title }}</div>
          <mat-nav-list>
            @for (item of group.items; track item.link) {
              <a mat-list-item [routerLink]="item.link" routerLinkActive="active"
                 [routerLinkActiveOptions]="{ exact: item.exact ?? false }" (click)="isMobile() && nav.close()">
                <mat-icon matListItemIcon>{{ item.icon }}</mat-icon>
                <span matListItemTitle>{{ item.label }}</span>
              </a>
            }
          </mat-nav-list>
        }
      </mat-sidenav>

      <mat-sidenav-content>
        <mat-toolbar class="topbar">
          @if (isMobile()) {
            <button mat-icon-button aria-label="Menu" (click)="nav.toggle()"><mat-icon>menu</mat-icon></button>
          }
          <span class="spacer"></span>
          @if (auth.user(); as user) {
            <button mat-button [matMenuTriggerFor]="userMenu" class="user-button">
              <mat-icon>account_circle</mat-icon>
              <span class="user-name">{{ user.userName }}</span>
              <span class="role">{{ user.role | label }}</span>
            </button>
            <mat-menu #userMenu="matMenu" xPosition="before">
              <a mat-menu-item routerLink="/profile"><mat-icon>person</mat-icon>My profile</a>
              <a mat-menu-item routerLink="/change-password"><mat-icon>key</mat-icon>Change password</a>
              <mat-divider />
              <button mat-menu-item (click)="auth.logout()"><mat-icon>logout</mat-icon>Log out</button>
            </mat-menu>
          }
        </mat-toolbar>
        <main>
          <router-outlet />
        </main>
      </mat-sidenav-content>
    </mat-sidenav-container>
  `,
  styles: `
    .container { height: 100vh; background: var(--erp-bg); }
    .sidenav { width: 248px; border-right: 1px solid var(--erp-border); background: #fff; }
    .brand { display: flex; align-items: center; gap: 10px; padding: 18px 20px 10px; font-weight: 700; font-size: 17px; color: var(--mat-sys-primary); }
    .group-title { padding: 14px 20px 2px; font-size: 11px; font-weight: 600; text-transform: uppercase; letter-spacing: .06em; color: var(--erp-muted); }
    mat-nav-list { padding-top: 0; }
    a.active { background: var(--mat-sys-secondary-container); border-radius: 999px; }
    .topbar { position: sticky; top: 0; z-index: 5; background: #fff; border-bottom: 1px solid var(--erp-border); height: 56px; }
    .spacer { flex: 1; }
    .user-button .user-name { margin: 0 6px; }
    .role { font-size: 11px; padding: 1px 8px; border-radius: 999px; background: var(--erp-chip-info-bg); color: var(--erp-chip-info-fg); }
    @media (max-width: 600px) { .user-name { display: none; } }
  `,
})
export class Shell {
  readonly auth = inject(AuthService);
  readonly isMobile = toSignal(inject(BreakpointObserver).observe('(max-width: 960px)').pipe(map((r) => r.matches)), { initialValue: false });

  private readonly allGroups: NavGroup[] = [
    {
      title: 'Overview',
      items: [{ label: 'Dashboard', icon: 'dashboard', link: '/', roles: ['ADMIN', 'MANAGER', 'USER'], exact: true }],
    },
    {
      title: 'Transactions',
      items: [
        { label: 'Sales orders', icon: 'point_of_sale', link: '/sales-orders', roles: ['ADMIN', 'MANAGER'] },
        { label: 'Purchase orders', icon: 'shopping_cart', link: '/purchase-orders', roles: ['ADMIN'] },
      ],
    },
    {
      title: 'Inventory',
      items: [
        { label: 'Products', icon: 'category', link: '/products', roles: ['ADMIN', 'MANAGER'] },
        { label: 'Stock balance', icon: 'inventory', link: '/stock', roles: ['ADMIN', 'MANAGER'], exact: true },
        { label: 'Stock ledger', icon: 'receipt_long', link: '/stock/ledger', roles: ['ADMIN', 'MANAGER'] },
        { label: 'Stock adjustments', icon: 'tune', link: '/stock/adjustments', roles: ['ADMIN'] },
      ],
    },
    {
      title: 'Master data',
      items: [
        { label: 'Customers', icon: 'groups', link: '/customers', roles: ['ADMIN', 'MANAGER'] },
        { label: 'Suppliers', icon: 'local_shipping', link: '/suppliers', roles: ['ADMIN'] },
        { label: 'Companies', icon: 'business', link: '/companies', roles: ['ADMIN'] },
      ],
    },
    {
      title: 'Reports',
      items: [
        // USER sees only reports allowed by the linked buyer / supplier (SRS 5)
        { label: 'My purchases', icon: 'receipt', link: '/sales-orders', roles: ['USER'], show: () => !!this.auth.user()?.customerUuid },
        { label: 'My supplies', icon: 'receipt', link: '/purchase-orders', roles: ['USER'], show: () => !!this.auth.user()?.supplierUuid },
        { label: 'Customer report', icon: 'person_search', link: '/reports/customers', roles: ['ADMIN', 'MANAGER', 'USER'], show: () => !this.auth.isUser() || !!this.auth.user()?.customerUuid },
        { label: 'Supplier report', icon: 'contact_page', link: '/reports/suppliers', roles: ['ADMIN', 'USER'], show: () => !this.auth.isUser() || !!this.auth.user()?.supplierUuid },
        { label: 'Company report', icon: 'domain', link: '/reports/companies', roles: ['ADMIN', 'MANAGER'] },
        { label: 'Due report', icon: 'pending_actions', link: '/reports/dues', roles: ['ADMIN', 'MANAGER', 'USER'], show: () => !this.auth.isUser() || !!(this.auth.user()?.customerUuid || this.auth.user()?.supplierUuid) },
      ],
    },
    {
      title: 'Administration',
      items: [
        { label: 'Users', icon: 'manage_accounts', link: '/users', roles: ['ADMIN'] },
        { label: 'SMS log', icon: 'sms', link: '/sms', roles: ['ADMIN'] },
      ],
    },
  ];

  readonly groups = computed(() => {
    const role = this.auth.role();
    if (!role) return [];
    return this.allGroups
      .map((g) => ({ ...g, items: g.items.filter((i) => i.roles.includes(role) && (i.show?.() ?? true)) }))
      .filter((g) => g.items.length > 0);
  });
}
