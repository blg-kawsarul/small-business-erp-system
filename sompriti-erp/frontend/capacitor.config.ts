import type { CapacitorConfig } from '@capacitor/cli';

const config: CapacitorConfig = {
  appId: 'com.sompriti.erp',
  appName: 'Enterprise Resource Planning',
  webDir: 'dist/frontend',
  android: {
    // The WebView origin becomes https://localhost, which must be allowed by the API's CORS setting.
    allowMixedContent: false,
  },
  plugins: {
    SplashScreen: {
      launchAutoHide: false,
      backgroundColor: '#ffffff',
      androidScaleType: 'CENTER_CROP',
    },
  },
};

export default config;
