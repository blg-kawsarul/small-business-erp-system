import { AfterViewInit, Component, ElementRef, OnDestroy, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import type { Chart } from 'chart.js';
import { ApiService, errorMessage } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { Dashboard, DropdownItem, OrderListItem } from '../../core/models';
import { MoneyPipe, QtyPipe, formatMoney } from '../../shared/pipes';
import { StatusChip } from '../../shared/status-chip';

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, DatePipe, NgTemplateOutlet, ReactiveFormsModule, MatButtonModule, MatIconModule, MatProgressBarModule, MatFormFieldModule, MatSelectModule, MoneyPipe, QtyPipe, StatusChip],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class DashboardPage implements OnInit, AfterViewInit, OnDestroy {
  readonly auth = inject(AuthService);
  private readonly api = inject(ApiService);

  readonly data = signal<Dashboard | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly companies = signal<DropdownItem[]>([]);
  readonly company = new FormControl<string | null>(null);
  readonly chartCanvas = viewChild<ElementRef<HTMLCanvasElement>>('chart');
  private chart: Chart | null = null;
  private viewReady = false;

  readonly greeting = computed(() => {
    const h = new Date().getHours();
    return h < 12 ? 'Good morning' : h < 17 ? 'Good afternoon' : 'Good evening';
  });
  readonly unlinkedUser = computed(() => {
    const u = this.auth.user();
    return u?.role === 'USER' && !u.customerUuid && !u.supplierUuid;
  });

  ngOnInit(): void {
    this.load();
    if (!this.auth.isUser()) this.api.get<DropdownItem[]>('/companies/dropdown').subscribe({ next: (c) => this.companies.set(c), error: () => {} });
  }

  ngAfterViewInit(): void {
    this.viewReady = true;
    this.renderChart();
  }

  ngOnDestroy(): void {
    this.chart?.destroy();
  }

  load(): void {
    this.loading.set(true);
    this.api.get<Dashboard>('/dashboard', { companyUuid: this.company.value }).subscribe({
      next: (d) => {
        this.data.set(d);
        this.loading.set(false);
        this.error.set(null);
        setTimeout(() => this.renderChart());
      },
      error: (e) => {
        this.error.set(errorMessage(e));
        this.loading.set(false);
      },
    });
  }

  orderLink(o: OrderListItem): string[] {
    return [o.transactionType === 'SALES' ? '/sales-orders' : '/purchase-orders', o.uuid];
  }

  /** Sales for the last 30 days as a bar chart (chart.js is loaded on demand). */
  private async renderChart(): Promise<void> {
    const d = this.data();
    const canvas = this.chartCanvas()?.nativeElement;
    if (!this.viewReady || !d || !canvas || d.salesLast30Days.length === 0) return;

    const { Chart, BarController, BarElement, CategoryScale, LinearScale, Tooltip } = await import('chart.js');
    Chart.register(BarController, BarElement, CategoryScale, LinearScale, Tooltip);

    const labels = d.salesLast30Days.map((x) => new Date(x.date + 'T00:00:00').toLocaleDateString('en-GB', { day: '2-digit', month: 'short' }));
    const values = d.salesLast30Days.map((x) => x.total);
    // Resolve the theme color to rgb() (CSS variables may use light-dark(), which chart.js cannot parse).
    const probe = document.createElement('span');
    probe.style.color = 'var(--mat-sys-primary)';
    document.body.appendChild(probe);
    const primary = getComputedStyle(probe).color || '#005cbb';
    probe.remove();

    this.chart?.destroy();
    this.chart = new Chart(canvas, {
      type: 'bar',
      data: { labels, datasets: [{ label: 'Sales', data: values, backgroundColor: primary, borderRadius: 4, maxBarThickness: 18 }] },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: { tooltip: { callbacks: { label: (ctx) => formatMoney(ctx.parsed.y) } } },
        scales: {
          x: { grid: { display: false }, ticks: { maxRotation: 0, autoSkip: true, maxTicksLimit: 10 } },
          y: { beginAtZero: true, grid: { color: 'rgba(0,0,0,0.06)' }, ticks: { callback: (v) => Number(v).toLocaleString('en-US') } },
        },
      },
    });
  }
}
