import { test, expect, APIRequestContext } from '@playwright/test';
import { readFileSync } from 'node:fs';

const API = process.env.UAT_API_URL ?? 'https://localhost:7174';

// Every row this spec creates carries the run prefix; nothing else is ever started, finished or deleted.
const ts = Date.now();
const gameName = `uat-${ts}`;
const genre = `uat-genre-${ts}`;

function bearer(): Record<string, string> {
  const state = JSON.parse(readFileSync('.auth/uat.json', 'utf8'));
  const token = state.origins[0].localStorage.find((e: { name: string }) => e.name === 'gp_token').value;
  return { Authorization: `Bearer ${token}` };
}

function api(request: APIRequestContext) {
  const headers = bearer();
  return {
    get: (path: string) => request.get(`${API}${path}`, { headers }),
    post: (path: string, data: unknown) => request.post(`${API}${path}`, { headers, data }),
    put: (path: string, data: unknown) => request.put(`${API}${path}`, { headers, data }),
    del: (path: string) => request.delete(`${API}${path}`, { headers }),
  };
}

test.describe.serial('daily flow', () => {
  let gameId = 0;
  let originalDevices: string[] = [];

  test.afterAll(async ({ playwright }) => {
    const request = await playwright.request.newContext({ ignoreHTTPSErrors: true });
    const a = api(request);
    try {
      if (gameId) {
        // Free the slot first (keeps status), then remove sessions, journal entry and the game itself.
        const slots = (await (await a.get('/api/now-playing')).json()) as { device: string; game: { id: number } | null }[];
        for (const s of slots.filter(s => s.game?.id === gameId)) {
          await a.post(`/api/now-playing/${s.device}/remove`, { outcome: 'None' });
        }
        const sessions = (await (await a.get(`/api/games/${gameId}/sessions`)).json()) as { id: number }[];
        for (const s of sessions) await a.del(`/api/sessions/${s.id}`);
        await a.del(`/api/journal/${gameId}`);
        await a.del(`/api/games/${gameId}`);
      }
    } finally {
      if (originalDevices.length) await a.put('/api/profile/devices', { devices: originalDevices });
      await request.dispose();
    }
  });

  test('set up devices and a game that tops What to Play on Steam Deck', async ({ request }) => {
    const a = api(request);

    const before = await a.get('/api/profile/devices');
    expect(before.status()).toBe(200);
    originalDevices = ((await before.json()) as { devices: string[] }).devices;

    const dev = await a.put('/api/profile/devices', { devices: ['PC', 'SteamDeck'] });
    expect(dev.ok()).toBeTruthy();

    const slots = (await (await a.get('/api/now-playing')).json()) as { device: string; game: { name: string } | null }[];
    const busy = slots.find(s => s.device === 'SteamDeck' && s.game);
    expect(busy, `SteamDeck slot is held by "${busy?.game?.name}"; the test never touches pre-existing games`).toBeUndefined();

    const created = await a.post('/api/games', {
      name: gameName, genres: [genre], estimatedMinutes: 90, tags: [], store: 'Steam',
    });
    expect(created.status()).toBe(201);
    gameId = ((await created.json()) as { id: number }).id;

    expect((await a.put(`/api/games/${gameId}/favorite`, { isFavorite: true })).ok()).toBeTruthy();
  });

  test('pick on What to Play, log 1,5 h with a note, finish with 4 stars', async ({ page }) => {
    await page.goto('/what-to-play');
    await page.getByRole('button', { name: /Steam Deck/ }).first().click();
    await page.getByRole('button', { name: genre, exact: true }).click();

    const top = page.locator('.top .name');
    await expect(top).toBeVisible();
    // Never start a game this test did not create.
    await expect(top).toHaveText(gameName);

    await page.getByRole('button', { name: /Start on Steam Deck/ }).click();
    await expect(page).toHaveURL(/\/now-playing$/);

    const slot = page.locator('.slot', { hasText: gameName });
    await expect(slot).toBeVisible();
    await slot.getByRole('button', { name: '+ Log session' }).click();
    await page.locator('input[name="hours"]').fill('1,5');
    await page.locator('textarea[name="note"]').fill(`${gameName} note`);
    await page.getByRole('button', { name: 'Save session' }).click();
    await expect(slot.locator('.sessions')).toContainText('1.5 h');
    await expect(slot.locator('.sessions')).toContainText(`${gameName} note`);

    await slot.getByRole('button', { name: 'Finish / remove' }).click();
    await page.locator('.choice.fin .star').nth(3).click();
    await page.locator('.choice.fin').click({ position: { x: 10, y: 10 } });
    await expect(page.locator('.slot', { hasText: gameName })).toHaveCount(0);
  });

  test('journal shows Finished, detail lists the session, What to Play no longer offers it', async ({ page, request }) => {
    await page.goto('/journal');
    const tile = page.locator('.game-tile', { hasText: gameName });
    await expect(tile).toBeVisible();
    await expect(tile.locator('.tile-status')).toHaveText('Finished');

    const entry = await (await api(request).get(`/api/journal/${gameId}`)).json();
    expect(entry.userRating).toBe(8);

    await page.goto(`/games/${gameId}`);
    const row = page.locator('.session-row');
    await expect(row).toHaveCount(1);
    await expect(row).toContainText('1.5 h');
    await expect(row).toContainText(`${gameName} note`);

    await page.goto('/what-to-play');
    await page.getByRole('button', { name: /Steam Deck/ }).first().click();
    const recommended = page.waitForResponse(r => r.url().includes('/api/what-to-play') && r.request().postData()?.includes(genre) === true);
    await page.getByRole('button', { name: genre, exact: true }).click();
    const body = (await (await recommended).json()) as { results: { name: string }[] };
    expect(body.results.map(r => r.name)).not.toContain(gameName);
    await expect(page.locator('.top .name, .empty-state').first()).toBeVisible();
    await expect(page.locator('.res').getByText(gameName)).toHaveCount(0);
  });
});

test.describe('non-admin cannot see workers', () => {
  test('sidebar has no Workers item and /workers lands on /dashboard', async ({ page, request }) => {
    expect((await api(request).get('/api/worker')).status()).toBe(403);

    await page.goto('/dashboard');
    await expect(page.locator('.sidebar-nav .nav-item', { hasText: 'Now Playing' })).toBeVisible();
    await expect(page.locator('.sidebar-nav .nav-item', { hasText: 'Workers' })).toHaveCount(0);

    await page.goto('/workers');
    await expect(page).toHaveURL(/\/dashboard$/);
  });
});
