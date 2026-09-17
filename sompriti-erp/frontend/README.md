# Sompriti ERP – Web (Angular)

Angular 22 + Angular Material single-page app. See the root `README.md` for the full setup.

```bash
npm install
npm start          # http://localhost:4200 (proxies /api to http://localhost:5080)
npm run build      # production build in dist/frontend (copied into the API's wwwroot by the Dockerfile)
```

Structure:

- `src/app/core` – API client, auth service (JWT + refresh), interceptor, guards, models
- `src/app/shared` – reusable pieces (search select, confirm dialog, list state, pipes, form error helpers)
- `src/app/layout` – application shell with role-based navigation
- `src/app/features` – screens: auth, dashboard, master data, stock, orders, reports, users, SMS log
