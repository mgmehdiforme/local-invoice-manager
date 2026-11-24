import { lazy, Suspense } from "react"
import { createBrowserRouter, Link } from "react-router"
import { AppLayout } from "@/layouts/app-layout"
import { Spinner } from "@/components/ui/spinner"
import { t } from "@/i18n"

const DashboardPage = lazy(() => import("@/features/dashboard/dashboard-page"))
const CustomersPage = lazy(() => import("@/features/customers/customers-page"))
const CustomerDetailPage = lazy(() => import("@/features/customers/customer-detail-page"))
const ProductsPage = lazy(() => import("@/features/products/products-page"))
const SettingsPage = lazy(() => import("@/features/settings/settings-page"))
const InvoicesPage = lazy(() => import("@/features/invoices/invoices-page"))
const InvoiceEditorPage = lazy(() => import("@/features/invoices/invoice-editor-page"))
const InvoiceDetailPage = lazy(() => import("@/features/invoices/invoice-detail-page"))
const PaymentsPage = lazy(() => import("@/features/payments/payments-page"))
const QuotationsPage = lazy(() => import("@/features/quotations/quotations-page"))
const QuotationEditorPage = lazy(() => import("@/features/quotations/quotation-editor-page"))
const QuotationDetailPage = lazy(() => import("@/features/quotations/quotation-detail-page"))
const ReportsPage = lazy(() => import("@/features/reports/reports-page"))
const RecurringListPage = lazy(() => import("@/features/recurring/recurring-pages").then((m) => ({ default: m.RecurringListPage })))
const RecurringEditorPage = lazy(() => import("@/features/recurring/recurring-pages").then((m) => ({ default: m.RecurringEditorPage })))

function Loading() {
  return (
    <div className="grid place-items-center py-24">
      <Spinner className="size-6" />
    </div>
  )
}

function page(element: React.ReactNode) {
  return <Suspense fallback={<Loading />}>{element}</Suspense>
}

function NotFound() {
  return (
    <div className="py-24 text-center">
      <p className="text-lg font-semibold">{t("این صفحه وجود ندارد.")}</p>
      <Link to="/" className="mt-3 inline-block text-sm text-brand underline">{t("بازگشت به داشبورد")}</Link>
    </div>
  )
}

export const router = createBrowserRouter([
  {
    element: <AppLayout />,
    children: [
      { index: true, element: page(<DashboardPage />) },
      { path: "customers", element: page(<CustomersPage />) },
      { path: "customers/:id", element: page(<CustomerDetailPage />) },
      { path: "products", element: page(<ProductsPage />) },
      { path: "settings", element: page(<SettingsPage />) },
      { path: "invoices", element: page(<InvoicesPage />) },
      { path: "invoices/new", element: page(<InvoiceEditorPage />) },
      { path: "invoices/:id/edit", element: page(<InvoiceEditorPage />) },
      { path: "invoices/:id", element: page(<InvoiceDetailPage />) },
      { path: "payments", element: page(<PaymentsPage />) },
      { path: "quotations", element: page(<QuotationsPage />) },
      { path: "quotations/new", element: page(<QuotationEditorPage />) },
      { path: "quotations/:id/edit", element: page(<QuotationEditorPage />) },
      { path: "quotations/:id", element: page(<QuotationDetailPage />) },
      { path: "reports", element: page(<ReportsPage />) },
      { path: "recurring", element: page(<RecurringListPage />) },
      { path: "recurring/new", element: page(<RecurringEditorPage />) },
      { path: "recurring/:id", element: page(<RecurringEditorPage />) },
      { path: "*", element: <NotFound /> },
    ],
  },
])
