import { test, expect } from '@playwright/test';

const UAT_EMAIL = process.env.UAT_EMAIL ?? '';
const UAT_PASSWORD = process.env.UAT_PASSWORD ?? '';

test.describe.serial('GamePlanner UAT', () => {
  test.describe('auth', () => {
    test.use({ storageState: { cookies: [], origins: [] } });

    test('unauthenticated visit to /games redirects to /login', async ({ page }) => {
      await page.goto('/games');
      await expect(page).toHaveURL(/\/login/);
    });

    test('steam failure redirect shows the fixed copy', async ({ page }) => {
      await page.goto('/login?error=steam_failed');
      await expect(page.getByText('Steam login failed. Please try again.', { exact: true })).toBeVisible();
    });

    test('wrong password shows the fixed copy', async ({ page }) => {
      await page.goto('/login');
      await page.getByPlaceholder('Email').fill(UAT_EMAIL);
      await page.getByPlaceholder('Password').fill(`wrong-${Date.now()}`);
      await page.getByRole('button', { name: 'Sign in', exact: true }).click();
      await expect(page.getByText('Invalid email or password.', { exact: true })).toBeVisible();
    });

    test('valid credentials land on the authenticated route', async ({ page }) => {
      await page.goto('/login');
      await page.getByPlaceholder('Email').fill(UAT_EMAIL);
      await page.getByPlaceholder('Password').fill(UAT_PASSWORD);
      await page.getByRole('button', { name: 'Sign in', exact: true }).click();
      await expect(page).toHaveURL(/\/games$/);
    });
  });
});
