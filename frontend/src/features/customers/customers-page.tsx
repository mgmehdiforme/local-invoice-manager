import { t } from "@/i18n"
import { useState } from "react"
import { useNavigate } from "react-router"
import { Archive, ArchiveRestore, MoreHorizontal, Pencil, Plus, Users } from "lucide-react"
import { toast } from "sonner"
import { Button } from "@/components/ui/button"
import { Switch } from "@/components/ui/switch"
import { Label } from "@/components/ui/label"
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from "@/components/ui/dropdown-menu"
import { PageHeader } from "@/components/page-header"
import { SearchInput } from "@/components/search-input"
import { DataTable, StopRowClick } from "@/components/data-table"
import { EmptyState } from "@/components/empty-state"
import { Money } from "@/components/money"
import { StatusBadge } from "@/components/status-badge"
import { useDebounced } from "@/hooks/use-debounced"
import { errorMessage } from "@/services/api"
import { fa } from "@/lib/format"
import { useCustomers, useSetCustomerActive, type Customer } from "./api"
import { CustomerFormDialog } from "./customer-form-dialog"

export default function CustomersPage() {
  const navigate = useNavigate()
  const [search, setSearch] = useState("")
  const [showArchived, setShowArchived] = useState(false)
  const [editing, setEditing] = useState<Customer | null>(null)
  const q = useDebounced(search)
  const { data, isLoading } = useCustomers({ search: q, includeInactive: showArchived })
  const setActive = useSetCustomerActive()

  const toggleActive = async (c: Customer) => {
    try {
      await setActive.mutateAsync({ id: c.id, active: !c.isActive })
      toast.success(c.isActive ? t("مشتری بایگانی شد.") : t("مشتری بازگردانده شد."))
    } catch (e) {
      toast.error(errorMessage(e))
    }
  }

  return (
    <>
      <PageHeader
        title={t("مشتریان")}
        description={data ? t("{0} مشتری", [fa(data.total)]) : undefined}
        actions={
          <CustomerFormDialog
            trigger={<Button><Plus /> {t("مشتری جدید")}</Button>}
            onSaved={(c) => navigate(`/customers/${c.id}`)}
          />
        }
      />
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <SearchInput value={search} onChange={setSearch} placeholder={t("نام، موبایل، ایمیل یا کد اقتصادی")} />
        <div className="flex items-center gap-2">
          <Switch id="archived" checked={showArchived} onCheckedChange={setShowArchived} />
          <Label htmlFor="archived" className="text-sm text-muted-foreground">{t("نمایش بایگانی‌شده‌ها")}</Label>
        </div>
      </div>
      <DataTable
        rows={data?.items}
        loading={isLoading}
        rowKey={(c) => c.id}
        onRowClick={(c) => navigate(`/customers/${c.id}`)}
        rowClassName={(c) => (c.isActive ? undefined : "opacity-60")}
        empty={
          <EmptyState
            icon={Users}
            title={q ? t("مشتری‌ای با این مشخصات پیدا نشد.") : t("هنوز مشتری‌ای ثبت نکرده‌اید.")}
            description={q ? t("عبارت دیگری را جستجو کنید.") : t("اولین مشتری را اضافه کنید تا بتوانید برایش فاکتور صادر کنید.")}
            action={!q && <CustomerFormDialog trigger={<Button><Plus /> {t("مشتری جدید")}</Button>} />}
          />
        }
        columns={[
          {
            key: "name",
            header: t("نام"),
            cell: (c) => (
              <div>
                <div className="font-medium">{c.name}</div>
                {c.companyName && <div className="text-xs text-muted-foreground">{c.companyName}</div>}
              </div>
            ),
          },
          { key: "contact", header: t("تماس"), hideOnMobile: true, cell: (c) => <span className="num" dir="ltr">{fa(c.mobile || c.phone || "")}</span> },
          { key: "invoices", header: t("فاکتورها"), hideOnMobile: true, align: "end", cell: (c) => <span className="num">{fa(c.invoiceCount)}</span> },
          {
            key: "balance",
            header: t("مانده بدهی"),
            align: "end",
            cell: (c) =>
              c.balance > 0 ? (
                <div className="flex flex-col items-end gap-1">
                  <Money value={c.balance} className="font-medium" />
                  {c.overdueAmount > 0 && <StatusBadge tone="overdue">{t("سررسید گذشته")}</StatusBadge>}
                </div>
              ) : (
                <span className="text-muted-foreground">{t("تسویه")}</span>
              ),
          },
          {
            key: "actions",
            header: <span className="sr-only">{t("عملیات")}</span>,
            className: "w-10",
            cell: (c) => (
              <StopRowClick>
                <DropdownMenu>
                  <DropdownMenuTrigger asChild>
                    <Button variant="ghost" size="icon" aria-label={t("عملیات")}><MoreHorizontal /></Button>
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end">
                    <DropdownMenuItem onClick={() => setEditing(c)}><Pencil /> {t("ویرایش")}</DropdownMenuItem>
                    <DropdownMenuItem onClick={() => toggleActive(c)}>
                      {c.isActive ? <><Archive /> {t("بایگانی")}</> : <><ArchiveRestore /> {t("بازگرداندن")}</>}
                    </DropdownMenuItem>
                  </DropdownMenuContent>
                </DropdownMenu>
              </StopRowClick>
            ),
          },
        ]}
      />
      <CustomerFormDialog customer={editing ?? undefined} open={!!editing} onOpenChange={(o) => !o && setEditing(null)} />
    </>
  )
}
