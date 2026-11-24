import type { CurrencyFormat } from "@/lib/format"
import { t } from "@/i18n"

export type DiscountType = "None" | "Fixed" | "Percent"

export interface LineInput {
  productId?: number | null
  description: string
  sku?: string | null
  unit: string
  quantity: number
  unitPrice: number
  discountType: DiscountType
  discountValue: number
  isTaxable: boolean
}

export interface Line extends LineInput {
  id: number
  lineNo: number
  discountAmount: number
  lineTotal: number
}

export interface Totals {
  lineTotals: number[]
  lineDiscounts: number[]
  subtotal: number
  discountAmount: number
  taxableAmount: number
  taxAmount: number
  total: number
}

export interface Currency extends CurrencyFormat {
  currencyCode: string
}

export interface CustomerSnapshot {
  name: string
  companyName?: string | null
  phone?: string | null
  mobile?: string | null
  email?: string | null
  address?: string | null
  city?: string | null
  postalCode?: string | null
  taxId?: string | null
  nationalId?: string | null
}

export interface SellerSnapshot {
  companyName: string
  legalName?: string | null
  economicCode?: string | null
  nationalId?: string | null
  registrationNumber?: string | null
  phone?: string | null
  email?: string | null
  website?: string | null
  address?: string | null
  postalCode?: string | null
  logoFileId?: number | null
  bankName?: string | null
  bankAccountNumber?: string | null
  bankCardNumber?: string | null
  bankIban?: string | null
  bankAccountHolder?: string | null
  taxLabel: string
  accentColor: string
}

/** Form values shared by the invoice and quotation editors. */
export interface DocumentForm {
  customerId: number
  issueDate: string
  /** Due date (invoice) or valid-until date (quotation). */
  endDate: string | null
  discountType: DiscountType
  discountValue: number
  taxRate: number
  notes: string
  paymentTerms: string
  templateKey: string
  /** Document currency; null/undefined = the default currency. */
  currencyCode?: string | null
  items: LineInput[]
}

export const emptyLine = (): LineInput => ({
  productId: null, description: "", sku: null, unit: t("عدد"), quantity: 1, unitPrice: 0, discountType: "None", discountValue: 0, isTaxable: true,
})

export const TEMPLATE_LABELS: Record<string, string> = { modern: t("مدرن"), classic: t("کلاسیک"), minimal: t("ساده") }
