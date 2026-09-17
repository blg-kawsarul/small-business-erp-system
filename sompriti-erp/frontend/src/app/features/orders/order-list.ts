import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSortModule } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Observable, of } from 'rxjs';
import { ApiService, dateToIso } from '../../core/api.service';
import { AuthService } from '../../core/auth.service';
import { LayoutService } from '../../core/layout.service';
import { DropdownItem, OrderKind, OrderListItem, Paged, PostingStatus } from '../../core/models';
import { ListFooter } from '../../shared/list-footer';
import { ListState } from '../../shared/list-state';
import { LabelPipe, MoneyPipe } from '../../shared/pipes';
import { SearchSelect } from '../../shared/search-select';
import { StatusChip } from '../../shared/status-chip';
import { orderMeta } from './order-kind';

@Component({
  selector: 'app-order-list',
  imports: [
    RouterLink, DatePipe, ReactiveFormsModule, MatTableModule, MatSortModule, MatButtonModule, MatButtonToggleModule,
    MatIconModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatDatepickerModule, MatProgressBarModule,
    SearchSelect, StatusChip, ListFooter, MoneyPipe, LabelPipe,
  ],
  templateUrl: './order-list.html',
  styleUrl: './order-list.scss',
})
export class OrderListPage implements OnInit {
  readonly kind = input<OrderKind>('sales');
  readonly auth = inject(AuthService);
  readonly layout = inject(LayoutService);
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly dialog = inject(MatDialog);

  readonly meta = computed(() => orderMeta(this.kind()));
  readonly title = computed(() =>
    this.auth.isUser() ? (this.kind() === 'sales' ? 'My purchases' : 'My supplies') : this.meta().title,
  );
  readonly canCreate = computed(() => (this.kind() === 'purchase' ? this.auth.isAdmin() : this.auth.canSales()));
  readonly columns = computed(() =>
    this.auth.isUser()
      ? ['orderNumber', 'orderDate', 'company', 'paymentType', 'status', 'totalAmount', 'paid', 'due']
      : ['orderNumber', 'orderDate', 'partyName', 'company', 'paymentType', 'status', 'totalAmount', 'paid', 'due'],
  );

  readonly statuses: (PostingStatus | '')[] = ['', 'DRAFT', 'FINAL', 'VOID'];
  readonly status = signal<PostingStatus | ''>((this.route.snapshot.queryParamMap.get('status') as PostingStatus) ?? '');
  readonly party = new FormControl<string | null>(this.route.snapshot.queryParamMap.get('partyUuid'));
  readonly partyLabel = this.route.snapshot.queryParamMap.get('partyLabel');
  readonly company = new FormControl<string | null>(null);
  readonly from = new FormControl<Date | null>(null);
  readonly to = new FormControl<Date | null>(null);
  readonly companies = signal<DropdownItem[]>([]);

  /** How many extra filters are in use; shown as a badge on the mobile Filters button. */
  readonly filterCount = signal(0);

  readonly fetchParties = (term: string) =>
    this.auth.isUser() ? of([]) : this.api.get<DropdownItem[]>(`${this.meta().partyApi}/dropdown`, { search: term });

  readonly list = new ListState<OrderListItem>((q) =>
    this.api.get<Paged<OrderListItem>>(this.meta().api, {
      ...q,
      postingStatus: this.status() || null,
      partyUuid: this.party.value,
      companyUuid: this.company.value,
      fromDate: dateToIso(this.from.value),
      toDate: dateToIso(this.to.value),
    }),
  );

  ngOnInit(): void {
    this.refreshFilterCount();
    this.list.reload();
    if (!this.auth.isUser()) this.api.get<DropdownItem[]>('/companies/dropdown').subscribe({ next: (c) => this.companies.set(c), error: () => {} });
  }

  statusLabel(s: PostingStatus | ''): string {
    return s === '' ? 'All' : s.charAt(0) + s.slice(1).toLowerCase();
  }

  setStatus(value: PostingStatus | ''): void {
    this.status.set(value);
    this.list.resetToFirstPage();
  }

  applyFilters(): void {
    this.refreshFilterCount();
    this.list.resetToFirstPage();
  }

  clearFilters(): void {
    this.party.setValue(null);
    this.company.setValue(null);
    this.from.setValue(null);
    this.to.setValue(null);
    this.status.set('');
    this.applyFilters();
  }

  private refreshFilterCount(): void {
    this.filterCount.set([this.party.value, this.company.value, this.from.value, this.to.value].filter(Boolean).length);
  }

  /** Phone: filters live in a full-screen sheet so the list itself stays clean. */
  openFilters(): void {
    const ref = this.dialog.open(OrderFilterSheet, this.layout.dialog({
      partyLabelText: this.meta().partyLabel,
      party: this.party,
      partyInitialLabel: this.partyLabel,
      company: this.company,
      from: this.from,
      to: this.to,
      companies: this.companies(),
      fetchParties: this.fetchParties,
      showParty: !this.auth.isUser(),
    }, '480px'));
    ref.afterClosed().subscribe((changed) => {
      if (changed === 'clear') this.clearFilters();
      else if (changed) this.applyFilters();
    });
  }

  open(o: OrderListItem): void {
    void this.router.navigate([this.meta().route, o.uuid]);
  }
}

interface FilterSheetData {
  partyLabelText: string;
  party: FormControl<string | null>;
  partyInitialLabel: string | null;
  company: FormControl<string | null>;
  from: FormControl<Date | null>;
  to: FormControl<Date | null>;
  companies: DropdownItem[];
  fetchParties: (term: string) => Observable<DropdownItem[]>;
  showParty: boolean;
}

@Component({
  selector: 'app-order-filter-sheet',
  imports: [ReactiveFormsModule, MatDialogModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatDatepickerModule, SearchSelect],
  template: `
    <h2 mat-dialog-title>Filters</h2>
    <mat-dialog-content>
      @if (data.showParty) {
        <app-search-select [label]="data.partyLabelText" [control]="data.party" [fetch]="data.fetchParties" [initialLabel]="data.partyInitialLabel" />
      }
      <mat-form-field class="full-width">
        <mat-label>Company</mat-label>
        <mat-select [formControl]="data.company">
          <mat-option [value]="null">All companies</mat-option>
          @for (c of data.companies; track c.uuid) { <mat-option [value]="c.uuid">{{ c.name }}</mat-option> }
        </mat-select>
      </mat-form-field>
      <mat-form-field class="full-width">
        <mat-label>Order date</mat-label>
        <mat-date-range-input [rangePicker]="picker">
          <input matStartDate [formControl]="data.from" placeholder="From" />
          <input matEndDate [formControl]="data.to" placeholder="To" />
        </mat-date-range-input>
        <mat-datepicker-toggle matIconSuffix [for]="picker" />
        <mat-date-range-picker #picker />
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="ref.close('clear')">Clear all</button>
      <button mat-flat-button (click)="ref.close(true)">Apply</button>
    </mat-dialog-actions>
  `,
})
export class OrderFilterSheet {
  readonly ref = inject(MatDialogRef<OrderFilterSheet>);
  readonly data = inject<FilterSheetData>(MAT_DIALOG_DATA);
}
