import { Component, DestroyRef, OnInit, effect, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatAutocompleteModule, MatAutocompleteSelectedEvent } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { Observable, catchError, debounceTime, distinctUntilChanged, of, startWith, switchMap } from 'rxjs';
import { DropdownItem } from '../core/models';

/**
 * Server-searched dropdown (autocomplete). Binds the selected uuid to `control`,
 * shows "Name (Code)" labels and emits the full selected item.
 */
@Component({
  selector: 'app-search-select',
  imports: [ReactiveFormsModule, MatAutocompleteModule, MatFormFieldModule, MatInputModule, MatIconModule, MatButtonModule],
  template: `
    <mat-form-field [appearance]="appearance()" class="full" [subscriptSizing]="subscript()">
      <mat-label>{{ label() }}</mat-label>
      <input matInput [formControl]="text" [matAutocomplete]="auto" (blur)="onBlur()" [attr.aria-label]="label()" />
      @if (control().value && !control().disabled) {
        <button matSuffix mat-icon-button type="button" aria-label="Clear" (click)="clear()"><mat-icon>close</mat-icon></button>
      }
      <mat-autocomplete #auto="matAutocomplete" (optionSelected)="select($event)" [displayWith]="display">
        @for (item of options(); track item.uuid) {
          <mat-option [value]="item">
            <span>{{ item.name }}</span> <span class="code">{{ item.code }}</span>
            @if (hint(); as h) { <span class="hint">{{ h(item) }}</span> }
          </mat-option>
        } @empty {
          <mat-option disabled>{{ loading() ? 'Searching…' : 'No matches' }}</mat-option>
        }
      </mat-autocomplete>
      @if (control().touched && control().invalid) {
        <mat-error>{{ errorText() }}</mat-error>
      }
    </mat-form-field>
  `,
  styles: `
    .full { width: 100%; }
    .code { color: var(--mat-sys-on-surface-variant); margin-left: 6px; font-size: 12px; }
    .hint { float: right; color: var(--mat-sys-on-surface-variant); font-size: 12px; margin-left: 12px; }
  `,
})
export class SearchSelect<T extends DropdownItem = DropdownItem> implements OnInit {
  readonly label = input.required<string>();
  readonly control = input.required<FormControl<string | null>>();
  readonly fetch = input.required<(term: string) => Observable<T[]>>();
  /** Label for an already-selected value (edit screens). */
  readonly initialLabel = input<string | null>(null);
  readonly errorText = input('This field is required.');
  readonly appearance = input<'fill' | 'outline'>('outline');
  readonly subscript = input<'fixed' | 'dynamic'>('fixed');
  readonly hint = input<((item: T) => string) | null>(null);
  readonly selected = output<T | null>();

  readonly text = new FormControl<string | T>('', { nonNullable: true });
  readonly options = signal<T[]>([]);
  readonly loading = signal(false);
  private selectedLabel = '';
  private readonly destroyRef = inject(DestroyRef);

  constructor() {
    effect(() => {
      const initial = this.initialLabel();
      if (initial && this.control().value && !this.selectedLabel) {
        this.selectedLabel = initial;
        this.text.setValue(initial, { emitEvent: false });
      }
    });
  }

  readonly display = (value: string | T | null): string =>
    value === null ? '' : typeof value === 'string' ? value : value.label;

  ngOnInit(): void {
    const ctrl = this.control();
    if (ctrl.disabled) this.text.disable({ emitEvent: false });
    ctrl.statusChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      if (ctrl.disabled) this.text.disable({ emitEvent: false });
      else this.text.enable({ emitEvent: false });
    });
    ctrl.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((v) => {
      if (!v) {
        this.selectedLabel = '';
        if (typeof this.text.value !== 'string' || this.text.value) this.text.setValue('', { emitEvent: false });
      }
    });

    this.text.valueChanges
      .pipe(
        startWith(''),
        debounceTime(250),
        distinctUntilChanged(),
        switchMap((value) => {
          if (typeof value !== 'string') return of(null);
          this.loading.set(true);
          return this.fetch()(value.trim()).pipe(catchError(() => of([] as T[])));
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((items) => {
        this.loading.set(false);
        if (items) this.options.set(items);
      });
  }

  select(event: MatAutocompleteSelectedEvent): void {
    const item = event.option.value as T;
    this.selectedLabel = item.label;
    this.control().setValue(item.uuid);
    this.control().markAsDirty();
    this.selected.emit(item);
  }

  clear(): void {
    this.selectedLabel = '';
    this.text.setValue('');
    this.control().setValue(null);
    this.control().markAsDirty();
    this.selected.emit(null);
  }

  onBlur(): void {
    this.control().markAsTouched();
    // Typed text that was not chosen from the list is discarded.
    const v = this.text.value;
    if (typeof v === 'string' && v !== this.selectedLabel) {
      this.text.setValue(this.control().value ? this.selectedLabel : '', { emitEvent: false });
    }
  }
}
