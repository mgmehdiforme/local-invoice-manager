import { useState } from "react"
import { Copy, Mail, MessageCircle, Share2 } from "lucide-react"
import { toast } from "sonner"
import { Button } from "@/components/ui/button"
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from "@/components/ui/dropdown-menu"
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { Spinner } from "@/components/ui/spinner"
import { FormField } from "@/components/form-field"
import { api, errorMessage } from "@/services/api"
import type { Invoice } from "@/features/invoices/api"
import { t } from "@/i18n"

interface ShareInfo { text: string; whatsappUrl: string | null; phone: string | null }

/** Send an issued invoice: email with PDF attached (needs SMTP), WhatsApp link, or copy a ready-made message. */
export function ShareMenu({ invoice }: { invoice: Invoice }) {
  const [emailOpen, setEmailOpen] = useState(false)

  const share = async (mode: "whatsapp" | "copy") => {
    try {
      const info = await api.get<ShareInfo>(`/invoices/${invoice.id}/share`)
      if (mode === "copy") {
        await navigator.clipboard.writeText(info.text)
        toast.success(t("متن پیام کپی شد."))
      } else {
        window.open(info.whatsappUrl ?? "https://wa.me/", "_blank", "noopener")
        if (!info.phone) toast.info(t("شماره موبایل مشتری ثبت نشده؛ مخاطب را در واتس‌اپ انتخاب کنید."))
      }
    } catch (e) {
      toast.error(errorMessage(e))
    }
  }

  return (
    <>
      <DropdownMenu>
        <DropdownMenuTrigger asChild><Button variant="outline"><Share2 /> {t("ارسال")}</Button></DropdownMenuTrigger>
        <DropdownMenuContent align="end">
          <DropdownMenuItem onClick={() => setEmailOpen(true)}><Mail /> {t("ایمیل با PDF")}</DropdownMenuItem>
          <DropdownMenuItem onClick={() => share("whatsapp")}><MessageCircle /> {t("واتس‌اپ")}</DropdownMenuItem>
          <DropdownMenuItem onClick={() => share("copy")}><Copy /> {t("کپی متن پیام")}</DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
      <EmailDialog invoice={invoice} open={emailOpen} onOpenChange={setEmailOpen} />
    </>
  )
}

function EmailDialog({ invoice, open, onOpenChange }: { invoice: Invoice; open: boolean; onOpenChange: (o: boolean) => void }) {
  const [to, setTo] = useState(invoice.customer.email ?? "")
  const [message, setMessage] = useState("")
  const [busy, setBusy] = useState(false)
  const send = async () => {
    setBusy(true)
    try {
      await api.post(`/invoices/${invoice.id}/email`, { to, message: message || null })
      toast.success(t("فاکتور به {0} ارسال شد.", [to]))
      onOpenChange(false)
    } catch (e) {
      toast.error(errorMessage(e))
    } finally {
      setBusy(false)
    }
  }
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{t("ارسال فاکتور با ایمیل")}</DialogTitle>
          <DialogDescription>{t("PDF فاکتور پیوست می‌شود. برای ارسال، اتصال اینترنت و تنظیمات ایمیل لازم است.")}</DialogDescription>
        </DialogHeader>
        <div className="grid gap-4">
          <FormField label={t("گیرنده")} htmlFor="em-to"><Input id="em-to" dir="ltr" className="text-end" type="email" value={to} onChange={(e) => setTo(e.target.value)} /></FormField>
          <FormField label={t("متن پیام (اختیاری)")} htmlFor="em-msg"><Textarea id="em-msg" rows={3} value={message} onChange={(e) => setMessage(e.target.value)} /></FormField>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>{t("انصراف")}</Button>
          <Button onClick={send} disabled={busy || !to}>{busy && <Spinner />} {t("ارسال ایمیل")}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
