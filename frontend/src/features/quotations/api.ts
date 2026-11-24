import { useQuery } from "@tanstack/react-query"
import { api } from "@/services/api"
import type { Paged } from "@/services/types"
import { t } from "@/i18n"
import type { Currency, CustomerSnapshot, DiscountType, DocumentForm, Line, SellerSnapshot } from "@/features/documents/types"

export type QuotationStatus = "Draft" | "Sent" | "Accepted" | "Rejected" | "Converted"

export interface QuotationListItem {
  id: number; quotationNumber: string; status: QuotationStatus; isExpired: boolean; customerId: number; customerName: string
  issueDate: string; validUntil: string | null; total: number; currency: Currency; convertedInvoiceId: number | null
}

export interface Quotation {
  id: number; quotationNumber: string; status: QuotationStatus; isExpired: boolean; customerId: number
  customer: CustomerSnapshot; seller: SellerSnapshot; issueDate: string; validUntil: string | null; currency: Currency
  discountType: DiscountType; discountValue: number; taxRate: number; subtotal: number; discountAmount: number
  taxableAmount: number; taxAmount: number; total: number; notes: string | null; paymentTerms: string | null
  templateKey: string; items: Line[]; convertedInvoiceId: number | null; convertedInvoiceNumber: string | null
  acceptedAtUtc: string | null; createdAtUtc: string; updatedAtUtc: string
}

export const STATUS_LABEL: Record<QuotationStatus, string> = {
  Draft: t("پیش‌نویس"), Sent: t("ارسال‌شده"), Accepted: t("تأییدشده"), Rejected: t("ردشده"), Converted: t("تبدیل به فاکتور"),
}

export function useQuotations(f: { search?: string; status?: QuotationStatus }) {
  return useQuery({ queryKey: ["quotations", "list", f], queryFn: () => api.get<Paged<Quotation & QuotationListItem>>("/quotations", { ...f, pageSize: 500 }), placeholderData: (p) => p })
}

export function useQuotation(id: number | undefined) {
  return useQuery({ queryKey: ["quotations", "one", id], queryFn: () => api.get<Quotation>(`/quotations/${id}`), enabled: !!id })
}

/** Body of POST/PUT /quotations. `currencyCode` null = default currency. */
export interface QuotationInput {
  customerId: number; issueDate: string; validUntil: string | null; currencyCode?: string | null; discountType: DiscountType
  discountValue: number; taxRate: number; notes: string; paymentTerms: string; templateKey: string; items: DocumentForm["items"]
}

export function toQuotationInput(v: DocumentForm): QuotationInput {
  return {
    currencyCode: v.currencyCode || null,
    customerId: v.customerId, issueDate: v.issueDate, validUntil: v.endDate, discountType: v.discountType,
    discountValue: Number(v.discountValue) || 0, taxRate: Number(v.taxRate) || 0, notes: v.notes, paymentTerms: v.paymentTerms,
    templateKey: v.templateKey,
    items: v.items.map((l) => ({ ...l, quantity: Number(l.quantity), unitPrice: Number(l.unitPrice), discountValue: Number(l.discountValue) || 0 })),
  }
}

export function fromQuotation(q: Quotation): DocumentForm {
  return {
    customerId: q.customerId, issueDate: q.issueDate, endDate: q.validUntil, discountType: q.discountType, discountValue: q.discountValue,
    taxRate: q.taxRate, notes: q.notes ?? "", paymentTerms: q.paymentTerms ?? "", templateKey: q.templateKey, currencyCode: q.currency.currencyCode,
    items: q.items.map(({ productId, description, sku, unit, quantity, unitPrice, discountType, discountValue, isTaxable }) => ({
      productId, description, sku, unit, quantity, unitPrice, discountType, discountValue, isTaxable,
    })),
  }
}
