import clsx from "clsx";
import { ButtonHTMLAttributes } from "react";

type Props = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: "primary" | "ghost" | "danger" | "secondary";
  size?: "sm" | "md";
};

export default function Button({ variant = "primary", size = "md", className, ...rest }: Props) {
  return (
    <button
      className={clsx(
        "btn",
        `btn-${variant}`,
        size === "sm" ? "btn-sm" : "btn-md",
        className,
        rest.disabled && "btn-disabled",
      )}
      {...rest}
    />
  );
}
