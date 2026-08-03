import type { ReactNode } from "react";
import type { AccountRole, Module } from "@/shared/api/types";
import type { NavItem } from "@/shared/components/AppShell";
import type { TranslationKey } from "@/shared/i18n/translations";
import { appointmentsModule } from "@/modules/appointments/manifest";

/** Everything a module has to declare to exist in the app.
 *
 * The point of the shape is that adding a module is one folder and one line in MODULE_REGISTRY
 * below — no edit to App.tsx, no edit to a shared navigation file, no route list to keep in
 * sync. When the second module lands, that claim is what gets tested. */
export interface ModuleManifest {
  /** Matches the wire value the API grants — see Domain/Common/Enums/Module.cs. */
  key: Module;

  /** Every route this module owns lives under here. Makes the module guard one check on a
   *  subtree rather than one per page, and makes lazy-loading a one-line change later. */
  pathPrefix: string;

  /** Where the picker sends you, and where a single-module tenant lands. */
  home: string;

  titleKey: TranslationKey;
  descriptionKey: TranslationKey;

  nav: (role: AccountRole | null, t: (key: TranslationKey) => string) => NavItem[];

  /** <Route> elements, spread into the router by App. */
  routes: ReactNode;
}

/** The modules that actually exist in the front end.
 *
 * Deliberately shorter than the Module union: the API can grant Information or Feedback today,
 * because the grant model shipped before the modules did, but nothing here can render them. The
 * picker shows such a grant greyed out rather than pretending — see ModulePicker. */
export const MODULE_REGISTRY: ModuleManifest[] = [appointmentsModule];

export function findModule(key: Module): ModuleManifest | undefined
{
  return MODULE_REGISTRY.find((m) => m.key === key);
}

/** The tenant's modules that are actually usable — granted AND built. */
export function usableModules(enabled: Module[]): ModuleManifest[] {
  return enabled.map(findModule).filter((m): m is ModuleManifest => m !== undefined);
}
