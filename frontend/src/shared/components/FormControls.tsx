import type { InputHTMLAttributes, ReactNode, SelectHTMLAttributes, TextareaHTMLAttributes } from "react";

interface FieldWrapperProps {
  label: string;
  /** What the field wants, in a line, under its name.
   *
   * ⚠ A slot rather than a div the caller puts after the control. That is how it was done — a
   * sibling `.sub` with a negative top margin to drag it back up against the box — and it left
   * the explanation *below* the thing it was explaining, at the same size as the label it was
   * subordinate to. A reader met the box before the sentence telling them what to type in it. */
  hint?: string;
  error?: string;
  children: ReactNode;
}

function FieldWrapper({ label, hint, error, children }: FieldWrapperProps) {
  return (
    <div className="form-row">
      <label>{label}</label>
      {hint && <div className="hint">{hint}</div>}
      {children}
      {error && <div className="field-error">{error}</div>}
    </div>
  );
}

interface TextFieldProps extends InputHTMLAttributes<HTMLInputElement> {
  label: string;
  hint?: string;
  error?: string;
}

export function TextField({ label, hint, error, className, ...rest }: TextFieldProps) {
  return (
    <FieldWrapper label={label} hint={hint} error={error}>
      <input className={[error ? "error" : "", className ?? ""].filter(Boolean).join(" ")} {...rest} />
    </FieldWrapper>
  );
}

interface TextAreaFieldProps extends TextareaHTMLAttributes<HTMLTextAreaElement> {
  label: string;
  hint?: string;
  error?: string;
}

export function TextAreaField({ label, hint, error, className, ...rest }: TextAreaFieldProps) {
  return (
    <FieldWrapper label={label} hint={hint} error={error}>
      <textarea className={[error ? "error" : "", className ?? ""].filter(Boolean).join(" ")} {...rest} />
    </FieldWrapper>
  );
}

interface SelectOption {
  value: string;
  label: string;
}

interface SelectFieldProps extends SelectHTMLAttributes<HTMLSelectElement> {
  label: string;
  error?: string;
  options: SelectOption[];
  placeholder?: string;
}

export function SelectField({ label, error, options, placeholder, className, ...rest }: SelectFieldProps) {
  return (
    <FieldWrapper label={label} error={error}>
      <select className={[error ? "error" : "", className ?? ""].filter(Boolean).join(" ")} {...rest}>
        {placeholder && <option value="">{placeholder}</option>}
        {options.map((o) => (
          <option key={o.value} value={o.value}>
            {o.label}
          </option>
        ))}
      </select>
    </FieldWrapper>
  );
}
