import { t } from "@/i18n"
import { useRef, useState } from "react"
import { useQuery, useQueryClient } from "@tanstack/react-query"
import { DatabaseBackup, Download, RotateCcw, ShieldCheck, Trash2, Upload } from "lucide-react"
import { toast } from "sonner"
import { Button } from "@/components/ui/button"
import { Spinner } from "@/components/ui/spinner"
import { ConfirmDialog } from "@/components/confirm-dialog"
import { StatusBadge } from "@/components/status-badge"
import { api, downloadFile, errorMessage } from "@/services/api"
import { fa, formatBytes, formatDateTime } from "@/lib/format"

interface BackupInfo { fileName: string; sizeBytes: number; createdAtUtc: string; kind: string | null; manifestOk: boolean }
interface VerifyResult { isValid: boolean; problems: string[]; invoiceCount: number; customerCount: number }

const KIND: Record<string, string> = { Manual: t("دستی"), Scheduled: t("خودکار"), PreRestore: t("قبل از بازیابی"), PreMigration: t("قبل از به‌روزرسانی") }

export function BackupPanel() {
  const qc = useQueryClient()
  const { data, isLoading } = useQuery({ queryKey: ["backups"], queryFn: () => api.get<{ items: BackupInfo[] }>("/backup") })
  const [busy, setBusy] = useState<string | null>(null)
  const file = useRef<HTMLInputElement>(null)

  const run = async (key: string, fn: () => Promise<void>) => {
    setBusy(key)
    try { await fn() } catch (e) { toast.error(errorMessage(e)) } finally { setBusy(null) }
  }

  const create = () => run("create", async () => {
    await api.post("/backup")
    await qc.invalidateQueries({ queryKey: ["backups"] })
    toast.success(t("پشتیبان ساخته شد."))
  })

  const verify = (name: string) => run(name, async () => {
    const r = await api.post<VerifyResult>(`/backup/${encodeURIComponent(name)}/verify`)
    if (r.isValid) toast.success(t("پشتیبان سالم است: {0} فاکتور، {1} مشتری.", [fa(r.invoiceCount), fa(r.customerCount)]))
    else toast.error(r.problems.join(" "))
  })

  const afterRestore = async (r: VerifyResult) => {
    toast.success(t("اطلاعات بازیابی شد ({0} فاکتور). صفحه دوباره بارگذاری می‌شود…", [fa(r.invoiceCount)]))
    setTimeout(() => window.location.reload(), 1500)
  }

  const restoreUpload = (f: File) => run("upload", async () => {
    const form = new FormData()
    form.append("file", f)
    const check = await api.upload<VerifyResult>("/backup/verify", form)
    if (!check.isValid) { toast.error(check.problems.join(" ")); return }
    if (!window.confirm(t("این پشتیبان {0} فاکتور و {1} مشتری دارد. اطلاعات فعلی جایگزین شود؟ (قبلش یک پشتیبان ایمنی گرفته می‌شود)", [fa(check.invoiceCount), fa(check.customerCount)]))) return
    const form2 = new FormData()
    form2.append("file", f)
    await afterRestore(await api.upload<VerifyResult>("/backup/restore", form2))
  })

  return (
    <section className="rounded-xl border bg-paper p-5">
      <div className="mb-4 flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="font-semibold">{t("نسخه‌های پشتیبان")}</h2>
          <p className="mt-0.5 text-sm text-muted-foreground">
            {t("هر پشتیبان شامل پایگاه داده، لوگو و تنظیمات است و با هش SHA-256 بررسی می‌شود. پوشه پشتیبان روی همین رایانه است؛ برای امنیت بیشتر گاهی یک نسخه را روی دیسک دیگری کپی کنید.")}
          </p>
        </div>
        <div className="flex gap-2">
          <input ref={file} type="file" accept=".zip,application/zip" hidden onChange={(e) => { const f = e.target.files?.[0]; if (f) restoreUpload(f); e.target.value = "" }} />
          <Button type="button" variant="outline" disabled={!!busy} onClick={() => file.current?.click()}>{busy === "upload" ? <Spinner /> : <Upload />} {t("بازیابی از فایل")}</Button>
          <Button type="button" disabled={!!busy} onClick={create}>{busy === "create" ? <Spinner /> : <DatabaseBackup />} {t("پشتیبان‌گیری الان")}</Button>
        </div>
      </div>
      {isLoading ? <Spinner /> : !data?.items.length ? (
        <p className="py-6 text-center text-sm text-muted-foreground">{t("هنوز پشتیبانی گرفته نشده است.")}</p>
      ) : (
        <ul className="divide-y rounded-lg border">
          {data.items.map((b) => (
            <li key={b.fileName} className="flex flex-wrap items-center justify-between gap-3 px-3 py-2.5">
              <div className="min-w-0">
                <div className="flex items-center gap-2 text-sm">
                  <span className="num">{formatDateTime(b.createdAtUtc)}</span>
                  <StatusBadge tone={b.manifestOk ? "muted" : "overdue"}>{b.manifestOk ? KIND[b.kind ?? ""] ?? b.kind : t("نامعتبر")}</StatusBadge>
                </div>
                <div className="text-xs text-muted-foreground"><bdi dir="ltr">{b.fileName}</bdi> — {formatBytes(b.sizeBytes)}</div>
              </div>
              <div className="flex gap-1">
                <Button type="button" variant="ghost" size="sm" disabled={!!busy} onClick={() => verify(b.fileName)}>{busy === b.fileName ? <Spinner /> : <ShieldCheck />} {t("بررسی")}</Button>
                <Button type="button" variant="ghost" size="sm" onClick={() => downloadFile(`/backup/${encodeURIComponent(b.fileName)}`, b.fileName).catch((e) => toast.error(errorMessage(e)))}><Download /> {t("دانلود")}</Button>
                <ConfirmDialog
                  trigger={<Button type="button" variant="ghost" size="sm" disabled={!!busy}><RotateCcw /> {t("بازیابی")}</Button>}
                  title={t("بازیابی این نسخه؟")}
                  description={t("همه اطلاعات فعلی با این نسخه جایگزین می‌شود. قبل از آن به‌طور خودکار یک پشتیبان ایمنی گرفته می‌شود تا در صورت اشتباه برگردید.")}
                  confirmLabel={t("بازیابی")} destructive
                  onConfirm={() => run("restore", async () => afterRestore(await api.post<VerifyResult>("/backup/restore-stored", { fileName: b.fileName })))}
                />
                <ConfirmDialog
                  trigger={<Button type="button" variant="ghost" size="icon" aria-label={t("حذف")}><Trash2 /></Button>}
                  title={t("این پشتیبان حذف شود؟")} description={t("فایل پشتیبان برای همیشه حذف می‌شود.")} confirmLabel={t("حذف")} destructive
                  onConfirm={() => run("delete", async () => { await api.del(`/backup/${encodeURIComponent(b.fileName)}`); await qc.invalidateQueries({ queryKey: ["backups"] }) })}
                />
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
