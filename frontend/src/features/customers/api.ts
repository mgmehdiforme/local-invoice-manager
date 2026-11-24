import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { api } from "@/services/api"
import type { Paged } from "@/services/types"

export interface Customer {
  id: number
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
  notes?: string | null
  isActive: boolean
  createdAtUtc: string
  balance: number
  overdueAmount: number
  invoiceCount: number
}

export type CustomerInput = Omit<Customer, "id" | "isActive" | "createdAtUtc" | "balance" | "overdueAmount" | "invoiceCount">

/** Top-level figures are in the default currency; `byCurrency` lists every currency the customer was invoiced in. */
export interface CustomerBalance {
  totalInvoiced: number
  totalPaid: number
  balance: number
  overdueAmount: number
  invoiceCount: number
  overdueCount: number
  byCurrency: { currencyCode: string; totalInvoiced: number; totalPaid: number; balance: number; overdueAmount: number }[]
}

export const customerKeys = { all: ["customers"] as const }

export function useCustomers(params: { search?: string; includeInactive?: boolean; pageSize?: number }) {
  return useQuery({
    queryKey: [...customerKeys.all, params],
    queryFn: () => api.get<Paged<Customer>>("/customers", { ...params, pageSize: params.pageSize ?? 500 }),
    placeholderData: (prev) => prev,
  })
}

export function useCustomer(id: number | undefined) {
  return useQuery({ queryKey: [...customerKeys.all, "one", id], queryFn: () => api.get<Customer>(`/customers/${id}`), enabled: !!id })
}

export function useCustomerBalance(id: number) {
  return useQuery({ queryKey: [...customerKeys.all, "balance", id], queryFn: () => api.get<CustomerBalance>(`/customers/${id}/balance`) })
}

export function useSaveCustomer() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, input }: { id?: number; input: CustomerInput }) =>
      id ? api.put<Customer>(`/customers/${id}`, input) : api.post<Customer>("/customers", input),
    onSuccess: () => qc.invalidateQueries({ queryKey: customerKeys.all }),
  })
}

export function useSetCustomerActive() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, active }: { id: number; active: boolean }) =>
      active ? api.post(`/customers/${id}/restore`) : api.del(`/customers/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: customerKeys.all }),
  })
}
