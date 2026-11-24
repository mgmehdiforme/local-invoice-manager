import { useEffect, type ReactNode } from "react"
import { useQueryClient } from "@tanstack/react-query"
import { Spinner } from "@/components/ui/spinner"
import { authKey, useAuthStatus } from "@/hooks/use-auth"
import { onUnauthorized } from "@/services/api"
import { LoginPage, SetupPage } from "@/features/auth/auth-pages"
import { applyPrefsAndReload, t } from "@/i18n"

/** Shows setup / login until the user is signed in. Any 401 from the API sends the user back to login. */
export function AuthGate({ children }: { children: ReactNode }) {
  const qc = useQueryClient()
  const { data, isLoading, isError, refetch } = useAuthStatus()

  useEffect(() => {
    onUnauthorized(() => {
      qc.setQueryData(authKey, (old: unknown) => (old ? { ...(old as object), isAuthenticated: false } : old))
    })
  }, [qc])

  // Once set up, the server's language and calendar win; reload once if the cached ones differ.
  // Before setup the choice on the setup screen is local, so it must not be overridden.
  useEffect(() => {
    if (data?.isSetUp) applyPrefsAndReload({ language: data.language, calendar: data.calendar })
  }, [data])

  if (isLoading)
    return (
      <div className="grid min-h-svh place-items-center">
        <Spinner className="size-6" />
      </div>
    )
  if (isError || !data)
    return (
      <div className="grid min-h-svh place-items-center p-6 text-center">
        <div>
          <p className="font-semibold">{t("ارتباط با برنامه برقرار نشد.")}</p>
          <p className="mt-1 text-sm text-muted-foreground">{t("مطمئن شوید برنامه در حال اجراست.")}</p>
          <button className="mt-4 text-sm text-brand underline" onClick={() => refetch()}>{t("تلاش دوباره")}</button>
        </div>
      </div>
    )
  if (!data.isSetUp) return <SetupPage />
  if (!data.isAuthenticated) return <LoginPage />
  return <>{children}</>
}
