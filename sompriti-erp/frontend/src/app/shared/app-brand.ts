import { Component } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { APP_INFO } from '../core/app-info';

/** The product lock-up (mark + name) shown on the sign-in screens. */
@Component({
  selector: 'app-brand',
  imports: [MatIconModule],
  template: `<div class="brand"><mat-icon>inventory_2</mat-icon> {{ app.name }}</div>`,
  styles: `.brand { line-height: 1.25; }`,
})
export class AppBrand {
  readonly app = APP_INFO;
}
