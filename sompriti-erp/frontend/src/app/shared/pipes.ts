import { Pipe, PipeTransform } from '@angular/core';

const moneyFormat = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const qtyFormat = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });

export function formatMoney(value: number | null | undefined, symbol = true): string {
  if (value === null || value === undefined) return '-';
  return (symbol ? 'Tk ' : '') + moneyFormat.format(value);
}

/** 1234.5 -> "Tk 1,234.50" */
@Pipe({ name: 'money' })
export class MoneyPipe implements PipeTransform {
  transform(value: number | null | undefined, symbol = true): string {
    return formatMoney(value, symbol);
  }
}

@Pipe({ name: 'qty' })
export class QtyPipe implements PipeTransform {
  transform(value: number | null | undefined): string {
    return value === null || value === undefined ? '-' : qtyFormat.format(value);
  }
}

/** "MOBILE_BANKING" -> "Mobile Banking" */
@Pipe({ name: 'label' })
export class LabelPipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    return enumLabel(value);
  }
}

export function enumLabel(value: string | null | undefined): string {
  if (!value) return '';
  return value
    .toLowerCase()
    .split('_')
    .map((w) => w.charAt(0).toUpperCase() + w.slice(1))
    .join(' ');
}
