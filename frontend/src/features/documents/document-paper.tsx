import type { ReactNode } from "react"
import { cn } from "@/lib/utils"
import { fa, formatAmount, formatDate, formatMoney, formatQuantity } from "@/lib/format"
import { i18n, t } from "@/i18n"
import type { Currency, CustomerSnapshot, LineInput, SellerSnapshot, Totals } from "./types"

const LIST_SEP = i18n.lang === "fa" ? "، " : ", " // i18n-ignore
const PCT = i18n.lang === "fa" ? "٪" : "%" // i18n-ignore

export interface PaperProps {
  title: string
  number: string
  meta: { label: string; value: string }[]
  seller: SellerSnapshot
  customer: CustomerSnapshot | null
  items: LineInput[]
  totals: Totals | null
  currency: Currency
  taxRate: number
  discountType: "None" | "Fixed" | "Percent"
  discountValue: number
  extraTotals?: { label: string; value: number; strong?: boolean }[]
  notes?: string | null
  paymentTerms?: string | null
  template: string
  logoUrl?: string | null
  watermark?: string | null
  className?: string
}

/**
 * The document drawn as an A4 sheet (same structure as the PDF), used for the live preview and the detail page.
 * Amounts come from the server's calculation, never from the browser.
 */
export function DocumentPaper(p: PaperProps) {
  const accent = p.seller.accentColor || "#0e6b5c"
  const tpl = p.template
  const exempt = p.items.some((l) => !l.isTaxable) && p.taxRate > 0
  const bank: ReactNode[] = [
    seg([p.seller.bankName], [p.seller.bankAccountHolder && t("به نام {0}", [p.seller.bankAccountHolder])]),
    seg([t("شماره حساب"), p.seller.bankAccountNumber, true]),
    seg([t("شماره کارت"), p.seller.bankCardNumber?.replace(/(\d{4})(?=\d)/g, "$1-"), true]),
    seg([t("شبا"), p.seller.bankIban && (/^[A-Z]{2}/.test(p.seller.bankIban) ? p.seller.bankIban : "IR" + p.seller.bankIban), true]),
  ].filter(Boolean)

  return (
    <div
      dir={i18n.dir}
      className={cn("relative mx-auto min-h-[640px] w-full max-w-[794px] overflow-hidden bg-white text-[11px] leading-relaxed text-[#18211f] shadow-[0_1px_3px_rgba(24,33,31,.08),0_8px_24px_-12px_rgba(24,33,31,.18)]", p.className)}
      style={{ ["--accent" as string]: accent }}
    >
      {p.watermark && (
        <div className="pointer-events-none absolute inset-0 grid place-items-center">
          <span className="-rotate-[30deg] text-7xl font-black text-[#18211f]/[.06]">{p.watermark}</span>
        </div>
      )}
      {tpl === "modern" && <div className="h-1.5 bg-[var(--accent)]" />}
      <div className="px-[6%] py-[5%]">
        {/* Header */}
        <div className="flex items-start justify-between gap-4 border-b pb-4" style={tpl === "classic" ? { borderColor: "#18211f" } : undefined}>
          <div className="flex min-w-0 items-start gap-3">
            {p.logoUrl && <img src={p.logoUrl} alt="" className="size-14 object-contain" />}
            <div className="min-w-0">
              <div className="text-base font-bold">{p.seller.companyName || t("نام کسب‌وکار")}</div>
              {p.seller.legalName && <div className="text-[#66706c]">{p.seller.legalName}</div>}
              <div className="text-[10px] text-[#66706c]">
                {fa([p.seller.economicCode && t("کد اقتصادی: {0}", [p.seller.economicCode]), p.seller.nationalId && t("شناسه ملی: {0}", [p.seller.nationalId])].filter(Boolean).join("  "))}
              </div>
            </div>
          </div>
          <div className="shrink-0 text-end">
            <div className="text-xl font-bold" style={{ color: tpl === "minimal" ? undefined : accent }}>{p.title}</div>
            <div><span className="text-[#66706c]">{t("شماره:")} </span><span dir="ltr" className="font-semibold">{p.number}</span></div>
            {p.meta.map((m) => (
              <div key={m.label}><span className="text-[#66706c]">{m.label}: </span><span className="num font-semibold">{m.value}</span></div>
            ))}
          </div>
        </div>

        {/* Parties */}
        <div className="mt-4 grid grid-cols-2 gap-3">
          <Party tpl={tpl} title={t("فروشنده")} lines={[
            seg([p.seller.companyName]), seg([p.seller.address]),
            seg([t("کد پستی"), p.seller.postalCode, true], [t("تلفن"), p.seller.phone, true]), seg([null, p.seller.email, true]),
          ]} />
          <Party tpl={tpl} title={t("خریدار")} lines={p.customer ? [
            seg([p.customer.name]), seg([p.customer.companyName]), seg([[p.customer.city, p.customer.address].filter(Boolean).join(LIST_SEP)]),
            seg([t("کد پستی"), p.customer.postalCode, true], [t("تلفن"), p.customer.mobile || p.customer.phone, true]),
            seg([t("کد اقتصادی"), p.customer.taxId, true], [t("شناسه/کد ملی"), p.customer.nationalId, true]),
            seg([null, p.customer.email, true]),
          ] : [t("مشتری انتخاب نشده")]} />
        </div>

        {/* Lines */}
        <table className="mt-4 w-full border-collapse">
          <thead>
            <tr className={cn(tpl === "modern" && "text-white", tpl === "classic" && "bg-[#f3f5f3]")} style={tpl === "modern" ? { background: accent } : undefined}>
              {["#", t("شرح کالا / خدمت"), t("تعداد"), t("واحد"), t("قیمت واحد"), t("تخفیف"), t("مبلغ")].map((h, i) => (
                <th key={h} className={cn("px-1.5 py-1.5 text-[10px] font-semibold", i >= 2 ? "text-end" : "text-start", tpl === "classic" && "border border-[#18211f]", tpl === "minimal" && "border-b-2 border-[#18211f]")}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {p.items.length === 0 && (
              <tr><td colSpan={7} className="py-6 text-center text-[#66706c]">{t("ردیفی اضافه نشده است.")}</td></tr>
            )}
            {p.items.map((l, i) => {
              const total = p.totals?.lineTotals[i]
              const disc = p.totals?.lineDiscounts[i] ?? 0
              return (
                <tr key={i} className={cn(tpl === "modern" && i % 2 === 1 && "bg-[#f3f5f3]")}>
                  {[
                    <span className="text-[#66706c]">{fa(i + 1)}</span>,
                    <div>
                      <div className="whitespace-pre-wrap">{l.description || <span className="text-[#66706c]">—</span>}{exempt && !l.isTaxable ? " *" : ""}</div>
                      {l.sku && <div className="text-[9px] text-[#66706c]" dir="ltr">{l.sku}</div>}
                    </div>,
                    <span className="num">{formatQuantity(Number(l.quantity) || 0)}</span>,
                    l.unit,
                    <span className="num">{formatAmount(l.unitPrice, p.currency)}</span>,
                    <span className="num text-[#66706c]">{disc > 0 ? formatAmount(disc, p.currency) : "—"}</span>,
                    <span className="num font-semibold">{total === undefined ? "…" : formatAmount(total, p.currency)}</span>,
                  ].map((cell, ci) => (
                    <td key={ci} className={cn("px-1.5 py-1.5 align-top", ci >= 2 && "text-end", tpl === "classic" ? "border border-[#d9ddd9]" : "border-b border-[#d9ddd9]")}>{cell}</td>
                  ))}
                </tr>
              )
            })}
          </tbody>
        </table>

        {/* Totals */}
        <div className="mt-4 flex items-start justify-between gap-6">
          <div className="text-[10px] text-[#66706c]">{exempt && t("ردیف‌های علامت‌دار (*) معاف از مالیات هستند.")}</div>
          <div className="w-[46%] shrink-0">
            <Row label={t("جمع ردیف‌ها")} value={formatMoney(p.totals?.subtotal ?? 0, p.currency)} />
            {(p.totals?.discountAmount ?? 0) > 0 && (
              <Row label={p.discountType === "Percent" ? t("تخفیف ({0}٪)", [fa(p.discountValue)]) : t("تخفیف")} value={`− ${formatMoney(p.totals!.discountAmount, p.currency)}`} />
            )}
            {p.taxRate > 0 && <Row label={`${p.seller.taxLabel} (${fa(p.taxRate)}${PCT})`} value={formatMoney(p.totals?.taxAmount ?? 0, p.currency)} />}
            <div
              className={cn("mt-1 flex items-center justify-between px-2 py-2 font-semibold", tpl === "modern" && "text-white", tpl === "classic" && "border border-[#18211f]", tpl === "minimal" && "border-t-2")}
              style={tpl === "modern" ? { background: accent } : tpl === "minimal" ? { borderColor: accent } : undefined}
            >
              <span>{t("مبلغ قابل پرداخت")}</span>
              <span className="num text-sm font-bold">{formatMoney(p.totals?.total ?? 0, p.currency)}</span>
            </div>
            {p.extraTotals?.map((r) => <Row key={r.label} label={r.label} value={formatMoney(r.value, p.currency)} strong={r.strong} />)}
          </div>
        </div>

        {bank.length > 0 && (
          <div className="mt-3 border-t border-[#d9ddd9] pt-2">
            <div className="text-[10px] text-[#66706c]">{t("اطلاعات پرداخت")}</div>
            {bank.map((l, i) => <div key={i}>{l}</div>)}
          </div>
        )}
        {p.paymentTerms && <Box title={t("شرایط پرداخت")} lines={[p.paymentTerms]} />}
        {p.notes && <Box title={t("توضیحات")} lines={[p.notes]} />}
      </div>
    </div>
  )
}

function Party({ title, lines, tpl }: { title: string; lines: ReactNode[]; tpl: string }) {
  const shown = lines.filter(Boolean)
  return (
    <div className={cn("p-2.5", tpl === "modern" && "bg-[color-mix(in_oklab,var(--accent)_10%,white)]", tpl === "classic" && "border border-[#18211f]", tpl === "minimal" && "border-t border-[#d9ddd9] px-0")}>
      <div className="text-[10px] text-[#66706c]">{title}</div>
      {shown.map((l, i) => <div key={i} className={i === 0 ? "text-[13px] font-semibold" : undefined}>{l}</div>)}
    </div>
  )
}

function Row({ label, value, strong }: { label: string; value: string; strong?: boolean }) {
  return (
    <div className="flex items-center justify-between border-b border-[#d9ddd9] px-2 py-1">
      <span className={strong ? "font-semibold" : "text-[#66706c]"}>{label}</span>
      <span className={cn("num", strong && "font-bold")}>{value}</span>
    </div>
  )
}

function Box({ title, lines }: { title: string; lines: string[] }) {
  return (
    <div className="mt-3 border-t border-[#d9ddd9] pt-2">
      <div className="text-[10px] text-[#66706c]">{title}</div>
      {lines.map((l, i) => <div key={i} className="whitespace-pre-wrap">{fa(l)}</div>)}
    </div>
  )
}

export function paperDate(iso: string | null | undefined) {
  return formatDate(iso)
}

type Part = [label: string | null | undefined, value?: string | null, ltr?: boolean]

/**
 * Builds one line from parts. A part with only a label is plain text; [label, value] prints "label: value".
 * Numbers such as phones, card numbers and IBANs are isolated left-to-right so their digits stay in order.
 */
function seg(...parts: Part[]): ReactNode {
  // One element = plain text; two or more = "label: value" (skipped when the value is empty).
  const shown = parts.filter((part) => (part.length === 1 ? !!part[0] : !!part[1]))
  if (shown.length === 0) return null
  return (
    <>
      {shown.map((part, i) => {
        const [label, value, ltr] = part
        return (
        <span key={i}>
          {i > 0 && <span className="text-[#66706c]"> — </span>}
          {part.length === 1 ? fa(label) : !label ? <bdi dir="ltr">{value}</bdi> : (
            <>
              <span className="text-[#66706c]">{label}: </span>
              {ltr ? <bdi dir="ltr" className="num">{fa(value)}</bdi> : fa(value)}
            </>
          )}
        </span>
        )
      })}
    </>
  )
}
