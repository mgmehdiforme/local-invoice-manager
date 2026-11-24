import { useNavigate } from "react-router"
import { useQuery, useQueryClient } from "@tanstack/react-query"
import { AlertTriangle, Bell, CalendarClock, CheckCheck, Mail, Repeat, ShieldAlert } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover"
import { ScrollArea } from "@/components/ui/scroll-area"
import { api } from "@/services/api"
import { fa, formatDateTime } from "@/lib/format"
import { cn } from "@/lib/utils"
import { t } from "@/i18n"

interface Notification { id: number; type: string; channel: string; invoiceId: number | null; title: string; message: string; status: string; isRead: boolean; createdAtUtc: string }

const ICONS: Record<string, typeof Bell> = { Overdue: AlertTriangle, DueSoon: CalendarClock, InvoiceEmail: Mail, RecurringCreated: Repeat, BackupFailed: ShieldAlert }

/** Reminders, recurring-invoice results and backup problems. Polls the unread count every minute. */
export function NotificationBell() {
  const qc = useQueryClient()
  const navigate = useNavigate()
  const count = useQuery({ queryKey: ["notifications", "count"], queryFn: () => api.get<{ unread: number }>("/notifications/count"), refetchInterval: 60_000 })
  const list = useQuery({ queryKey: ["notifications", "list"], queryFn: () => api.get<Notification[]>("/notifications"), enabled: false })
  const unread = count.data?.unread ?? 0

  const readAll = async () => {
    await api.post("/notifications/read-all")
    qc.invalidateQueries({ queryKey: ["notifications"] })
  }

  return (
    <Popover onOpenChange={(o) => o && list.refetch()}>
      <PopoverTrigger asChild>
        <Button variant="ghost" size="icon" className="relative" aria-label={unread ? t("{0} اعلان خوانده‌نشده", [fa(unread)]) : t("اعلان‌ها")}>
          <Bell />
          {unread > 0 && <span className="absolute -top-0.5 -end-0.5 grid min-w-4 place-items-center rounded-full bg-overdue px-1 text-[10px] font-bold text-white">{fa(Math.min(unread, 99))}</span>}
        </Button>
      </PopoverTrigger>
      <PopoverContent align="end" className="w-96 p-0">
        <div className="flex items-center justify-between border-b px-4 py-2.5">
          <span className="text-sm font-semibold">{t("اعلان‌ها")}</span>
          {unread > 0 && <Button variant="ghost" size="sm" onClick={readAll}><CheckCheck /> {t("خواندن همه")}</Button>}
        </div>
        <ScrollArea className="max-h-96">
          {list.data?.length === 0 && <p className="p-6 text-center text-sm text-muted-foreground">{t("اعلانی نیست.")}</p>}
          <ul>
            {list.data?.map((n) => {
              const Icon = ICONS[n.type] ?? Bell
              return (
                <li key={n.id}>
                  <button
                    className={cn("flex w-full gap-3 border-b px-4 py-3 text-start hover:bg-accent", !n.isRead && "bg-brand-soft/60")}
                    onClick={async () => {
                      if (!n.isRead) { await api.post(`/notifications/${n.id}/read`); qc.invalidateQueries({ queryKey: ["notifications"] }) }
                      if (n.invoiceId) navigate(`/invoices/${n.invoiceId}`)
                    }}
                  >
                    <Icon className={cn("mt-0.5 size-4 shrink-0", n.type === "Overdue" || n.status === "Failed" || n.type === "BackupFailed" ? "text-overdue" : "text-muted-foreground")} />
                    <div className="min-w-0">
                      <div className="text-sm font-medium">{fa(n.title)}</div>
                      {n.message && <div className="text-xs text-muted-foreground">{n.message}</div>}
                      <div className="mt-0.5 text-[11px] text-muted-foreground">{formatDateTime(n.createdAtUtc)}</div>
                    </div>
                  </button>
                </li>
              )
            })}
          </ul>
        </ScrollArea>
      </PopoverContent>
    </Popover>
  )
}
