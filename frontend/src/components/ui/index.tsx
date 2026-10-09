import {
  useEffect,
  useId,
  useRef,
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
export function Skeleton({
  width,
  height,
  circle = false,
  className = "",
  style,
  ...props
}: HTMLAttributes<HTMLSpanElement> & {
  width?: number | string;
  height?: number | string;
  circle?: boolean;
}) {
  return (
    <span
      aria-hidden="true"
      className={`skeleton${circle ? " skeleton-circle" : ""} ${className}`}
      style={{ width, height, ...style }}
      {...props}
    />
  );
}
export function ConfirmDialog({
  open,
  title,
  message,
  error,
  confirmLabel = "Ya",
  cancelLabel = "Batal",
  busy = false,
  onConfirm,
  onCancel,
}: {
  open: boolean;
  title: string;
  message?: string;
  error?: string;
  confirmLabel?: string;
  cancelLabel?: string;
  busy?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  const titleId = useId();
  const cancelRef = useRef<HTMLButtonElement>(null);
  useEffect(() => {
    if (!open) return;
    cancelRef.current?.focus();
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape" && !busy) onCancel();
    };
    document.addEventListener("keydown", onKey);
    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      document.removeEventListener("keydown", onKey);
      document.body.style.overflow = previousOverflow;
    };
  }, [open, busy, onCancel]);
  if (!open) return null;
  return (
    <div
      className="modal-overlay"
      onMouseDown={(e) => {
        if (e.target === e.currentTarget && !busy) onCancel();
      }}
    >
      <div
        className="modal"
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
      >
        <h2 id={titleId}>{title}</h2>
        {message && <p>{message}</p>}
        {error && (
          <p role="alert" className="error">
            {error}
          </p>
        )}
        <div className="modal-actions">
          <button
            type="button"
            className="btn btn-ghost"
            ref={cancelRef}
            onClick={onCancel}
            disabled={busy}
          >
            {cancelLabel}
          </button>
          <button
            type="button"
            className="btn btn-primary"
            onClick={onConfirm}
            disabled={busy}
          >
            {confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
