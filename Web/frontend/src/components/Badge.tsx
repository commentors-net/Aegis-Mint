import clsx from "clsx";
import { ReactNode } from "react";

type Props = {
  tone?: "neutral" | "good" | "warn" | "bad" | "danger";
  children: ReactNode;
  className?: string;
};

export default function Badge({ tone = "neutral", children, className }: Props) {
  return <span className={clsx("pill", tone !== "neutral" && `pill-${tone}`, className)}>{children}</span>;
}
