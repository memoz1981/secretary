import { test, expect } from "@playwright/test";
import { loginAsStaff } from "./helpers";

test("services page loads and adding a service completes", async ({ page }) => {
  await loginAsStaff(page);
  await page.goto("/services");
  await expect(page.getByText("Services")).toBeVisible();

  await page.click("text=+ Add service");
  await page.fill("text=Name >> input", "Smoke Test Trim");
  const inputs = page.locator(".panel input");
  await inputs.nth(1).fill("10");
  await inputs.nth(2).fill("20");
  await page.click(".panel button:has-text('Save')");

  await expect(page.getByText("Smoke Test Trim")).toBeVisible();
});
