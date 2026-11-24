import { useQuery } from "@tanstack/react-query"
import { api } from "@/services/api"
import type { InvoiceListItem } from "@/features/invoices/api"

export interface Summary {
  issuedCount: number; draftCount: number; totalInvoiced: number; totalReceived: number; outstanding: number
  overdueCount: number; overdueAmount: number; monthInvoiced: number; monthReceived: number; monthLabel: string
  customerCount: number; dueSoonCount: number; dueSoonAmount: number
}
export interface RevenuePoint { key: string; label: string; start: string; end: string; invoiced: number; tax: number; received: number; invoiceCount: number }
export interface CustomerRow { customerId: number; customerName: string; invoiceCount: number; invoiced: number; paid: number; balance: number; overdue: number }

/** `currency` is an ISO code; empty means the server's default currency. Figures are never mixed across currencies. */
export const useSummary = (currency = "") =>
  useQuery({ queryKey: ["dashboard", "summary", currency], queryFn: () => api.get<Summary>("/dashboard/summary", { currency }) })
export const useMonthly = (months = 12, currency = "") =>
  useQuery({ queryKey: ["dashboard", "revenue", months, currency], queryFn: () => api.get<RevenuePoint[]>("/dashboard/revenue", { months, currency }) })
export const useOverdue = () => useQuery({ queryKey: ["dashboard", "overdue"], queryFn: () => api.get<InvoiceListItem[]>("/dashboard/overdue") })
export const useRevenue = (from: string, to: string, groupBy: string, currency = "") =>
  useQuery({ queryKey: ["reports", "revenue", from, to, groupBy, currency], queryFn: () => api.get<RevenuePoint[]>("/reports/revenue", { from, to, groupBy, currency }), placeholderData: (p) => p })
export const useCustomerReport = (from: string, to: string, currency = "") =>
  useQuery({ queryKey: ["reports", "customers", from, to, currency], queryFn: () => api.get<CustomerRow[]>("/reports/customers", { from, to, currency }), placeholderData: (p) => p })
