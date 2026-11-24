import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { api } from "@/services/api"
import type { Paged } from "@/services/types"
import { t } from "@/i18n"
import type { Currency, CustomerSnapshot, DiscountType, DocumentForm, Line, SellerSnapshot } from "@/features/documents/types"

export type InvoiceStatus = "Draft" | "Issued" | "Cancelled"
export type PaymentStatus = "Unpaid" | "PartiallyPaid" | "Paid"
export type PaymentMethod = "Cash" | "Card" | "BankTransfer" | "Cheque" | "Other"

export interface Payment {
  id: number
  invoiceId: number
  invoiceNumber?: string | null
  customerName?: string | null
  amount: number
  paymentDate: string
  method: PaymentMethod
  reference?: string | null
  notes?: string | null
  status: "Active" | "Reversed"
  reversedAtUtc?: string | null
  reversalReason?: string | null
  /** Currency of the invoice the payment belongs to. */
  currencyCode: string
  createdAtUtc: string
}

export interface InvoiceListItem {
  id: number
  invoiceNumber: string | null
  status: InvoiceStatus
  paymentStatus: PaymentStatus
  customerId: number
  customerName: string
  issueDate: string
  dueDate: string | null
  total: number
  paidAmount: number
  balance: number
  isOverdue: boolean
  currency: Currency
  updatedAtUtc: string
}

export interface Invoice {
  id: number
  invoiceNumber: string | null
  status: InvoiceStatus
  paymentStatus: PaymentStatus
  customerId: number
  customer: CustomerSnapshot
  seller: SellerSnapshot
  issueDate: string
  dueDate: string | null
  currency: Currency
  discountType: DiscountType
  discountValue: number
  taxRate: number
  subtotal: number
  discountAmount: number
  taxableAmount: number
  taxAmount: number
  total: number
  paidAmount: number
  balance: number
  isOverdue: boolean
  notes: string | null
  paymentTerms: string | null
  templateKey: string
  items: Line[]
  payments: Payment[]
  sourceQuotationId: number | null
  sourceQuotationNumber: string | null
  recurringInvoiceId: number | null
  duplicatedFromId: number | null
  issuedAtUtc: string | null
  cancelledAtUtc: string | null
  cancellationReason: string | null
  createdAtUtc: string
  updatedAtUtc: string
}

export interface InvoiceFilters {
  search?: string
  status?: InvoiceStatus
  paymentStatus?: PaymentStatus
  overdue?: boolean
  unpaid?: boolean
  customerId?: number
  from?: string
  to?: string
  pageSize?: number
}

export const invoiceKeys = { all: ["invoices"] as const }

export function useInvoices(f: InvoiceFilters) {
  return useQuery({
    queryKey: [...invoiceKeys.all, "list", f],
    queryFn: () => api.get<Paged<InvoiceListItem>>("/invoices", { ...f, pageSize: f.pageSize ?? 500 }),
    placeholderData: (prev) => prev,
  })
}

export function useInvoice(id: number | undefined) {
  return useQuery({ queryKey: [...invoiceKeys.all, "one", id], queryFn: () => api.get<Invoice>(`/invoices/${id}`), enabled: !!id })
}

/** Body of POST/PUT /invoices. `currencyCode` null = default currency. */
export interface InvoiceInput {
  customerId: number
  issueDate: string
  dueDate: string | null
  currencyCode?: string | null
  discountType: DiscountType
  discountValue: number
  taxRate: number
  notes: string
  paymentTerms: string
  templateKey: string
  items: DocumentForm["items"]
}

export function toInvoiceInput(v: DocumentForm): InvoiceInput {
  return {
    currencyCode: v.currencyCode || null,
    customerId: v.customerId,
    issueDate: v.issueDate,
    dueDate: v.endDate,
    discountType: v.discountType,
    discountValue: Number(v.discountValue) || 0,
    taxRate: Number(v.taxRate) || 0,
    notes: v.notes,
    paymentTerms: v.paymentTerms,
    templateKey: v.templateKey,
    items: v.items.map((l) => ({ ...l, quantity: Number(l.quantity), unitPrice: Number(l.unitPrice), discountValue: Number(l.discountValue) || 0 })),
  }
}

export function fromInvoice(i: Invoice): DocumentForm {
  return {
    customerId: i.customerId,
    issueDate: i.issueDate,
    endDate: i.dueDate,
    discountType: i.discountType,
    discountValue: i.discountValue,
    taxRate: i.taxRate,
    notes: i.notes ?? "",
    paymentTerms: i.paymentTerms ?? "",
    templateKey: i.templateKey,
    currencyCode: i.currency.currencyCode,
    items: i.items.map(({ productId, description, sku, unit, quantity, unitPrice, discountType, discountValue, isTaxable }) => ({
      productId, description, sku, unit, quantity, unitPrice, discountType, discountValue, isTaxable,
    })),
  }
}

export function useInvoiceAction() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, action, body }: { id: number; action: "issue" | "cancel" | "duplicate"; body?: unknown }) =>
      api.post<Invoice>(`/invoices/${id}/${action}`, body),
    onSuccess: () => qc.invalidateQueries(),
  })
}

export function useDeleteInvoice() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => api.del(`/invoices/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: invoiceKeys.all }),
  })
}

export const PAYMENT_METHODS: Record<PaymentMethod, string> = {
  Cash: t("نقدی"),
  Card: t("کارت‌خوان / کارت به کارت"),
  BankTransfer: t("انتقال بانکی"),
  Cheque: t("چک"),
  Other: t("سایر"),
}
