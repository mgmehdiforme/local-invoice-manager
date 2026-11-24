import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { api } from "@/services/api"
import type { AuthStatus } from "@/services/types"

export const authKey = ["auth", "status"]

export function useAuthStatus() {
  return useQuery({ queryKey: authKey, queryFn: () => api.get<AuthStatus>("/auth/status"), staleTime: 60_000 })
}

export function useLogout() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: () => api.post("/auth/logout"),
    onSettled: () => {
      qc.clear()
      qc.invalidateQueries({ queryKey: authKey })
    },
  })
}
