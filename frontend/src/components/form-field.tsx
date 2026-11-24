import type { ReactNode } from "react"
import type { FieldValues, Path, UseFormSetError } from "react-hook-form"
import { Label } from "@/components/ui/label"
import { cn } from "@/lib/utils"
import { ApiError } from "@/services/api"

/** Label + control + hint/error, laid out consistently in every form. */
export function FormField({ label, htmlFor, error, hint, children, className, required }: {
  label: string; htmlFor?: string; error?: string; hint?: ReactNode; children: ReactNode; className?: string; required?: boolean
}) {
  return (
    <div className={cn("grid content-start gap-1.5", className)}>
      <Label htmlFor={htmlFor} className="text-sm">
        {label}
        {required && <span className="text-destructive" aria-hidden> *</span>}
      </Label>
      {children}
      {error ? <p className="text-xs text-destructive" role="alert">{error}</p> : hint ? <p className="text-xs text-muted-foreground">{hint}</p> : null}
    </div>
  )
}

/** Puts server-side validation errors next to the matching fields. Returns true if any were applied. */
export function applyServerErrors<T extends FieldValues>(err: unknown, setError: UseFormSetError<T>): boolean {
  if (!(err instanceof ApiError) || !err.errors) return false
  for (const [field, messages] of Object.entries(err.errors)) setError(field as Path<T>, { message: messages[0] })
  return true
}
