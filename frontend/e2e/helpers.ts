import type { Page } from "@playwright/test";

export async function loginAsStaff(page: Page) {
  await page.goto("/login");
  await page.fill("input[type=text]", process.env.E2E_STAFF_EMAIL ?? "");
  await page.fill("input[type=password]", process.env.E2E_STAFF_PASSWORD ?? "");
  await page.click("button[type=submit]");
}

export async function loginAsPlatformAdmin(page: Page) {
  await page.goto("/login");
  await page.fill("input[type=text]", process.env.E2E_PLATFORM_ADMIN_EMAIL ?? "");
  await page.fill("input[type=password]", process.env.E2E_PLATFORM_ADMIN_PASSWORD ?? "");
  await page.click("button[type=submit]");
}
