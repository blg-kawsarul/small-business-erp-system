import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { APP_INFO, DEVELOPER_ROLE, copyrightLine } from '../core/app-info';

/**
 * The standard "About this app" screen: product identity, version and the developer credit.
 * Opened from the account menu on desktop and from the More tab on phones.
 */
@Component({
  selector: 'app-about-dialog',
  imports: [MatDialogModule, MatButtonModule, MatIconModule],
  template: `
    <mat-dialog-content class="about">
      <div class="identity">
        <div class="mark"><mat-icon>inventory_2</mat-icon></div>
        <h2>{{ app.name }}</h2>
        <p class="version">Version {{ app.version }}</p>
      </div>

      <div class="credit">
        <div class="label">Designed and developed by</div>
        <div class="who">{{ app.developer.name }}</div>
        <div class="role">{{ role }}</div>
      </div>

      <p class="copyright">{{ copyright }}</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close cdkFocusInitial>Close</button>
    </mat-dialog-actions>
  `,
  styles: `
    .about { padding-top: 12px; text-align: center; }
    .identity .mark {
      width: 60px; height: 60px; margin: 4px auto 10px; border-radius: 16px;
      display: flex; align-items: center; justify-content: center;
      background: var(--mat-sys-secondary-container);
    }
    .identity .mark mat-icon { font-size: 32px; width: 32px; height: 32px; color: var(--mat-sys-primary); }
    .identity h2 { margin: 0; font-size: 20px; font-weight: 650; }
    .version { margin: 2px 0 0; font-size: 13px; color: var(--erp-muted); }

    .credit { margin: 20px 0 0; padding: 16px 12px; border-radius: 12px; background: var(--erp-bg); }
    .credit .label { font-size: 11px; font-weight: 600; text-transform: uppercase; letter-spacing: .06em; color: var(--erp-faint); }
    .credit .who { margin-top: 6px; font-size: 16px; font-weight: 650; }
    .credit .role { margin-top: 2px; font-size: 13px; color: var(--erp-muted); }

    .copyright { margin: 14px 0 0; font-size: 11.5px; color: var(--erp-faint); }
  `,
})
export class AboutDialog {
  readonly app = APP_INFO;
  readonly role = DEVELOPER_ROLE;
  readonly copyright = copyrightLine();
}

/**
 * Opens the About dialog. Short informational dialogs stay a centred box on every screen
 * size - the full-screen sheet is reserved for forms, where the height is actually used.
 */
export function openAboutDialog(dialog: MatDialog): void {
  dialog.open(AboutDialog, { width: '360px', maxWidth: 'calc(100vw - 32px)', autoFocus: 'dialog' });
}
