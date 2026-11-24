import { t } from "@/i18n"
import { useState } from "react"
import { toast } from "sonner"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Spinner } from "@/components/ui/spinner"
import { FormField } from "@/components/form-field"
import { api, errorMessage } from "@/services/api"

/** Separate from the settings form so the password never sits in that form's state. */
export function ChangePasswordCard() {
  const [current, setCurrent] = useState("")
  const [next, setNext] = useState("")
  const [confirm, setConfirm] = useState("")
  const [busy, setBusy] = useState(false)
  const mismatch = confirm.length > 0 && confirm !== next

  const submit = async () => {
    setBusy(true)
    try {
      await api.post("/auth/change-password", { currentPassword: current, newPassword: next })
      toast.success(t("رمز عبور تغییر کرد. نشست‌های دیگر بسته شدند."))
      setCurrent(""); setNext(""); setConfirm("")
    } catch (e) {
      toast.error(errorMessage(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="rounded-xl border bg-paper p-5">
      <h2 className="mb-4 font-semibold">{t("تغییر رمز عبور")}</h2>
      <div className="grid gap-4 sm:grid-cols-3">
        <FormField label={t("رمز فعلی")} htmlFor="pw-cur"><Input id="pw-cur" type="password" dir="ltr" autoComplete="current-password" value={current} onChange={(e) => setCurrent(e.target.value)} /></FormField>
        <FormField label={t("رمز جدید")} htmlFor="pw-new" hint={t("حداقل ۸ کاراکتر")}><Input id="pw-new" type="password" dir="ltr" autoComplete="new-password" value={next} onChange={(e) => setNext(e.target.value)} /></FormField>
        <FormField label={t("تکرار رمز جدید")} htmlFor="pw-conf" error={mismatch ? t("رمزها یکسان نیستند.") : undefined}><Input id="pw-conf" type="password" dir="ltr" autoComplete="new-password" value={confirm} onChange={(e) => setConfirm(e.target.value)} /></FormField>
      </div>
      <Button type="button" className="mt-4" disabled={busy || !current || next.length < 8 || next !== confirm} onClick={submit}>
        {busy && <Spinner />} {t("تغییر رمز")}
      </Button>
    </section>
  )
}
