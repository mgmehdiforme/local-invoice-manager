import { i18n, t } from "@/i18n"
import { useEffect, useState, type ReactNode } from "react"
import { Controller, useForm } from "react-hook-form"
import { useQueryClient } from "@tanstack/react-query"
import { toast } from "sonner"
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from "@/components/ui/dialog"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { Spinner } from "@/components/ui/spinner"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { FormField, applyServerErrors } from "@/components/form-field"
import { NumberInput } from "@/components/number-input"
import { JalaliDatePicker } from "@/components/jalali-date-picker"
import { Money } from "@/components/money"
import { api, errorMessage } from "@/services/api"
import { todayIso } from "@/lib/jalali"
import { PAYMENT_METHODS, type Invoice, type PaymentMethod } from "@/features/invoices/api"

interface Form { amount: number; paymentDate: string; method: PaymentMethod; reference: string; notes: string }

export function PaymentDialog({ invoice, trigger }: { invoice: Invoice; trigger: ReactNode }) {
  const qc = useQueryClient()
  const [open, setOpen] = useState(false)
  const { control, register, handleSubmit, reset, setError, setValue, formState: { errors, isSubmitting } } = useForm<Form>()

  useEffect(() => {
    if (open) reset({ amount: invoice.balance, paymentDate: todayIso(), method: "BankTransfer", reference: "", notes: "" })
  }, [open, invoice.balance, reset])

  const submit = handleSubmit(async (v) => {
    try {
      const updated = await api.post<Invoice>(`/invoices/${invoice.id}/payments`, v)
      qc.setQueryData(["invoices", "one", invoice.id], updated)
      await qc.invalidateQueries()
      toast.success(updated.paymentStatus === "Paid" ? t("پرداخت ثبت شد. فاکتور تسویه شد.") : t("پرداخت ثبت شد."))
      setOpen(false)
    } catch (e) {
      if (!applyServerErrors(e, setError)) toast.error(errorMessage(e))
    }
  })

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>{trigger}</DialogTrigger>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{t("ثبت پرداخت")}</DialogTitle>
          <DialogDescription>
            {t("مانده فاکتور:")} <Money value={invoice.balance} currency={invoice.currency} className="font-semibold text-foreground" />
          </DialogDescription>
        </DialogHeader>
        <form id="payment-form" onSubmit={submit} className="grid gap-4">
          <FormField label={t("مبلغ ({0})", [invoice.currency.currencySymbol])} error={errors.amount?.message} required
            hint={<button type="button" className="text-brand hover:underline" onClick={() => setValue("amount", invoice.balance)}>{t("کل مانده")}</button>}>
            <Controller control={control} name="amount" render={({ field }) => <NumberInput value={field.value} onChange={field.onChange} decimals={invoice.currency.currencyDecimalPlaces} invalid={!!errors.amount} />} />
          </FormField>
          <div className="grid grid-cols-2 gap-3">
            <FormField label={t("تاریخ")} error={errors.paymentDate?.message} required>
              <Controller control={control} name="paymentDate" render={({ field }) => <JalaliDatePicker value={field.value} onChange={(v) => v && field.onChange(v)} />} />
            </FormField>
            <FormField label={t("روش")}>
              <Controller control={control} name="method" render={({ field }) => (
                <Select value={field.value} onValueChange={field.onChange} dir={i18n.dir}>
                  <SelectTrigger className="w-full"><SelectValue /></SelectTrigger>
                  <SelectContent>{Object.entries(PAYMENT_METHODS).map(([k, v]) => <SelectItem key={k} value={k}>{v}</SelectItem>)}</SelectContent>
                </Select>
              )} />
            </FormField>
          </div>
          <FormField label={t("شماره پیگیری / چک")} error={errors.reference?.message}><Input dir="ltr" className="text-end" {...register("reference")} /></FormField>
          <FormField label={t("یادداشت")} error={errors.notes?.message}><Textarea rows={2} {...register("notes")} /></FormField>
        </form>
        <DialogFooter>
          <Button type="button" variant="outline" onClick={() => setOpen(false)}>{t("انصراف")}</Button>
          <Button type="submit" form="payment-form" disabled={isSubmitting}>{isSubmitting && <Spinner />} {t("ثبت پرداخت")}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
