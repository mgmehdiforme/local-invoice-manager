import { t } from "@/i18n"
import { useState } from "react"
import { Archive, ArchiveRestore, MoreHorizontal, Package, Pencil, Plus } from "lucide-react"
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
import { useProducts, useSetProductActive, type Product } from "./api"
import { ProductFormDialog } from "./product-form-dialog"

export default function ProductsPage() {
  const [search, setSearch] = useState("")
  const [showArchived, setShowArchived] = useState(false)
  const [editing, setEditing] = useState<Product | null>(null)
  const q = useDebounced(search)
  const { data, isLoading } = useProducts({ search: q, includeInactive: showArchived })
  const setActive = useSetProductActive()

  const toggleActive = async (p: Product) => {
    try {
      await setActive.mutateAsync({ id: p.id, active: !p.isActive })
      toast.success(p.isActive ? t("بایگانی شد. فاکتورهای قبلی تغییری نمی‌کنند.") : t("بازگردانده شد."))
    } catch (e) {
      toast.error(errorMessage(e))
    }
  }

  return (
    <>
      <PageHeader
        title={t("کالاها و خدمات")}
        description={data ? t("{0} مورد", [fa(data.total)]) : undefined}
        actions={<ProductFormDialog trigger={<Button><Plus /> {t("مورد جدید")}</Button>} />}
      />
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <SearchInput value={search} onChange={setSearch} placeholder={t("نام یا کد کالا")} />
        <div className="flex items-center gap-2">
          <Switch id="archived" checked={showArchived} onCheckedChange={setShowArchived} />
          <Label htmlFor="archived" className="text-sm text-muted-foreground">{t("نمایش بایگانی‌شده‌ها")}</Label>
        </div>
      </div>
      <DataTable
        rows={data?.items}
        loading={isLoading}
        rowKey={(p) => p.id}
        onRowClick={(p) => setEditing(p)}
        rowClassName={(p) => (p.isActive ? undefined : "opacity-60")}
        empty={
          <EmptyState
            icon={Package}
            title={q ? t("موردی پیدا نشد.") : t("فهرست کالاها و خدمات خالی است.")}
            description={q ? t("عبارت دیگری را جستجو کنید.") : t("کالاها و خدماتی را که زیاد می‌فروشید اضافه کنید تا فاکتور سریع‌تر پر شود.")}
            action={!q && <ProductFormDialog trigger={<Button><Plus /> {t("مورد جدید")}</Button>} />}
          />
        }
        columns={[
          {
            key: "name",
            header: t("نام"),
            cell: (p) => (
              <div>
                <div className="font-medium">{p.name}</div>
                {p.description && <div className="line-clamp-1 text-xs text-muted-foreground">{p.description}</div>}
              </div>
            ),
          },
          { key: "kind", header: t("نوع"), hideOnMobile: true, cell: (p) => <StatusBadge tone={p.kind === "Service" ? "brand" : "muted"}>{p.kind === "Service" ? t("خدمت") : t("کالا")}</StatusBadge> },
          { key: "sku", header: t("کد"), hideOnMobile: true, cell: (p) => <span dir="ltr" className="num text-muted-foreground">{p.sku ?? "—"}</span> },
          { key: "unit", header: t("واحد"), hideOnMobile: true, cell: (p) => p.unit },
          { key: "tax", header: t("مالیات"), hideOnMobile: true, cell: (p) => (p.isTaxable ? t("مشمول") : <span className="text-muted-foreground">{t("معاف")}</span>) },
          { key: "price", header: t("قیمت"), align: "end", cell: (p) => <Money value={p.defaultPrice} className="font-medium" /> },
          {
            key: "actions",
            header: <span className="sr-only">{t("عملیات")}</span>,
            className: "w-10",
            cell: (p) => (
              <StopRowClick>
                <DropdownMenu>
                  <DropdownMenuTrigger asChild>
                    <Button variant="ghost" size="icon" aria-label={t("عملیات")}><MoreHorizontal /></Button>
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end">
                    <DropdownMenuItem onClick={() => setEditing(p)}><Pencil /> {t("ویرایش")}</DropdownMenuItem>
                    <DropdownMenuItem onClick={() => toggleActive(p)}>
                      {p.isActive ? <><Archive /> {t("بایگانی")}</> : <><ArchiveRestore /> {t("بازگرداندن")}</>}
                    </DropdownMenuItem>
                  </DropdownMenuContent>
                </DropdownMenu>
              </StopRowClick>
            ),
          },
        ]}
      />
      <ProductFormDialog product={editing ?? undefined} open={!!editing} onOpenChange={(o) => !o && setEditing(null)} />
    </>
  )
}
