import { t } from "@/i18n"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { api } from "@/services/api"
import type { Paged } from "@/services/types"

export type ProductKind = "Product" | "Service"

export interface Product {
  id: number
  name: string
  sku?: string | null
  kind: ProductKind
  unit: string
  description?: string | null
  defaultPrice: number
  isTaxable: boolean
  isActive: boolean
  createdAtUtc: string
  customerPrice?: number | null
}

export type ProductInput = Pick<Product, "name" | "sku" | "kind" | "unit" | "description" | "defaultPrice" | "isTaxable">

export const productKeys = { all: ["products"] as const }

export function useProducts(params: { search?: string; includeInactive?: boolean; customerId?: number }) {
  return useQuery({
    queryKey: [...productKeys.all, params],
    queryFn: () => api.get<Paged<Product>>("/products", { ...params, pageSize: 500 }),
    placeholderData: (prev) => prev,
  })
}

export function useSaveProduct() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, input }: { id?: number; input: ProductInput }) =>
      id ? api.put<Product>(`/products/${id}`, input) : api.post<Product>("/products", input),
    onSuccess: () => qc.invalidateQueries({ queryKey: productKeys.all }),
  })
}

export function useSetProductActive() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, active }: { id: number; active: boolean }) => (active ? api.post(`/products/${id}/restore`) : api.del(`/products/${id}`)),
    onSuccess: () => qc.invalidateQueries({ queryKey: productKeys.all }),
  })
}

/** Suggested units, in the UI language (the unit is stored as typed). */
export const UNITS = [t("عدد"), t("ساعت"), t("روز"), t("ماه"), t("پروژه"), t("کیلوگرم"), t("متر"), t("متر مربع"), t("لیتر"), t("بسته"), t("کارتن"), t("سرویس")]
