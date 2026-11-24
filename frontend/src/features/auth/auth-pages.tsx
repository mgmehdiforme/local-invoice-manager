import { useState } from "react"
import { useQueryClient } from "@tanstack/react-query"
import { LockKeyhole } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Spinner } from "@/components/ui/spinner"
import { api, errorMessage } from "@/services/api"
import { authKey } from "@/hooks/use-auth"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { CURRENCY_PRESETS } from "@/lib/currencies"
import { i18n, syncPrefs, t, type CalendarKind, type Lang } from "@/i18n"

/** Language before sign-in is local only: store it and reload so every label re-renders. */
function switchLocalLanguage(language: Lang) {
  if (syncPrefs({ language, calendar: language === "fa" ? "Jalali" : "Gregorian" })) window.location.reload()
}

const PERSIAN_NAME = "فارسی" // i18n-ignore

/**
 * Sign-in shows the last language used; once set up, the language is the app setting (header switch after
 * signing in). The first-run screen has its own language select.
 */
function AuthFrame({ title, description, children }: { title: string; description: string; children: React.ReactNode }) {
  return (
    <div className="grid min-h-svh place-items-center bg-background px-4">
      <div className="w-full max-w-sm">
        <div className="mb-6 flex items-center gap-3">
          <div className="grid size-11 place-items-center rounded-lg bg-brand text-white">
            <LockKeyhole className="size-5" />
          </div>
          <div>
            <h1 className="text-xl font-bold">{title}</h1>
            <p className="text-sm text-muted-foreground">{description}</p>
          </div>
        </div>
        <div className="rounded-xl border bg-paper p-6 shadow-sm">{children}</div>
        <p className="mt-4 text-center text-xs text-muted-foreground">{t("برنامه فقط روی همین رایانه اجرا می‌شود و اطلاعات از آن خارج نمی‌شود.")}</p>
      </div>
    </div>
  )
}

export function LoginPage() {
  const qc = useQueryClient()
  const [password, setPassword] = useState("")
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    setBusy(true)
    setError(null)
    try {
      await api.post("/auth/login", { password })
      await qc.invalidateQueries({ queryKey: authKey })
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <AuthFrame title={t("ورود")} description={t("برای ادامه رمز عبور را وارد کنید.")}>
      <form onSubmit={submit} className="grid gap-4">
        <div className="grid gap-2">
          <Label htmlFor="password">{t("رمز عبور")}</Label>
          <Input id="password" type="password" autoFocus autoComplete="current-password" dir="ltr" value={password} onChange={(e) => setPassword(e.target.value)} aria-invalid={!!error} />
          {error && <p className="text-sm text-destructive" role="alert">{error}</p>}
        </div>
        <Button type="submit" disabled={busy || !password}>
          {busy && <Spinner />} {t("ورود")}
        </Button>
      </form>
    </AuthFrame>
  )
}

export function SetupPage() {
  const qc = useQueryClient()
  const [password, setPassword] = useState("")
  const [confirm, setConfirm] = useState("")
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [calendar, setCalendar] = useState<CalendarKind>(i18n.lang === "fa" ? "Jalali" : "Gregorian")
  const [currencyCode, setCurrencyCode] = useState(i18n.lang === "fa" ? "IRR" : "USD")
  const mismatch = confirm.length > 0 && confirm !== password

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (password !== confirm) return
    setBusy(true)
    setError(null)
    try {
      await api.post("/auth/setup", { password, language: i18n.lang, calendar, currencyCode })
      syncPrefs({ language: i18n.lang, calendar })
      await qc.invalidateQueries({ queryKey: authKey })
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <AuthFrame title={t("خوش آمدید")} description={t("زبان، تقویم و ارز را انتخاب کنید و یک رمز عبور برای قفل برنامه بگذارید.")}>
      <form onSubmit={submit} className="grid gap-4">
        <div className="grid grid-cols-2 gap-3">
          <div className="grid gap-2">
            <Label htmlFor="setup-language">{t("زبان")}</Label>
            <Select value={i18n.lang} onValueChange={(v) => switchLocalLanguage(v as Lang)} dir={i18n.dir}>
              <SelectTrigger id="setup-language" className="w-full"><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="en">English</SelectItem>
                <SelectItem value="fa">{PERSIAN_NAME}</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div className="grid gap-2">
            <Label htmlFor="setup-calendar">{t("تقویم")}</Label>
            <Select value={calendar} onValueChange={(v) => setCalendar(v as CalendarKind)} dir={i18n.dir}>
              <SelectTrigger id="setup-calendar" className="w-full"><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="Gregorian">{t("میلادی")}</SelectItem>
                <SelectItem value="Jalali">{t("شمسی")}</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </div>
        <div className="grid gap-2">
          <Label htmlFor="setup-currency">{t("ارز پیش‌فرض")}</Label>
          <Select value={currencyCode} onValueChange={setCurrencyCode} dir={i18n.dir}>
            <SelectTrigger id="setup-currency" className="w-full"><SelectValue /></SelectTrigger>
            <SelectContent>
              {CURRENCY_PRESETS.map((c) => (
                <SelectItem key={c.code} value={c.code}>
                  {c.code} · {c.symbol}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <p className="text-xs text-muted-foreground">{t("ارزهای دیگر را بعداً در تنظیمات اضافه کنید.")}</p>
        </div>
        <div className="grid gap-2">
          <Label htmlFor="new-password">{t("رمز عبور")}</Label>
          <Input id="new-password" type="password" autoFocus autoComplete="new-password" dir="ltr" value={password} onChange={(e) => setPassword(e.target.value)} />
          <p className="text-xs text-muted-foreground">{t("حداقل ۸ کاراکتر. این رمز را جایی امن نگه دارید.")}</p>
        </div>
        <div className="grid gap-2">
          <Label htmlFor="confirm-password">{t("تکرار رمز عبور")}</Label>
          <Input id="confirm-password" type="password" autoComplete="new-password" dir="ltr" value={confirm} onChange={(e) => setConfirm(e.target.value)} aria-invalid={mismatch} />
          {mismatch && <p className="text-sm text-destructive">{t("رمزها یکسان نیستند.")}</p>}
        </div>
        {error && <p className="text-sm text-destructive" role="alert">{error}</p>}
        <Button type="submit" disabled={busy || password.length < 8 || password !== confirm}>
          {busy && <Spinner />} {t("ذخیره رمز و ورود")}
        </Button>
      </form>
    </AuthFrame>
  )
}
