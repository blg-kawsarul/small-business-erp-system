import { BreakpointObserver } from '@angular/cdk/layout';
import { Injectable, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatDialogConfig } from '@angular/material/dialog';
import { map } from 'rxjs';

/** Phones and small tablets use the mobile layout (cards, bottom navigation, full-screen dialogs). */
export const HANDSET_QUERY = '(max-width: 840px)';

@Injectable({ providedIn: 'root' })
export class LayoutService {
  private readonly breakpoints = inject(BreakpointObserver);

  readonly isHandset = toSignal(this.breakpoints.observe(HANDSET_QUERY).pipe(map((r) => r.matches)), { initialValue: matchesNow() });
  readonly isDesktop = computed(() => !this.isHandset());

  /** Dialogs cover the whole screen on phones and are centred boxes on desktop. */
  dialog<T>(data?: T, desktopWidth = '640px'): MatDialogConfig<T> {
    return this.isHandset()
      ? { data, width: '100vw', maxWidth: '100vw', height: '100%', maxHeight: '100%', panelClass: 'sheet-dialog', autoFocus: 'dialog' }
      : { data, width: desktopWidth, autoFocus: 'first-tabbable' };
  }
}

function matchesNow(): boolean {
  return typeof window !== 'undefined' && window.matchMedia(HANDSET_QUERY).matches;
}
