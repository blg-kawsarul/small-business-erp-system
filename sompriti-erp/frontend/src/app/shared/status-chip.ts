import { Component, computed, input } from '@angular/core';
import { enumLabel } from './pipes';

/** Small colored pill for statuses such as DRAFT / FINAL / VOID / SENT / FAILED. */
@Component({
  selector: 'app-status',
  template: `<span class="chip" [class]="'chip chip-' + tone()">{{ text() }}</span>`,
  styles: `
    .chip { display: inline-block; padding: 2px 10px; border-radius: 999px; font-size: 12px; font-weight: 600; line-height: 20px; white-space: nowrap; }
    .chip-neutral { background: var(--erp-chip-neutral-bg); color: var(--erp-chip-neutral-fg); }
    .chip-info { background: var(--erp-chip-info-bg); color: var(--erp-chip-info-fg); }
    .chip-success { background: var(--erp-chip-success-bg); color: var(--erp-chip-success-fg); }
    .chip-warn { background: var(--erp-chip-warn-bg); color: var(--erp-chip-warn-fg); }
    .chip-danger { background: var(--erp-chip-danger-bg); color: var(--erp-chip-danger-fg); }
  `,
})
export class StatusChip {
  readonly value = input.required<string>();
  readonly text = computed(() => enumLabel(this.value()));
  readonly tone = computed(() => {
    switch (this.value()) {
      case 'FINAL':
      case 'SENT':
      case 'ACTIVE':
      case 'INCREASE':
        return 'success';
      case 'DRAFT':
      case 'PENDING':
        return 'info';
      case 'VOID':
      case 'FAILED':
      case 'DELETED':
      case 'DECREASE':
        return 'danger';
      case 'SKIPPED':
      case 'MANAGER':
        return 'warn';
      default:
        return 'neutral';
    }
  });
}
