import { Component, OnInit, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { PlatformService } from './core/platform.service';
import { IconFontService } from './shared/icon-font.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  template: `<router-outlet />`,
})
export class App implements OnInit {
  private readonly platform = inject(PlatformService);
  private readonly icons = inject(IconFontService);

  ngOnInit(): void {
    this.icons.start();
    void this.platform.initialize();
  }
}
