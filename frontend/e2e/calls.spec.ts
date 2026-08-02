import { test, expect } from "@playwright/test";
import { loginAsStaff } from "./helpers";

test("call log loads and navigating into a call's detail works", async ({ page }) => {
  await loginAsStaff(page);
  await page.goto("/calls");
  await expect(page.getByText("Call log")).toBeVisible();

  const firstRow = page.locator("table.data tbody tr").first();
  const rowCount = await page.locator("table.data tbody tr").count();
  test.skip(rowCount === 0, "No calls exist yet for this test tenant — nothing to click into.");

  await firstRow.click();
  await expect(page).toHaveURL(/\/calls\/.+/);
  await expect(page.getByText("Recording")).toBeVisible();
});
