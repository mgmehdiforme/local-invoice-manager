import { StrictMode } from "react"
import { createRoot } from "react-dom/client"
import { RouterProvider } from "react-router"
import { QueryClient, QueryClientProvider } from "@tanstack/react-query"
import { Direction } from "radix-ui"
import { TooltipProvider } from "@/components/ui/tooltip"
import { Toaster } from "@/components/ui/sonner"
import { AuthGate } from "@/app/auth-gate"
import { router } from "@/app/router"
import { ApiError } from "@/services/api"
import { applyDocumentLanguage, i18n } from "@/i18n"
import "./index.css"

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      refetchOnWindowFocus: false,
      retry: (count, error) => !(error instanceof ApiError && error.status < 500) && count < 2,
    },
  },
})

applyDocumentLanguage()

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <Direction.Provider dir={i18n.dir}>
      <QueryClientProvider client={queryClient}>
        <TooltipProvider delayDuration={300}>
          <AuthGate>
            <RouterProvider router={router} />
          </AuthGate>
          <Toaster />
        </TooltipProvider>
      </QueryClientProvider>
    </Direction.Provider>
  </StrictMode>,
)
