# Enterprise Resource Planning

Small-business ERP for purchase, sales, stock and payments (Bangladesh), built from `ERP_SRS_DRAFT.md` (v0.2).

| Layer | Technology |
|---|---|
| API | ASP.NET Core Web API, .NET 10, C# |
| Data | PostgreSQL + EF Core (Npgsql); schema created by versioned SQL scripts |
| Web | Angular 22 + Angular Material (responsive: tables on desktop, cards and bottom tabs on phones) |
| Android app | Capacitor 8 (the same Angular app packaged as an APK) |
| Hosting | Railway (one Docker service for API + web, one PostgreSQL service) |

## Features

- Login, self-registration (role USER), refresh tokens, lockout after 5 failed logins, password reset by email, forced password change for new/seeded users
- Roles ADMIN / MANAGER / USER enforced on the server; USER sees only orders and reports of the linked buyer (customer) and/or supplier
- Companies, customers, suppliers (auto codes from 100001), products (prices per pcs, PCS/BOX)
- Purchase and sales orders: DRAFT → FINAL → VOID, line calculations for boxes/pcs, stock checks, row-locked atomic finalize/void, revision-based concurrency (HTTP 409)
- Stock balance, stock ledger, stock adjustments (opening stock, damage, loss, correction)
- Payments on FINAL orders with overpayment protection; SMS to Bangladesh numbers via an outbox + retry worker
- PDF sales invoice / purchase order (DRAFT/VOID watermark)
- Dashboard, customer / supplier / company / due reports
- Soft delete everywhere, full audit columns

## Android app (APK)

The Angular app is packaged for Android with [Capacitor](https://capacitorjs.com). The APK contains the web app and
talks to the same API over the network, so the server must be reachable from the phone (deploy it to Railway first,
or use your computer's LAN address while testing).

**One-time setup**

1. Install [Android Studio](https://developer.android.com/studio) (it brings the Android SDK) and a JDK 21
   (`brew install --cask temurin@21` on macOS). Open Android Studio once and let it finish installing the SDK.
2. Point the build at your API: edit `frontend/src/environments/environment.mobile.ts` and set
   `apiBaseUrl` to your server, e.g. `https://sompriti-erp.up.railway.app`.
3. Allow the app's origin on the API: set `Cors__AllowedOrigins=https://localhost,capacitor://localhost,http://localhost`
   (Railway → Variables, or `appsettings.Development.json` locally). Without this the app cannot log in.
4. Create the Android project once:

   ```bash
   cd frontend
   npm install
   npm run build:mobile
   npx cap add android
   npx @capacitor/assets generate --android   # app icon and splash screen from resources/
   ```

**Build the APK**

```bash
cd frontend
npm run android:apk       # build + sync + ./gradlew assembleDebug
# APK: frontend/android/app/build/outputs/apk/debug/app-debug.apk
```

Copy that file to an Android phone and open it (allow "install unknown apps"). For Play Store or a signed release build:

```bash
keytool -genkey -v -keystore sompriti.keystore -alias sompriti -keyalg RSA -keysize 2048 -validity 10000
cd android && ./gradlew assembleRelease   # configure signing in android/app/build.gradle first
```

`npm run android:open` opens the project in Android Studio, where you can run it on an emulator or a connected phone.

**Testing against your Mac instead of Railway**

Set `apiBaseUrl` to `http://<your-mac-ip>:5080` (find it with `ipconfig getifaddr en0`), run the API with
`ASPNETCORE_URLS=http://0.0.0.0:5080`, and allow plain http in `android/app/src/main/AndroidManifest.xml`
by adding `android:usesCleartextTraffic="true"` to the `<application>` tag. Use https in production.

## Repository layout

```
backend/
  src/Sompriti.Erp.Domain          entities, enums, business rules (calculations, status rules, BD mobile)
  src/Sompriti.Erp.Application     services (auth, users, master data, stock, orders, reports, SMS)
  src/Sompriti.Erp.Infrastructure  EF Core context, SQL migrations, JWT, password hashing, SMS/email providers, PDF
  src/Sompriti.Erp.Api             controllers, authentication handler, error handling, Program.cs
  tests/Sompriti.Erp.Tests         xUnit tests (rules, JWT, PDF, connection string)
frontend/
  src/app/core                     API client, auth, layout (phone vs desktop), Capacitor bridge
  src/app/shared                   reusable pieces: search select, list state, list footer, pipes
  src/app/layout                   app shell: sidebar on desktop, top bar + bottom tabs on phones
  src/app/features                 screens (auth, dashboard, master data, stock, orders, reports, users, SMS)
  public/fonts                     bundled Inter + Material Symbols subset (works offline)
  tools/build-icon-font.py         regenerates the icon font subset after adding new icons
  capacitor.config.ts              Android app settings
Dockerfile                         builds web + API into one image
railway.json                       Railway build/deploy settings
docker-compose.yml                 local PostgreSQL (and optional full app)
.env.example                       all configuration variables
```

## Run locally

Prerequisites: .NET 10 SDK, Node.js 22.22+ (or 24), Docker (for PostgreSQL).

```bash
# 1. database
docker compose up -d postgres

# 2. API  (http://localhost:5080) – applies migrations and seeds admin@sompriti.local / Admin@12345
cd backend
dotnet run --project src/Sompriti.Erp.Api

# 3. web  (http://localhost:4200)
cd frontend
npm install
npm start
```

Log in with `admin@sompriti.local` / `Admin@12345`; you will be asked to change the password.
Development settings live in `backend/src/Sompriti.Erp.Api/appsettings.Development.json`.
SMS and email use the `Log` provider locally (messages are written to the API console).

Run the tests:

```bash
cd backend
dotnet test
```

Or run everything in containers: `docker compose --profile app up --build` → http://localhost:8080

## Suggested first steps in the app

1. Companies → add your company (name, code, address, phone – printed on PDFs)
2. Products → add products (prices are per piece; set pcs per box for box items)
3. Stock adjustments → enter opening stock (reason: Opening stock)
4. Customers / Suppliers → add parties with valid Bangladesh mobile numbers
5. Create a purchase or sales order, finalize it, add payments, print the PDF

## Deploy to Railway

1. Push this repository to GitHub.
2. In Railway: **New Project → Deploy from GitHub repo** and select the repository. Railway detects `railway.json` and builds the `Dockerfile`.
3. **+ New → Database → PostgreSQL** in the same project.
4. Open the app service → **Variables** and add (see `.env.example`):
   - `DATABASE_URL` = `${{Postgres.DATABASE_URL}}` (variable reference to the database service)
   - `Jwt__SigningKey` = a long random secret (`openssl rand -base64 48`)
   - `SEED_ADMIN_EMAIL`, `SEED_ADMIN_PASSWORD` (first admin; min. 8 chars with a letter and a number)
   - `App__PublicBaseUrl` = your Railway URL, e.g. `https://sompriti-erp.up.railway.app`
   - SMS: `Sms__Enabled=true`, `Sms__Provider=BulkSmsBd`, `Sms__ApiKey`, `Sms__SenderId` (or `GenericHttp` settings)
   - Email: `Email__Provider=Brevo` (or `Resend`), `Email__ApiKey`, `Email__FromAddress`
5. **Settings → Networking → Generate Domain**.
6. Deploy. On start the app applies database migrations, creates the first admin and serves the web app. Health check: `/health`.

Backups: enable Railway PostgreSQL backups (or schedule `pg_dump`) with at least 7 days of retention.

## Database migrations

The schema is managed by plain SQL files in `backend/src/Sompriti.Erp.Infrastructure/Persistence/Migrations`
(`0001_initial.sql`, `0002_...sql`). They run in order at startup inside a PostgreSQL advisory lock and are recorded in
`schema_migrations`. To change the schema, add a new numbered file – never edit an applied one – and update the EF mapping
in `AppDbContext` if needed.

## API

Base path `/api/v1`, JSON (camelCase, enums as `UPPER_SNAKE_CASE`), errors as RFC 7807 problem details with a `code`
(`REVISION_CONFLICT`, `INSUFFICIENT_STOCK`, `OVERPAYMENT`, …). Every update/delete/status change must send the record's `revision`.

| Area | Endpoints |
|---|---|
| Auth | `POST auth/register, login, refresh, logout, forgot-password, reset-password, change-password` · `GET auth/me` |
| Master data | `companies`, `customers`, `suppliers`, `products` (list, `dropdown`, get, create, update, delete) |
| Users | `users` (ADMIN) + `POST users/{id}/unlock` |
| Stock | `GET stock/balances`, `GET stock/ledger`, `GET/POST stock/adjustments` |
| Orders | `purchase-orders`, `sales-orders`: list, get, create, update (draft), delete (draft), `DELETE {id}/lines/{lineId}`, `POST {id}/finalize`, `POST {id}/void`, `POST {id}/payments`, `DELETE {id}/payments/{paymentId}`, `GET {id}/pdf` |
| Reports | `GET reports/customers`, `reports/suppliers`, `reports/companies`, `GET dashboard` |
| SMS | `GET sms`, `POST sms/{id}/retry` (ADMIN) |
