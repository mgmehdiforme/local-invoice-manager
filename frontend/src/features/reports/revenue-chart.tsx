import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts"
import { fa, formatMoney, type CurrencyFormat } from "@/lib/format"
import { i18n, t } from "@/i18n"
import { useCurrency } from "@/hooks/use-settings"
import type { RevenuePoint } from "./api"

/** Validated categorical pair (dataviz validator: all checks pass on white). Fixed order: invoiced, received. */
export const SERIES = [
  { key: "invoiced", label: t("فاکتورشده"), color: "#0f8a74" },
  { key: "received", label: t("دریافت‌شده"), color: "#5b6fd6" },
] as const

function compact(n: number): string {
  const abs = Math.abs(n)
  if (abs >= 1e12) return fa(+(n / 1e12).toFixed(1)) + t(" هزار میلیارد")
  if (abs >= 1e9) return fa(+(n / 1e9).toFixed(1)) + t(" میلیارد")
  if (abs >= 1e6) return fa(+(n / 1e6).toFixed(1)) + t(" میلیون")
  if (abs >= 1e3) return fa(+(n / 1e3).toFixed(0)) + t(" هزار")
  return fa(n)
}

/** Grouped bars per month (Jalali or Gregorian, labels come from the server): invoiced vs received. One axis, legend + hover tooltip, table view lives on the reports page. */
export function RevenueChart({ data, height = 260, currency: own }: { data: RevenuePoint[]; height?: number; currency?: CurrencyFormat }) {
  const app = useCurrency()
  const currency = own ?? app
  const rtl = i18n.dir === "rtl"
  // Axis label: the month name only; English names are cut to three letters so twelve months fit.
  const rows = data.map((d) => { const m = d.label.split(" ")[0]; return { ...d, short: i18n.lang === "en" ? m.slice(0, 3) : m } })
  return (
    <div>
      <div className="mb-2 flex gap-4 text-xs text-muted-foreground" role="list" aria-label={t("راهنمای نمودار")}>
        {SERIES.map((s) => (
          <span key={s.key} className="inline-flex items-center gap-1.5" role="listitem">
            <span className="size-2.5 rounded-sm" style={{ background: s.color }} /> {s.label}
          </span>
        ))}
      </div>
      <div style={{ height }} dir="ltr">
        <ResponsiveContainer width="100%" height="100%">
          <BarChart data={rows} barGap={2} barCategoryGap="22%" margin={{ top: 4, right: 4, bottom: 0, left: 4 }}>
            <CartesianGrid vertical={false} stroke="#e2e5e2" strokeDasharray="0" />
            <XAxis dataKey="short" reversed={rtl} tickLine={false} axisLine={false} tick={{ fontSize: 11, fill: "#66706c", fontFamily: "inherit" }} interval={0} />
            <YAxis orientation={rtl ? "right" : "left"} tickLine={false} axisLine={false} width={84} tickFormatter={compact} tick={{ fontSize: 11, fill: "#66706c", fontFamily: "inherit" }} />
            <Tooltip
              cursor={{ fill: "rgba(24,33,31,.05)" }}
              content={({ active, payload }) => {
                if (!active || !payload?.length) return null
                const p = payload[0].payload as RevenuePoint
                return (
                  <div dir={i18n.dir} className="rounded-lg border bg-popover px-3 py-2 text-xs shadow-md">
                    <div className="mb-1 font-semibold">{fa(p.label)}</div>
                    {SERIES.map((s) => (
                      <div key={s.key} className="flex items-center justify-between gap-4">
                        <span className="inline-flex items-center gap-1.5 text-muted-foreground"><span className="size-2 rounded-sm" style={{ background: s.color }} />{s.label}</span>
                        <span className="num font-medium">{formatMoney(p[s.key], currency)}</span>
                      </div>
                    ))}
                    <div className="mt-1 text-muted-foreground">{t("{0} فاکتور", [fa(p.invoiceCount)])}</div>
                  </div>
                )
              }}
            />
            {SERIES.map((s) => (
              <Bar key={s.key} dataKey={s.key} name={s.label} fill={s.color} radius={[4, 4, 0, 0]} maxBarSize={22} />
            ))}
          </BarChart>
        </ResponsiveContainer>
      </div>
    </div>
  )
}
