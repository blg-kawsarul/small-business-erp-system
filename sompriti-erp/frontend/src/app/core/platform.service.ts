import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { App as CapacitorApp } from '@capacitor/app';
import { Capacitor } from '@capacitor/core';
import { Directory, Filesystem } from '@capacitor/filesystem';
import { Keyboard, KeyboardResize } from '@capacitor/keyboard';
import { Share } from '@capacitor/share';
import { SplashScreen } from '@capacitor/splash-screen';
import { Style, StatusBar } from '@capacitor/status-bar';

/**
 * Everything that behaves differently inside the Android app (Capacitor) compared to the browser:
 * status bar, splash screen, hardware back button and opening generated PDFs.
 */
@Injectable({ providedIn: 'root' })
export class PlatformService {
  private readonly router = inject(Router);

  readonly isNative = Capacitor.isNativePlatform();

  /** Called once at start-up from the root component. */
  async initialize(): Promise<void> {
    if (!this.isNative) return;
    try {
      await StatusBar.setStyle({ style: Style.Light });
      await StatusBar.setBackgroundColor({ color: '#ffffff' });
      Keyboard.setResizeMode({ mode: KeyboardResize.Native }).catch(() => undefined);
      await SplashScreen.hide();
      CapacitorApp.addListener('backButton', ({ canGoBack }) => {
        // Leave the app only from a top-level screen.
        if (canGoBack && !isRootRoute(this.router.url)) history.back();
        else void CapacitorApp.exitApp();
      });
    } catch {
      /* the app still works if a plugin is unavailable */
    }
  }

  /**
   * Opens a generated PDF. In the browser this is a new tab or a download;
   * in the Android app the file is written to the cache and handed to the system share/open sheet.
   */
  async openPdf(blob: Blob, fileName: string, download: boolean): Promise<void> {
    if (!this.isNative) {
      const url = URL.createObjectURL(blob);
      if (download) {
        const a = document.createElement('a');
        a.href = url;
        a.download = fileName;
        a.click();
      } else {
        window.open(url, '_blank', 'noopener');
      }
      setTimeout(() => URL.revokeObjectURL(url), 60_000);
      return;
    }

    const base64 = await blobToBase64(blob);
    const written = await Filesystem.writeFile({ path: fileName, data: base64, directory: Directory.Cache });
    await Share.share({ title: fileName, url: written.uri, dialogTitle: 'Open or share the PDF' });
  }
}

function isRootRoute(url: string): boolean {
  const path = url.split('?')[0];
  return ['/', '/sales-orders', '/purchase-orders', '/stock', '/more'].includes(path);
}

function blobToBase64(blob: Blob): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onerror = () => reject(reader.error);
    reader.onload = () => resolve(String(reader.result).split(',')[1] ?? '');
    reader.readAsDataURL(blob);
  });
}
