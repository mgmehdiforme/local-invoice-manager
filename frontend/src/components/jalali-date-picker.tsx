import { useEffect, useMemo, useState } from "react"
import { CalendarDays, ChevronLeft, ChevronRight, X } from "lucide-react"
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover"
import { Button } from "@/components/ui/button"
import { cn } from "@/lib/utils"
import { fa, formatDate } from "@/lib/format"
import { todayIso } from "@/lib/jalali"
import { datePlaceholder, fromParts, monthLength, monthNames, parseDate, toParts, weekdayIndex, weekdayNames } from "@/lib/calendar"
import { i18n, t } from "@/i18n"

interface Props {
  value: string | null | undefined
  onChange: (iso: string | null) => void
  placeholder?: string
  clearable?: boolean
  id?: string
  invalid?: boolean
  className?: string
}

/**
 * Date input in the calendar chosen in settings (Jalali or Gregorian): type "1405/08/24" / "2026-11-15"
 * or pick from the month grid. The value is always a Gregorian ISO date.
 */
export function JalaliDatePicker({ value, onChange, placeholder, clearable, id, invalid, className }: Props) {
  const [open, setOpen] = useState(false)
  const [text, setText] = useState(value ? formatDate(value) : "")
  const initial = toParts(value ?? todayIso())
  const [view, setView] = useState({ y: initial.y, m: initial.m })

  useEffect(() => {
    setText(value ? formatDate(value) : "")
    if (value) {
      const p = toParts(value)
      setView({ y: p.y, m: p.m })
    }
  }, [value])

  const today = todayIso()
  const days = useMemo(() => {
    const first = weekdayIndex({ y: view.y, m: view.m, d: 1 })
    const len = monthLength(view.y, view.m)
    return [...Array(first).fill(null), ...Array.from({ length: len }, (_, i) => i + 1)] as (number | null)[]
  }, [view])

  const move = (delta: number) =>
    setView((v) => {
      const total = v.y * 12 + (v.m - 1) + delta
      return { y: Math.floor(total / 12), m: (total % 12) + 1 }
    })

  const rtl = i18n.dir === "rtl"
  const PrevIcon = rtl ? ChevronRight : ChevronLeft
  const NextIcon = rtl ? ChevronLeft : ChevronRight

  const commitText = () => {
    if (!text.trim()) return clearable ? onChange(null) : setText(value ? formatDate(value) : "")
    const iso = parseDate(text)
    if (iso) onChange(iso)
    else setText(value ? formatDate(value) : "")
  }

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <div
        className={cn(
          "flex h-9 w-full items-center rounded-md border border-input bg-paper text-sm shadow-xs focus-within:ring-[3px] focus-within:ring-ring/50",
          invalid && "border-destructive",
          className,
        )}
      >
        <input
          id={id}
          dir="ltr"
          className="num h-full min-w-0 flex-1 bg-transparent px-3 text-end outline-none placeholder:text-muted-foreground"
          placeholder={placeholder ?? fa(datePlaceholder())}
          value={text}
          aria-invalid={invalid}
          onChange={(e) => setText(e.target.value)}
          onBlur={commitText}
          onKeyDown={(e) => e.key === "Enter" && (e.preventDefault(), commitText())}
        />
        {clearable && value && (
          <button type="button" className="px-1 text-muted-foreground hover:text-foreground" onClick={() => onChange(null)} aria-label={t("پاک کردن تاریخ")}>
            <X className="size-4" />
          </button>
        )}
        <PopoverTrigger asChild>
          <button type="button" className="flex h-full items-center px-2.5 text-muted-foreground hover:text-foreground" aria-label={t("باز کردن تقویم")}>
            <CalendarDays className="size-4" />
          </button>
        </PopoverTrigger>
      </div>
      <PopoverContent align="end" className="w-72 p-3">
        <div className="mb-2 flex items-center justify-between">
          <Button type="button" variant="ghost" size="icon" onClick={() => move(-1)} aria-label={t("ماه قبل")}>
            <PrevIcon className="size-4" />
          </Button>
          <div className="text-sm font-semibold">
            {monthNames()[view.m - 1]} {fa(view.y)}
          </div>
          <Button type="button" variant="ghost" size="icon" onClick={() => move(1)} aria-label={t("ماه بعد")}>
            <NextIcon className="size-4" />
          </Button>
        </div>
        <div className="grid grid-cols-7 gap-1 text-center text-xs text-muted-foreground">
          {weekdayNames().map((d) => (
            <div key={d} className="py-1">{d}</div>
          ))}
        </div>
        <div className="grid grid-cols-7 gap-1 text-center">
          {days.map((d, i) => {
            if (d === null) return <div key={`e${i}`} />
            const iso = fromParts({ y: view.y, m: view.m, d })
            const selected = iso === value
            return (
              <button
                key={iso}
                type="button"
                onClick={() => {
                  onChange(iso)
                  setOpen(false)
                }}
                className={cn(
                  "num h-8 rounded-md text-sm hover:bg-accent",
                  iso === today && "font-bold text-brand",
                  selected && "bg-primary text-primary-foreground hover:bg-primary",
                )}
              >
                {fa(d)}
              </button>
            )
          })}
        </div>
        <div className="mt-2 flex justify-between border-t pt-2">
          <Button type="button" variant="ghost" size="sm" onClick={() => (onChange(today), setOpen(false))}>
            {t("امروز")}
          </Button>
          {clearable && (
            <Button type="button" variant="ghost" size="sm" onClick={() => (onChange(null), setOpen(false))}>
              {t("بدون تاریخ")}
            </Button>
          )}
        </div>
      </PopoverContent>
    </Popover>
  )
}

/** Calendar-neutral name for the same component. */
export const DatePicker = JalaliDatePicker
