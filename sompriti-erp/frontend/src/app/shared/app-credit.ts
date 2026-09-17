import { Component } from '@angular/core';
import { APP_INFO, DEVELOPER_ROLE, copyrightLine } from '../core/app-info';

/** One-line developer credit for pages outside the shell, such as the login screen. */
@Component({
  selector: 'app-credit',
  template: `
    <footer class="credit">
      <div>{{ app.name }} <span class="ver">v{{ app.version }}</span></div>
      <div>Developed by <strong>{{ app.developer.name }}</strong></div>
      <div class="role">{{ role }}</div>
      <div class="role">{{ copyright }}</div>
    </footer>
  `,
  styles: `
    .credit {
      margin: 20px auto 0;
      padding-bottom: env(safe-area-inset-bottom, 0px);
      text-align: center;
      font-size: 12px;
      line-height: 1.6;
      color: var(--erp-muted);
    }
    .ver, .role { color: var(--erp-faint); }
    .role { font-size: 11.5px; }
  `,
})
export class AppCredit {
  readonly app = APP_INFO;
  readonly role = DEVELOPER_ROLE;
  readonly copyright = copyrightLine();
}
