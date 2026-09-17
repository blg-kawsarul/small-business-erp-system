import { CurrentUser, Role } from '../core/models';

export interface NavItem {
  label: string;
  /** Short label for the bottom tab bar. */
  short?: string;
  icon: string;
  link: string;
  roles: Role[];
  exact?: boolean;
  /** Extra condition, e.g. a USER must be linked to a buyer. */
  show?: (user: CurrentUser) => boolean;
}

export interface NavGroup {
  title: string;
  items: NavItem[];
}

const linkedBuyer = (u: CurrentUser) => !!u.customerUuid;
const linkedSupplier = (u: CurrentUser) => !!u.supplierUuid;
const staffOrLinkedBuyer = (u: CurrentUser) => u.role !== 'USER' || !!u.customerUuid;
const staffOrLinkedSupplier = (u: CurrentUser) => u.role !== 'USER' || !!u.supplierUuid;

export const NAV_GROUPS: NavGroup[] = [
  {
    title: 'Overview',
    items: [{ label: 'Dashboard', short: 'Home', icon: 'dashboard', link: '/', roles: ['ADMIN', 'MANAGER', 'USER'], exact: true }],
  },
  {
    title: 'Transactions',
    items: [
      { label: 'Sales orders', short: 'Sales', icon: 'point_of_sale', link: '/sales-orders', roles: ['ADMIN', 'MANAGER'] },
      { label: 'Purchase orders', short: 'Purchases', icon: 'shopping_cart', link: '/purchase-orders', roles: ['ADMIN'] },
      { label: 'My purchases', short: 'Orders', icon: 'receipt_long', link: '/sales-orders', roles: ['USER'], show: linkedBuyer },
      { label: 'My supplies', short: 'Supplies', icon: 'local_shipping', link: '/purchase-orders', roles: ['USER'], show: linkedSupplier },
    ],
  },
  {
    title: 'Inventory',
    items: [
      { label: 'Stock balance', short: 'Stock', icon: 'inventory', link: '/stock', roles: ['ADMIN', 'MANAGER'], exact: true },
      { label: 'Products', short: 'Products', icon: 'category', link: '/products', roles: ['ADMIN', 'MANAGER'] },
      { label: 'Stock ledger', icon: 'receipt_long', link: '/stock/ledger', roles: ['ADMIN', 'MANAGER'] },
      { label: 'Stock adjustments', icon: 'tune', link: '/stock/adjustments', roles: ['ADMIN'] },
    ],
  },
  {
    title: 'Master data',
    items: [
      { label: 'Customers', short: 'Customers', icon: 'groups', link: '/customers', roles: ['ADMIN', 'MANAGER'] },
      { label: 'Suppliers', icon: 'local_shipping', link: '/suppliers', roles: ['ADMIN'] },
      { label: 'Companies', icon: 'apartment', link: '/companies', roles: ['ADMIN'] },
    ],
  },
  {
    title: 'Reports',
    items: [
      { label: 'Due report', short: 'Dues', icon: 'pending_actions', link: '/reports/dues', roles: ['ADMIN', 'MANAGER', 'USER'], show: (u) => staffOrLinkedBuyer(u) || staffOrLinkedSupplier(u) },
      { label: 'Customer report', icon: 'person_search', link: '/reports/customers', roles: ['ADMIN', 'MANAGER', 'USER'], show: staffOrLinkedBuyer },
      { label: 'Supplier report', icon: 'contact_page', link: '/reports/suppliers', roles: ['ADMIN', 'USER'], show: staffOrLinkedSupplier },
      { label: 'Company report', icon: 'domain', link: '/reports/companies', roles: ['ADMIN', 'MANAGER'] },
    ],
  },
  {
    title: 'Administration',
    items: [
      { label: 'Users', icon: 'manage_accounts', link: '/users', roles: ['ADMIN'] },
      { label: 'SMS log', icon: 'sms', link: '/sms', roles: ['ADMIN'] },
    ],
  },
];

export function visibleGroups(groups: NavGroup[], user: CurrentUser | null): NavGroup[] {
  if (!user) return [];
  return groups
    .map((g) => ({ ...g, items: g.items.filter((i) => i.roles.includes(user.role) && (i.show?.(user) ?? true)) }))
    .filter((g) => g.items.length > 0);
}

/** Up to four destinations for the phone tab bar; "More" is added by the shell. */
export function bottomTabs(user: CurrentUser | null): NavItem[] {
  if (!user) return [];
  const all = visibleGroups(NAV_GROUPS, user).flatMap((g) => g.items);
  const pick = (link: string) => all.find((i) => i.link === link);
  const wanted =
    user.role === 'ADMIN' ? ['/', '/sales-orders', '/purchase-orders', '/stock']
      : user.role === 'MANAGER' ? ['/', '/sales-orders', '/customers', '/stock']
        : ['/', '/sales-orders', '/purchase-orders', '/reports/dues'];
  const tabs = wanted.map(pick).filter((i): i is NavItem => !!i).slice(0, 4);
  return [...tabs, { label: 'More', short: 'More', icon: 'apps', link: '/more', roles: ['ADMIN', 'MANAGER', 'USER'], exact: true }];
}
