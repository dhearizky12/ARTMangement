import {
  useId,
  type ButtonHTMLAttributes,
  type HTMLAttributes,
  type InputHTMLAttributes,
  type ReactNode,
  type SelectHTMLAttributes,
  type TextareaHTMLAttributes,
} from "react";
export function Button({
  variant = "primary",
  className = "",
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: "primary" | "secondary" | "ghost";
}) {
  return (
    <button
      type="button"
      className={`btn btn-${variant} ${className}`}
      {...props}
    />
  );
}
export function Card({
  lifted = false,
  className = "",
  ...props
}: HTMLAttributes<HTMLDivElement> & { lifted?: boolean }) {
  return (
    <div className={`card ${lifted ? "lifted" : ""} ${className}`} {...props} />
  );
}
export function Badge({
  tone = "neutral",
  sticker = false,
  children,
}: {
  tone?: "neutral" | "success" | "accent";
  sticker?: boolean;
  children: ReactNode;
}) {
  return (
    <span className={`badge badge-${tone} ${sticker ? "sticker" : ""}`}>
      {children}
    </span>
  );
}
type FieldProps = { label: string; hint?: string; error?: string };
export function Input({
  label,
  hint,
  error,
  id,
  ...props
}: InputHTMLAttributes<HTMLInputElement> & FieldProps) {
  const generated = useId();
  const fieldId = id || generated;
  return (
    <div className="field">
      <label htmlFor={fieldId}>{label}</label>
      <input
        className="input"
        id={fieldId}
        aria-invalid={!!error}
        aria-describedby={hint || error ? `${fieldId}-hint` : undefined}
        {...props}
      />
      {(hint || error) && (
        <small
          id={`${fieldId}-hint`}
          className={error ? "field-error" : "muted"}
        >
          {error || hint}
        </small>
      )}
    </div>
  );
}
export function Select({
  label,
  id,
  children,
  ...props
}: SelectHTMLAttributes<HTMLSelectElement> & FieldProps) {
  const generated = useId();
  const fieldId = id || generated;
  return (
    <div className="field">
      <label htmlFor={fieldId}>{label}</label>
      <select className="input" id={fieldId} {...props}>
        {children}
      </select>
    </div>
  );
}
export function Textarea({
  label,
  id,
  ...props
}: TextareaHTMLAttributes<HTMLTextAreaElement> & FieldProps) {
  const generated = useId();
  const fieldId = id || generated;
  return (
    <div className="field">
      <label htmlFor={fieldId}>{label}</label>
      <textarea className="input" id={fieldId} {...props} />
    </div>
  );
}
