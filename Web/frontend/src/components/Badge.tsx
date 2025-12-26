import clsx from "clsx";
import { ReactNode } from "react";

type Props = {
  tone?: "neutral" | "good" | "warn" | "bad";
  children: ReactNode;
};

export default function Badge({ tone = "neutral", children }: Props) {
  return <span className={clsx("pill", tone !== "neutral" && `pill-${tone}`)}>{children}</span>;
}
