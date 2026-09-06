import type { CapacitorConfig } from '@capacitor/cli';

const config: CapacitorConfig = {
  appId: 'pl.notionmanagement.inbox',
  appName: 'Notion Management',
  webDir: 'dist',
  // Local emulator development only. Remove these HTTP allowances before distribution.
  server: { androidScheme: 'https', cleartext: true },
  android: { allowMixedContent: true },
};

export default config;
