import { Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { APP_INFO, DEVELOPED_BY, DEVELOPER_ROLE } from '../../core/app-info';
import { AuthService } from '../../core/auth.service';
import { NAV_GROUPS, visibleGroups } from '../../layout/navigation';
import { openAboutDialog } from '../../shared/about-dialog';
import { LabelPipe } from '../../shared/pipes';

/** Phone-only menu screen reached from the last tab: every destination, grouped. */
@Component({
  selector: 'app-more',
  imports: [RouterLink, MatIconModule, MatButtonModule, LabelPipe],
  template: `
    <div class="page">
      @if (auth.user(); as user) {
        <section class="card card-pad profile">
          <div class="avatar"><mat-icon>account_circle</mat-icon></div>
          <div class="who">
            <div class="name">{{ user.userName }}</div>
            <div class="muted">{{ user.email }}</div>
            <span class="role">{{ user.role | label }}</span>
          </div>
        </section>
      }

      @for (group of groups(); track group.title) {
        <h2 class="group">{{ group.title }}</h2>
        <section class="card tiles">
          @for (item of group.items; track item.link + item.label) {
            <a class="tile" [routerLink]="item.link">
              <mat-icon>{{ item.icon }}</mat-icon>
              <span>{{ item.label }}</span>
            </a>
          }
        </section>
      }

      <h2 class="group">Account</h2>
      <section class="card tiles">
        <a class="tile" routerLink="/profile"><mat-icon>person</mat-icon><span>My profile</span></a>
        <a class="tile" routerLink="/change-password"><mat-icon>key</mat-icon><span>Change password</span></a>
        <button class="tile" type="button" (click)="about()"><mat-icon>info</mat-icon><span>About</span></button>
        <button class="tile" type="button" (click)="auth.logout()"><mat-icon>logout</mat-icon><span>Log out</span></button>
      </section>

      <!-- Developer credit, closing the menu screen. -->
      <footer class="credit">
        <div class="app">{{ app.name }} <span class="ver">v{{ app.version }}</span></div>
        <div class="by">{{ developedBy }}</div>
        <div class="org">{{ role }}</div>
      </footer>
    </div>
  `,
  styles: `
    .profile { display: flex; align-items: center; gap: 14px; margin-bottom: 8px; }
    .avatar mat-icon { font-size: 44px; width: 44px; height: 44px; color: var(--mat-sys-primary); }
    .who .name { font-size: 17px; font-weight: 650; }
    .who .muted { font-size: 13px; }
    .role { display: inline-block; margin-top: 6px; font-size: 11px; padding: 2px 10px; border-radius: 999px; background: var(--erp-chip-info-bg); color: var(--erp-chip-info-fg); font-weight: 600; }
    .group { font-size: 11.5px; font-weight: 600; text-transform: uppercase; letter-spacing: .06em; color: var(--erp-faint); margin: 18px 4px 8px; }
    .tiles { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); overflow: hidden; }
    .tile {
      display: flex; align-items: center; gap: 10px; padding: 16px 14px; font: inherit; font-size: 14px; text-align: left;
      text-decoration: none; color: inherit; background: none; border: 0; border-bottom: 1px solid var(--erp-border); cursor: pointer;
    }
    .tile:nth-child(odd) { border-right: 1px solid var(--erp-border); }
    .tile:active { background: #eef2f7; }
    .tile mat-icon { color: var(--mat-sys-primary); }
    .tile span { overflow: hidden; text-overflow: ellipsis; }
    .credit { margin: 26px 4px 4px; text-align: center; line-height: 1.5; }
    .credit .app { font-size: 12.5px; font-weight: 600; color: var(--erp-muted); }
    .credit .ver { font-weight: 400; color: var(--erp-faint); }
    .credit .by { font-size: 12.5px; color: var(--erp-muted); margin-top: 2px; }
    .credit .org { font-size: 11.5px; color: var(--erp-faint); }
  `,
})
export class MorePage {
  readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);

  readonly app = APP_INFO;
  readonly developedBy = DEVELOPED_BY;
  readonly role = DEVELOPER_ROLE;
  readonly groups = computed(() => visibleGroups(NAV_GROUPS, this.auth.user()));

  about(): void {
    openAboutDialog(this.dialog);
  }
}
