import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ApiService } from '../../core/api.service';
import { LayoutService } from '../../core/layout.service';
import { Paged, SmsLog, SmsStatus } from '../../core/models';
import { NotifyService } from '../../core/notify.service';
import { ListFooter } from '../../shared/list-footer';
import { ListState } from '../../shared/list-state';
import { StatusChip } from '../../shared/status-chip';

@Component({
  selector: 'app-sms-log',
  imports: [DatePipe, MatTableModule, MatButtonModule, MatButtonToggleModule, MatIconModule, MatFormFieldModule, MatInputModule, MatProgressBarModule, MatTooltipModule, ListFooter, StatusChip],
  template: `
    <div class="page">
      <div class="page-header">
        <div>
          <h1>SMS log</h1>
          <div class="subtitle">Payment notifications sent to Bangladesh mobile numbers. Failed messages retry automatically up to 5 times.</div>
        </div>
        <div class="actions"><button mat-stroked-button (click)="list.reload()"><mat-icon>refresh</mat-icon>Refresh</button></div>
      </div>
      <div class="card">
        <div class="toolbar">
          <mat-button-toggle-group [value]="status()" (change)="status.set($event.value); list.resetToFirstPage()" hideSingleSelectionIndicator>
            <mat-button-toggle value="">All</mat-button-toggle>
            <mat-button-toggle value="PENDING">Pending</mat-button-toggle>
            <mat-button-toggle value="SENT">Sent</mat-button-toggle>
            <mat-button-toggle value="FAILED">Failed</mat-button-toggle>
            <mat-button-toggle value="SKIPPED">Skipped</mat-button-toggle>
          </mat-button-toggle-group>
          <mat-form-field class="search" subscriptSizing="dynamic">
            <mat-icon matPrefix>search</mat-icon>
            <mat-label>Mobile number or text</mat-label>
            <input matInput (input)="list.search($any($event.target).value)" />
          </mat-form-field>
        </div>
        @if (list.loading()) { <mat-progress-bar mode="indeterminate" /> }

        @if (layout.isHandset()) {
          <div class="m-list">
            @for (s of list.items(); track s.uuid) {
              <div class="m-card">
                <div class="m-card-head">
                  <div>
                    <div class="m-title">{{ s.recipientNumber }}</div>
                    <div class="m-sub">{{ s.createdDate | date: 'dd MMM yyyy, h:mm a' }} · {{ s.attemptCount }} tries</div>
                  </div>
                  <div class="m-right">
                    <app-status [value]="s.status" />
                    @if (s.status === 'FAILED' || s.status === 'SKIPPED') {
                      <button mat-icon-button matTooltip="Retry" (click)="retry(s)"><mat-icon>replay</mat-icon></button>
                    }
                  </div>
                </div>
                <div class="message">{{ s.message }}</div>
                @if (s.lastError) { <div class="negative small">{{ s.lastError }}</div> }
              </div>
            }
          </div>
        } @else {
        <div class="table-wrap">

          <table mat-table [dataSource]="list.items()">
            <ng-container matColumnDef="created"><th mat-header-cell *matHeaderCellDef>Queued</th><td mat-cell *matCellDef="let s" class="nowrap">{{ s.createdDate | date: 'dd MMM, h:mm a' }}</td></ng-container>
            <ng-container matColumnDef="to"><th mat-header-cell *matHeaderCellDef>To</th><td mat-cell *matCellDef="let s" class="nowrap">{{ s.recipientNumber }}</td></ng-container>
            <ng-container matColumnDef="message"><th mat-header-cell *matHeaderCellDef>Message</th>
              <td mat-cell *matCellDef="let s">{{ s.message }}@if (s.lastError) { <div class="negative small">{{ s.lastError }}</div> }</td></ng-container>
            <ng-container matColumnDef="status"><th mat-header-cell *matHeaderCellDef>Status</th><td mat-cell *matCellDef="let s"><app-status [value]="s.status" /></td></ng-container>
            <ng-container matColumnDef="attempts"><th mat-header-cell *matHeaderCellDef class="num">Tries</th><td mat-cell *matCellDef="let s" class="num">{{ s.attemptCount }}</td></ng-container>
            <ng-container matColumnDef="sent"><th mat-header-cell *matHeaderCellDef>Sent / next try</th>
              <td mat-cell *matCellDef="let s" class="nowrap muted">{{ s.sentDate ? (s.sentDate | date: 'dd MMM, h:mm a') : (s.nextAttemptDate ? 'next ' + (s.nextAttemptDate | date: 'h:mm a') : '') }}</td></ng-container>
            <ng-container matColumnDef="actions"><th mat-header-cell *matHeaderCellDef></th>
              <td mat-cell *matCellDef="let s" class="num">
                @if (s.status === 'FAILED' || s.status === 'SKIPPED') { <button mat-icon-button matTooltip="Retry" (click)="retry(s)"><mat-icon>replay</mat-icon></button> }
              </td></ng-container>
            <tr mat-header-row *matHeaderRowDef="columns"></tr>
            <tr mat-row *matRowDef="let row; columns: columns"></tr>
          </table>
        </div>
        }

        @if (!list.loading() && list.items().length === 0) { <div class="empty">{{ list.error() ?? 'No SMS messages.' }}</div> }
        <app-list-footer [list]="list" />
      </div>
    </div>
  `,
  styles: `
    .small { font-size: 12px; }
    .message { margin-top: 10px; font-size: 13px; line-height: 1.45; color: var(--erp-muted); }
  `,
})
export class SmsLogPage implements OnInit {
  readonly layout = inject(LayoutService);
  private readonly api = inject(ApiService);
  private readonly notify = inject(NotifyService);
  readonly status = signal<SmsStatus | ''>('');
  readonly columns = ['created', 'to', 'message', 'status', 'attempts', 'sent', 'actions'];
  readonly list = new ListState<SmsLog>((q) => this.api.get<Paged<SmsLog>>('/sms', { ...q, status: this.status() || null }), 50);

  ngOnInit(): void {
    this.list.reload();
  }

  retry(s: SmsLog): void {
    this.api.post(`/sms/${s.uuid}/retry`).subscribe({
      next: () => { this.notify.success('Queued for retry.'); this.list.reload(); },
      error: (e) => this.notify.error(e),
    });
  }
}
