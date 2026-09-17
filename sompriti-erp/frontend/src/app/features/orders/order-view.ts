import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { ApiService, dateToIso, isoToDate, problemOf } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { OrderDetail, OrderKind, PaymentMethod } from '../../core/models';
import { NotifyService } from '../../core/notify.service';
import { applyServerErrors, controlError } from '../../shared/form-errors';
import { LabelPipe, MoneyPipe, QtyPipe, formatMoney } from '../../shared/pipes';
import { StatusChip } from '../../shared/status-chip';
import { orderMeta } from './order-kind';

@Component({
  selector: 'app-order-view',
  imports: [RouterLink, DatePipe, MatTableModule, MatButtonModule, MatIconModule, MatMenuModule, MatProgressBarModule, MatTooltipModule, StatusChip, MoneyPipe, QtyPipe, LabelPipe],
  templateUrl: './order-view.html',
  styleUrl: './order-view.scss',
})
export class OrderViewPage implements OnInit {
  readonly kind = input<OrderKind>('sales');
  readonly id = input.required<string>();

  readonly auth = inject(AuthService);
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);
  private readonly notify = inject(NotifyService);

  readonly meta = computed(() => orderMeta(this.kind()));
  readonly order = signal<OrderDetail | null>(null);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  readonly lineColumns = ['no', 'product', 'type', 'boxes', 'pcs', 'perBox', 'perPcs', 'total'];
  readonly paymentColumns = ['date', 'method', 'note', 'by', 'amount', 'actions'];

  /** Write permissions per SRS 5.1. */
  readonly canWrite = computed(() => (this.kind() === 'purchase' ? this.auth.isAdmin() : this.auth.canSales()));
  readonly canVoid = computed(() => this.auth.isAdmin());
  readonly isDraft = computed(() => this.order()?.postingStatus === 'DRAFT');
  readonly isFinal = computed(() => this.order()?.postingStatus === 'FINAL');

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.get<OrderDetail>(`${this.meta().api}/${this.id()}`).subscribe({
      next: (o) => { this.order.set(o); this.loading.set(false); },
      error: (e) => { this.error.set(problemOf(e).title ?? 'Could not load the order.'); this.loading.set(false); },
    });
  }

  finalize(): void {
    const o = this.order()!;
    const effect = this.kind() === 'sales' ? 'deducted from' : 'added to';
    this.notify.confirm({
      title: `Finalize #${o.orderNumber}?`,
      message: `Stock for ${o.lines.length} line item(s) will be ${effect} inventory.\nAfter finalizing, the order and its lines can no longer be edited. Payments can still be added.`,
      confirmText: 'Finalize',
    }).subscribe((ok) => {
      if (!ok) return;
      this.run(this.api.post<OrderDetail>(`${this.meta().api}/${o.uuid}/finalize`, { revision: o.revision }), `Order #${o.orderNumber} finalized.`);
    });
  }

  voidOrder(): void {
    const o = this.order()!;
    this.dialog.open(VoidDialog, { data: { order: o, kind: this.kind() }, width: '480px' }).afterClosed().subscribe((reason?: string) => {
      if (!reason) return;
      this.run(this.api.post<OrderDetail>(`${this.meta().api}/${o.uuid}/void`, { revision: o.revision, voidReason: reason }), `Order #${o.orderNumber} voided.`);
    });
  }

  deleteDraft(): void {
    const o = this.order()!;
    this.notify.confirm({ title: 'Delete draft', message: `Delete draft order #${o.orderNumber}?`, confirmText: 'Delete', danger: true }).subscribe((ok) => {
      if (!ok) return;
      this.busy.set(true);
      this.api.delete(`${this.meta().api}/${o.uuid}`, { revision: o.revision }).subscribe({
        next: () => { this.notify.success('Draft deleted.'); void this.router.navigate([this.meta().route]); },
        error: (e) => { this.notify.error(e); this.busy.set(false); this.reloadOnConflict(e); },
      });
    });
  }

  addPayment(): void {
    const o = this.order()!;
    this.dialog.open(PaymentDialog, { data: { order: o, api: this.meta().api }, width: '520px' }).afterClosed().subscribe((updated?: OrderDetail) => {
      if (updated) this.order.set(updated);
    });
  }

  deletePayment(paymentUuid: string): void {
    const o = this.order()!;
    const p = o.payments.find((x) => x.uuid === paymentUuid)!;
    this.notify.confirm({
      title: 'Remove payment',
      message: `Remove the payment of ${formatMoney(p.paymentAmount)} on ${p.paymentDate}? It stays in the audit history as deleted.`,
      confirmText: 'Remove',
      danger: true,
    }).subscribe((ok) => {
      if (!ok) return;
      this.run(this.api.delete<OrderDetail>(`${this.meta().api}/${o.uuid}/payments/${paymentUuid}`, { revision: o.revision }), 'Payment removed.');
    });
  }

  pdf(download: boolean): void {
    const o = this.order()!;
    this.busy.set(true);
    this.api.blob(`${this.meta().api}/${o.uuid}/pdf`).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        if (download) {
          const a = document.createElement('a');
          a.href = url;
          a.download = `${this.kind() === 'sales' ? 'sales-invoice' : 'purchase-order'}-${o.orderNumber}.pdf`;
          a.click();
        } else {
          window.open(url, '_blank', 'noopener');
        }
        setTimeout(() => URL.revokeObjectURL(url), 60_000);
        this.busy.set(false);
      },
      error: (e) => { this.notify.error(e); this.busy.set(false); },
    });
  }

  private run(req: Observable<OrderDetail>, message: string): void {
    this.busy.set(true);
    req.subscribe({
      next: (updated) => {
        this.order.set(updated);
        this.notify.success(message);
        this.busy.set(false);
      },
      error: (e: unknown) => {
        this.notify.error(e);
        this.busy.set(false);
        this.reloadOnConflict(e);
      },
    });
  }

  private reloadOnConflict(e: unknown): void {
    if (problemOf(e).code === 'REVISION_CONFLICT') this.load();
  }
}

// ====================================================================== dialogs

@Component({
  selector: 'app-payment-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatDatepickerModule, MatButtonModule, MoneyPipe],
  template: `
    <h2 mat-dialog-title>Add payment · #{{ data.order.orderNumber }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content>
        <p class="muted" style="margin-top: 0">
          Total {{ data.order.totalAmount | money }} · Paid {{ data.order.totalPaidAmount | money }} ·
          <strong class="negative">Due {{ data.order.dueAmount | money }}</strong>
        </p>
        <div class="form-grid">
          <mat-form-field>
            <mat-label>Amount</mat-label>
            <span matTextPrefix>Tk&nbsp;</span>
            <input matInput type="number" min="0.01" step="0.01" [max]="data.order.dueAmount" formControlName="paymentAmount" cdkFocusInitial />
            <mat-error>{{ err('paymentAmount', 'Amount') }}</mat-error>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Method</mat-label>
            <mat-select formControlName="paymentMethod">
              @for (m of methods; track m.value) { <mat-option [value]="m.value">{{ m.label }}</mat-option> }
            </mat-select>
          </mat-form-field>
          <mat-form-field>
            <mat-label>Payment date</mat-label>
            <input matInput [matDatepicker]="dp" formControlName="paymentDate" [min]="minDate" [max]="today" />
            <mat-datepicker-toggle matIconSuffix [for]="dp" /><mat-datepicker #dp />
            <mat-error>{{ err('paymentDate', 'Payment date') }}</mat-error>
          </mat-form-field>
          <div style="display: flex; align-items: center;">
            <button mat-button type="button" (click)="form.controls.paymentAmount.setValue(data.order.dueAmount)">Pay full due</button>
          </div>
          <mat-form-field class="span-2"><mat-label>Note</mat-label><input matInput formControlName="paymentNote" placeholder="e.g. bKash TrxID" /></mat-form-field>
        </div>
        <p class="muted" style="margin: 0">An SMS with the amount and remaining due will be sent to {{ data.order.party.mobileNumber }}.</p>
        @if (error()) { <p class="negative">{{ error() }}</p> }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="busy()">Add payment</button>
      </mat-dialog-actions>
    </form>
  `,
})
export class PaymentDialog {
  readonly data = inject<{ order: OrderDetail; api: string }>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<PaymentDialog>);
  private readonly api = inject(ApiService);
  private readonly notify = inject(NotifyService);
  readonly today = new Date();
  readonly minDate = isoToDate(this.data.order.orderDate);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly methods: { value: PaymentMethod; label: string }[] = [
    { value: 'CASH', label: 'Cash' },
    { value: 'MOBILE_BANKING', label: 'Mobile banking (bKash, Nagad...)' },
    { value: 'BANK', label: 'Bank transfer' },
    { value: 'CHEQUE', label: 'Cheque' },
    { value: 'OTHER', label: 'Other' },
  ];
  readonly form = inject(FormBuilder).group({
    paymentAmount: [null as number | null, [Validators.required, Validators.min(0.01), Validators.max(this.data.order.dueAmount)]],
    paymentMethod: ['CASH' as PaymentMethod, Validators.required],
    paymentDate: [new Date() as Date | null, Validators.required],
    paymentNote: [''],
  });

  err(name: string, label: string): string {
    return controlError(this.form.get(name), label);
  }

  save(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    const v = this.form.getRawValue();
    this.busy.set(true);
    this.api.post<OrderDetail>(`${this.data.api}/${this.data.order.uuid}/payments`, {
      revision: this.data.order.revision,
      paymentAmount: v.paymentAmount,
      paymentMethod: v.paymentMethod,
      paymentDate: dateToIso(v.paymentDate),
      paymentNote: v.paymentNote,
    }).subscribe({
      next: (updated) => { this.notify.success('Payment added.'); this.ref.close(updated); },
      error: (e) => { this.error.set(applyServerErrors(this.form, e)); this.busy.set(false); },
    });
  }
}

@Component({
  selector: 'app-void-dialog',
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Void #{{ data.order.orderNumber }}</h2>
    <mat-dialog-content>
      @if (data.order.postingStatus === 'FINAL') {
        @if (data.order.payments.length > 0) {
          <p class="negative">This order has {{ data.order.payments.length }} payment(s). Remove all payments before voiding.</p>
        } @else {
          <p>Voiding reverses the stock movement: stock will be {{ data.kind === 'sales' ? 'added back' : 'deducted' }}. This cannot be undone.</p>
        }
      } @else {
        <p>This draft will be marked VOID. Stock is not affected. This cannot be undone.</p>
      }
      <mat-form-field class="full-width">
        <mat-label>Reason</mat-label>
        <textarea matInput rows="3" [formControl]="reason" cdkFocusInitial></textarea>
        <mat-error>A reason is required.</mat-error>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button mat-flat-button class="danger" [disabled]="data.order.postingStatus === 'FINAL' && data.order.payments.length > 0" (click)="confirm()">Void order</button>
    </mat-dialog-actions>
  `,
})
export class VoidDialog {
  readonly data = inject<{ order: OrderDetail; kind: OrderKind }>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<VoidDialog>);
  readonly reason = inject(FormBuilder).control('', [Validators.required, Validators.maxLength(500)]);

  confirm(): void {
    if (this.reason.invalid || !this.reason.value?.trim()) { this.reason.markAsTouched(); return; }
    this.ref.close(this.reason.value.trim());
  }
}
