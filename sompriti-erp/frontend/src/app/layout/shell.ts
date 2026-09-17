import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Location } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatDialog } from '@angular/material/dialog';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, map, startWith } from 'rxjs';
import { APP_INFO, DEVELOPED_BY } from '../core/app-info';
import { openAboutDialog } from '../shared/about-dialog';
import { AuthService } from '../core/auth.service';
import { AppTitleStrategy } from '../core/title.service';
import { LayoutService } from '../core/layout.service';
import { LabelPipe } from '../shared/pipes';
import { NAV_GROUPS, NavItem, bottomTabs, visibleGroups } from './navigation';

@Component({
  selector: 'app-shell',
  imports: [
    RouterOutlet, RouterLink, RouterLinkActive, MatSidenavModule, MatToolbarModule, MatListModule,
    MatIconModule, MatButtonModule, MatMenuModule, MatDividerModule, LabelPipe,
  ],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  readonly auth = inject(AuthService);
  readonly layout = inject(LayoutService);
  private readonly router = inject(Router);
  private readonly location = inject(Location);
  private readonly dialog = inject(MatDialog);

  readonly app = APP_INFO;
  readonly developedBy = DEVELOPED_BY;

  /** Current URL, updated on every navigation. */
  private readonly url = toSignal(
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map((e) => e.urlAfterRedirects),
      startWith(this.router.url),
    ),
    { initialValue: this.router.url },
  );

  readonly groups = computed(() => visibleGroups(NAV_GROUPS, this.auth.user()));
  readonly tabs = computed(() => bottomTabs(this.auth.user()));

  /** Page title for the mobile top bar (set by the route's `title`). */
  readonly pageTitle = inject(AppTitleStrategy).pageTitle;

  /** A tab root shows the brand; deeper screens show a back arrow. */
  readonly canGoBack = computed(() => !this.tabs().some((t) => t.link === this.url().split('?')[0]));

  isTabActive(tab: NavItem): boolean {
    const current = this.url().split('?')[0];
    return tab.link === '/' ? current === '/' : current.startsWith(tab.link);
  }

  back(): void {
    this.location.back();
  }

  about(): void {
    openAboutDialog(this.dialog);
  }
}
