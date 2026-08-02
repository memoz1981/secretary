import { test, expect } from "@playwright/test";
import { loginAsPlatformAdmin } from "./helpers";

test("platform admin can view tenant list and create a new tenant", async ({ page }) => {
  await loginAsPlatformAdmin(page);
  await expect(page).toHaveURL(/\/admin\/tenants/);
  await expect(page.getByText("Tenants")).toBeVisible();

  await page.click("text=+ Create tenant");
  const suffix = Date.now();
  await page.fill("text=Business name >> input", `Smoke Test Business ${suffix}`);
  await page.fill("text=Owner name >> input", "Smoke Test Owner");
  await page.fill("text=Owner email >> input", `owner-${suffix}@example.com`);
  await page.fill("text=Owner password >> input", "SmokeTest123!");
  await page.click("button:has-text('Create tenant')");

  await expect(page).toHaveURL(/\/admin\/tenants\/.+/);
  await expect(page.getByText(`Smoke Test Business ${suffix}`)).toBeVisible();
});
