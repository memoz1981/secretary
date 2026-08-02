import type { ReactNode } from "react";
import { useLanguage } from "@/shared/i18n/LanguageContext";

interface SidePanelProps {
  title: ReactNode;
  onClose: () => void;
  children: ReactNode;
}

export function SidePanel({ title, onClose, children }: SidePanelProps) {
  const { t } = useLanguage();
  return (
    <>
      <div className="backdrop" onClick={onClose} />
      <div className="panel">
        <div className="panel-header">
          <h1>{title}</h1>
          <button className="close-btn" onClick={onClose} aria-label={t("close")}>
            ✕
          </button>
        </div>
        {children}
      </div>
    </>
  );
}
