import { Routes } from '@angular/router';
import { authGuard, guestGuard, roleGuard } from './core/guards';

export const routes: Routes = [
  // ---------------------------------------------------------------- public
  { path: 'login', canActivate: [guestGuard], title: 'Log in', loadComponent: () => import('./features/auth/login').then((m) => m.LoginPage) },
  { path: 'register', canActivate: [guestGuard], title: 'Create account', loadComponent: () => import('./features/auth/register').then((m) => m.RegisterPage) },
  { path: 'forgot-password', title: 'Forgot password', loadComponent: () => import('./features/auth/forgot-password').then((m) => m.ForgotPasswordPage) },
  { path: 'reset-password', title: 'Reset password', loadComponent: () => import('./features/auth/reset-password').then((m) => m.ResetPasswordPage) },

  // ---------------------------------------------------------------- app shell
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/shell').then((m) => m.Shell),
    children: [
      { path: '', pathMatch: 'full', title: 'Dashboard', loadComponent: () => import('./features/dashboard/dashboard').then((m) => m.DashboardPage) },
      { path: 'change-password', title: 'Change password', loadComponent: () => import('./features/auth/change-password').then((m) => m.ChangePasswordPage) },
      { path: 'more', title: 'Menu', loadComponent: () => import('./features/more/more').then((m) => m.MorePage) },
      { path: 'profile', title: 'My profile', loadComponent: () => import('./features/profile/profile').then((m) => m.ProfilePage) },

      // master data
      { path: 'companies', title: 'Companies', canActivate: [roleGuard], data: { roles: ['ADMIN'] }, loadComponent: () => import('./features/master/companies').then((m) => m.CompaniesPage) },
      { path: 'customers', title: 'Customers', canActivate: [roleGuard], data: { roles: ['ADMIN', 'MANAGER'], kind: 'customer' }, loadComponent: () => import('./features/master/parties').then((m) => m.PartiesPage) },
      { path: 'suppliers', title: 'Suppliers', canActivate: [roleGuard], data: { roles: ['ADMIN'], kind: 'supplier' }, loadComponent: () => import('./features/master/parties').then((m) => m.PartiesPage) },
      { path: 'products', title: 'Products', canActivate: [roleGuard], data: { roles: ['ADMIN', 'MANAGER'] }, loadComponent: () => import('./features/master/products').then((m) => m.ProductsPage) },

      // stock
      { path: 'stock', title: 'Stock balance', canActivate: [roleGuard], data: { roles: ['ADMIN', 'MANAGER'] }, loadComponent: () => import('./features/stock/stock-balances').then((m) => m.StockBalancesPage) },
      { path: 'stock/ledger', title: 'Stock ledger', canActivate: [roleGuard], data: { roles: ['ADMIN', 'MANAGER'] }, loadComponent: () => import('./features/stock/stock-ledger').then((m) => m.StockLedgerPage) },
      { path: 'stock/adjustments', title: 'Stock adjustments', canActivate: [roleGuard], data: { roles: ['ADMIN'] }, loadComponent: () => import('./features/stock/stock-adjustments').then((m) => m.StockAdjustmentsPage) },

      // orders (kind comes from route data)
      { path: 'purchase-orders', title: 'Purchase orders', canActivate: [roleGuard], data: { roles: ['ADMIN', 'USER'], kind: 'purchase' }, loadComponent: () => import('./features/orders/order-list').then((m) => m.OrderListPage) },
      { path: 'purchase-orders/new', title: 'New purchase order', canActivate: [roleGuard], data: { roles: ['ADMIN'], kind: 'purchase' }, loadComponent: () => import('./features/orders/order-editor').then((m) => m.OrderEditorPage) },
      { path: 'purchase-orders/:id', title: 'Purchase order', canActivate: [roleGuard], data: { roles: ['ADMIN', 'USER'], kind: 'purchase' }, loadComponent: () => import('./features/orders/order-view').then((m) => m.OrderViewPage) },
      { path: 'purchase-orders/:id/edit', title: 'Edit purchase order', canActivate: [roleGuard], data: { roles: ['ADMIN'], kind: 'purchase' }, loadComponent: () => import('./features/orders/order-editor').then((m) => m.OrderEditorPage) },
      { path: 'sales-orders', title: 'Sales orders', canActivate: [roleGuard], data: { roles: ['ADMIN', 'MANAGER', 'USER'], kind: 'sales' }, loadComponent: () => import('./features/orders/order-list').then((m) => m.OrderListPage) },
      { path: 'sales-orders/new', title: 'New sales order', canActivate: [roleGuard], data: { roles: ['ADMIN', 'MANAGER'], kind: 'sales' }, loadComponent: () => import('./features/orders/order-editor').then((m) => m.OrderEditorPage) },
      { path: 'sales-orders/:id', title: 'Sales order', canActivate: [roleGuard], data: { roles: ['ADMIN', 'MANAGER', 'USER'], kind: 'sales' }, loadComponent: () => import('./features/orders/order-view').then((m) => m.OrderViewPage) },
      { path: 'sales-orders/:id/edit', title: 'Edit sales order', canActivate: [roleGuard], data: { roles: ['ADMIN', 'MANAGER'], kind: 'sales' }, loadComponent: () => import('./features/orders/order-editor').then((m) => m.OrderEditorPage) },

      // reports
      { path: 'reports/customers', title: 'Customer report', data: { kind: 'customer' }, loadComponent: () => import('./features/reports/party-report').then((m) => m.PartyReportPage) },
      { path: 'reports/suppliers', title: 'Supplier report', canActivate: [roleGuard], data: { roles: ['ADMIN', 'USER'], kind: 'supplier' }, loadComponent: () => import('./features/reports/party-report').then((m) => m.PartyReportPage) },
      { path: 'reports/companies', title: 'Company report', canActivate: [roleGuard], data: { roles: ['ADMIN', 'MANAGER'] }, loadComponent: () => import('./features/reports/company-report').then((m) => m.CompanyReportPage) },
      { path: 'reports/dues', title: 'Due report', loadComponent: () => import('./features/reports/due-report').then((m) => m.DueReportPage) },

      // administration
      { path: 'users', title: 'Users', canActivate: [roleGuard], data: { roles: ['ADMIN'] }, loadComponent: () => import('./features/users/users').then((m) => m.UsersPage) },
      { path: 'sms', title: 'SMS log', canActivate: [roleGuard], data: { roles: ['ADMIN'] }, loadComponent: () => import('./features/sms/sms-log').then((m) => m.SmsLogPage) },
    ],
  },
  { path: '**', redirectTo: '' },
];
