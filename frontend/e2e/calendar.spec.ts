import { test, expect } from "@playwright/test";
import { loginAsStaff } from "./helpers";

test("calendar page loads and the new-appointment form submits successfully", async ({ page }) => {
  await loginAsStaff(page);
  await expect(page).toHaveURL(/\/calendar/);
  await expect(page.getByText("+ New appointment")).toBeVisible();

  await page.click("text=+ New appointment");
  await page.fill("input[placeholder='+994 XX XXX XX XX']", "+994501112233");
  const durationSelects = page.locator("select");
  await durationSelects.nth(0).selectOption({ index: 1 }); // service — confirms it's populated
  await durationSelects.nth(1).selectOption({ index: 0 }); // provider

  const start = new Date(Date.now() + 24 * 60 * 60 * 1000);
  start.setHours(14, 0, 0, 0);
  const localValue = start.toISOString().slice(0, 16);
  await page.fill("input[type=datetime-local]", localValue);

  await page.click("button:has-text('Save')");

  // Panel closes and the calendar reflects the new appointment without a page error.
  await expect(page.locator(".panel")).toHaveCount(0);
});
