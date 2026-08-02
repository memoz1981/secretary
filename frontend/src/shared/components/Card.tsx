import type { CSSProperties, ReactNode } from "react";

export function Card({
  children,
  style,
  className,
}: {
  children: ReactNode;
  style?: CSSProperties;
  className?: string;
}) {
  return (
    <div className={className ? `card ${className}` : "card"} style={style}>
      {children}
    </div>
  );
}
