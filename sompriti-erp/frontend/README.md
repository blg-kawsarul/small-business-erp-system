# Enterprise Resource Planning – Web and Android app

Angular 22 + Angular Material. The same code runs as a website and, through Capacitor, as an Android app.

```bash
npm install
npm start            # http://localhost:4200 (proxies /api to http://localhost:5080)
npm run build        # production build for the API's wwwroot
npm run build:mobile # build that points at environment.mobile.ts (full API URL, used for the APK)
```

## Responsive design

`LayoutService.isHandset()` (max-width 840px) switches the UI between two shapes:

| | Desktop | Phone |
|---|---|---|
| Navigation | left sidebar | top bar + 5 bottom tabs, "More" screen for the rest |
| Lists | tables with a paginator | one card per record, "Load more" |
| Filters | inline toolbar | search + status chips, extra filters in a full-screen sheet |
| Create/edit | centred dialog | full-screen sheet |
| Order lines | one grid row per line | one card per line |
| Primary action | header button | sticky bar above the tabs |

## Android (Capacitor)

See the root `README.md` for the full APK instructions. Short version:

```bash
npm run build:mobile && npx cap add android   # first time only
npm run android:apk                           # build the debug APK
npm run android:open                          # open in Android Studio
```

Set the API address in `src/environments/environment.mobile.ts` before building.

## Icons and fonts

Inter and a 36 KB Material Symbols subset are bundled in `public/fonts`, so the app needs no internet for its UI.
After adding a new `<mat-icon>`, regenerate the subset:

```bash
pip install fonttools brotli
npm install --save-dev material-symbols
python3 tools/build-icon-font.py
```

## App identity and credit

`src/app/core/app-info.ts` holds the product name, the version and the developer credit. Everything that
shows them reads from there: the About dialog (`shared/about-dialog.ts`), the footer of the desktop
navigation rail, the bottom of the phone **More** screen and the login page. Bump `version` there when you
release, and the whole app follows.

Structure: `core/` (API, auth, layout, platform, app info), `shared/` (reusable UI), `layout/` (shell + navigation), `features/` (screens).
