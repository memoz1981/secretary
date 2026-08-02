import type { InputHTMLAttributes, ReactNode, SelectHTMLAttributes, TextareaHTMLAttributes } from "react";

interface FieldWrapperProps {
  label: string;
  error?: string;
  children: ReactNode;
}

function FieldWrapper({ label, error, children }: FieldWrapperProps) {
  return (
    <div className="form-row">
      <label>{label}</label>
      {children}
      {error && <div className="field-error">{error}</div>}
    </div>
  );
}

interface TextFieldProps extends InputHTMLAttributes<HTMLInputElement> {
  label: string;
  error?: string;
}

export function TextField({ label, error, className, ...rest }: TextFieldProps) {
  return (
    <FieldWrapper label={label} error={error}>
      <input className={[error ? "error" : "", className ?? ""].filter(Boolean).join(" ")} {...rest} />
    </FieldWrapper>
  );
}

interface TextAreaFieldProps extends TextareaHTMLAttributes<HTMLTextAreaElement> {
  label: string;
  error?: string;
}

export function TextAreaField({ label, error, className, ...rest }: TextAreaFieldProps) {
  return (
    <FieldWrapper label={label} error={error}>
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
