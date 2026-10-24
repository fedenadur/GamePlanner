import { request } from '@playwright/test';
import { mkdirSync, writeFileSync } from 'node:fs';

const UAT_EMAIL = 'uat-test@gameplanner.local';
const OWNER_USER_ID = '2';
const NAME_ID_CLAIM = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier';

export default async function globalSetup(): Promise<void> {
  const ui = process.env.UAT_UI_URL ?? 'http://localhost:4200';
  const api = process.env.UAT_API_URL ?? 'https://localhost:7174';

  const api401 = await request.newContext({ ignoreHTTPSErrors: true });
  try {
    const uiRes = await fetch(ui).catch(() => null);
    const apiRes = await api401.get(`${api}/api/games`).catch(() => null);
    if (uiRes?.status !== 200 || apiRes?.status() !== 401) {
      throw new Error('Dev servers not reachable — start them (see CLAUDE.md Commands)');
    }

    if (process.env.UAT_EMAIL !== UAT_EMAIL) {
      throw new Error(`Refusing to run UAT: UAT_EMAIL must be ${UAT_EMAIL}`);
    }
    const password = process.env.UAT_PASSWORD;
    if (!password) {
      throw new Error('UAT_PASSWORD is empty — set it in e2e/.env or the environment');
    }

    const login = await api401.post(`${api}/api/auth/login`, {
      data: { email: UAT_EMAIL, password },
    });
    if (!login.ok()) {
      throw new Error(`UAT login failed with HTTP ${login.status()}`);
    }
    const { token } = (await login.json()) as { token: string };

    const payload = JSON.parse(Buffer.from(token.split('.')[1], 'base64url').toString('utf8'));
    const userId = String(payload['nameid'] ?? payload[NAME_ID_CLAIM] ?? '');
    if (!userId || userId === OWNER_USER_ID) {
      throw new Error('Refusing to run UAT: token does not belong to the dedicated test account');
    }

    mkdirSync('.auth', { recursive: true });
    writeFileSync(
      '.auth/uat.json',
      JSON.stringify({
        cookies: [],
        origins: [{ origin: new URL(ui).origin, localStorage: [{ name: 'gp_token', value: token }] }],
      }),
    );
  } finally {
    await api401.dispose();
  }
}
