import { OrderKind } from '../../core/models';

export interface OrderKindMeta {
  kind: OrderKind;
  title: string;
  singular: string;
  api: string;
  route: string;
  partyLabel: string;
  partyApi: string;
  priceField: 'purchasePrice' | 'salesPrice';
}

export function orderMeta(kind: OrderKind): OrderKindMeta {
  return kind === 'purchase'
    ? { kind, title: 'Purchase orders', singular: 'Purchase order', api: '/purchase-orders', route: '/purchase-orders', partyLabel: 'Supplier', partyApi: '/suppliers', priceField: 'purchasePrice' }
    : { kind, title: 'Sales orders', singular: 'Sales order', api: '/sales-orders', route: '/sales-orders', partyLabel: 'Customer', partyApi: '/customers', priceField: 'salesPrice' };
}
