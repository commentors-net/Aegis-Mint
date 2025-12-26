import { InputHTMLAttributes, ReactNode } from "react";

type Props = InputHTMLAttributes<HTMLInputElement> & {
  label: string;
  hint?: ReactNode;
};

export default function FormField({ label, hint, ...rest }: Props) {
  return (
    <label className="field">
      <div className="field-label">
        <span>{label}</span>
        {hint && <span className="muted">{hint}</span>}
      </div>
      <input {...rest} />
    </label>
  );
}
