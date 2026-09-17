import { Injectable, signal } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterStateSnapshot, TitleStrategy } from '@angular/router';
import { APP_INFO } from './app-info';

/**
 * Keeps the browser title in sync and exposes the current page title as a signal
 * so the mobile top bar can show it.
 */
@Injectable({ providedIn: 'root' })
export class AppTitleStrategy extends TitleStrategy {
  private static readonly AppName = APP_INFO.name;
  readonly pageTitle = signal<string>(AppTitleStrategy.AppName);

  constructor(private readonly title: Title) {
    super();
  }

  override updateTitle(snapshot: RouterStateSnapshot): void {
    const title = this.buildTitle(snapshot);
    this.pageTitle.set(title ?? AppTitleStrategy.AppName);
    this.title.setTitle(title ? `${title} · ${AppTitleStrategy.AppName}` : AppTitleStrategy.AppName);
  }
}
