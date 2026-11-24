import { useState } from "react"
import { Input } from "@/components/ui/input"
import { cn } from "@/lib/utils"
import { formatNumber } from "@/lib/format"
import { toLatinDigits } from "@/lib/jalali"

interface Props {
  value: number | null | undefined
  onChange: (value: number) => void
  decimals?: number
  id?: string
  invalid?: boolean
  className?: string
  placeholder?: string
  min?: number
  "aria-label"?: string
}

/** Number field that accepts Persian or Latin digits and shows thousands separators when not focused. */
export function NumberInput({ value, onChange, decimals = 0, id, invalid, className, placeholder, min = 0, ...rest }: Props) {
  const [focused, setFocused] = useState(false)
  const [text, setText] = useState("")

  const parse = (raw: string): number | null => {
    const cleaned = toLatinDigits(raw).replace(/[٬,\s]/g, "").replace("٫", ".") // i18n-ignore
    if (cleaned === "") return null
    const n = Number(cleaned)
    return Number.isFinite(n) ? n : null
  }

  return (
    <Input
      id={id}
      dir="ltr"
      inputMode="decimal"
      aria-invalid={invalid}
      aria-label={rest["aria-label"]}
      placeholder={placeholder}
      className={cn("num text-end", className)}
      value={focused ? text : value === null || value === undefined ? "" : formatNumber(value, Math.min(decimals, countDecimals(value)))}
      onFocus={() => {
        setFocused(true)
        setText(value === null || value === undefined ? "" : String(value))
      }}
      onChange={(e) => {
        setText(e.target.value)
        const n = parse(e.target.value)
        if (n !== null && n >= min) onChange(n)
        if (e.target.value.trim() === "") onChange(0)
      }}
      onBlur={() => setFocused(false)}
    />
  )
}

function countDecimals(n: number): number {
  const s = String(n)
  const i = s.indexOf(".")
  return i < 0 ? 0 : s.length - i - 1
}
