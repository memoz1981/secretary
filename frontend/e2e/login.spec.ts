import { test, expect } from "@playwright/test";

// Expects a Staff or Owner test account to already exist in the backend's database —
// see README for how to seed one. Credentials come from env vars, never hardcoded.
const EMAIL = process.env.E2E_STAFF_EMAIL ?? "";
const PASSWORD = process.env.E2E_STAFF_PASSWORD ?? "";

test("login page loads and a valid login redirects to the calendar", async ({ page }) => {
  await page.goto("/login");
  await expect(page.getByText("Sign in")).toBeVisible();

  await page.fill("input[type=text]", EMAIL);
  await page.fill("input[type=password]", PASSWORD);
  await page.click("button[type=submit]");

  await expect(page).toHaveURL(/\/calendar/);
});

test("an invalid login shows an error instead of navigating", async ({ page }) => {
  await page.goto("/login");
  await page.fill("input[type=text]", "nobody@example.com");
  await page.fill("input[type=password]", "wrong-password");
  await page.click("button[type=submit]");

  await expect(page.getByText("Email or password is incorrect.")).toBeVisible();
  await expect(page).toHaveURL(/\/login/);
});
