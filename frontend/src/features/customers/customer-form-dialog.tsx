import { t } from "@/i18n"
import { useEffect, useState, type ReactNode } from "react"
import { useForm } from "react-hook-form"
import { toast } from "sonner"
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from "@/components/ui/dialog"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { Spinner } from "@/components/ui/spinner"
import { FormField, applyServerErrors } from "@/components/form-field"
import { errorMessage } from "@/services/api"
import { useSaveCustomer, type Customer, type CustomerInput } from "./api"

const empty: CustomerInput = { name: "", companyName: "", phone: "", mobile: "", email: "", address: "", city: "", postalCode: "", taxId: "", nationalId: "", notes: "" }

export function CustomerFormDialog({ customer, trigger, onSaved, open: openProp, onOpenChange }: {
  customer?: Customer; trigger?: ReactNode; onSaved?: (c: Customer) => void; open?: boolean; onOpenChange?: (o: boolean) => void
}) {
  const [openState, setOpenState] = useState(false)
  const open = openProp ?? openState
  const setOpen = onOpenChange ?? setOpenState
  const save = useSaveCustomer()
  const { register, handleSubmit, reset, setError, formState: { errors } } = useForm<CustomerInput>({ defaultValues: empty })

  useEffect(() => {
    if (open) reset(customer ? { ...empty, ...customer } : empty)
  }, [open, customer, reset])

  const onSubmit = handleSubmit(async (input) => {
    try {
      const saved = await save.mutateAsync({ id: customer?.id, input })
      toast.success(customer ? t("مشتری ذخیره شد.") : t("مشتری اضافه شد."))
      setOpen(false)
      onSaved?.(saved)
    } catch (err) {
      if (!applyServerErrors(err, setError)) toast.error(errorMessage(err))
    }
  })

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      {trigger && <DialogTrigger asChild>{trigger}</DialogTrigger>}
      <DialogContent className="sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>{customer ? t("ویرایش مشتری") : t("مشتری جدید")}</DialogTitle>
          <DialogDescription>{t("فقط نام الزامی است. بقیه اطلاعات روی فاکتور چاپ می‌شود.")}</DialogDescription>
        </DialogHeader>
        <form id="customer-form" onSubmit={onSubmit} className="grid max-h-[65vh] gap-4 overflow-y-auto px-0.5 sm:grid-cols-2">
          <FormField label={t("نام")} htmlFor="c-name" error={errors.name?.message} required>
            <Input id="c-name" autoFocus {...register("name", { required: t("نام مشتری را وارد کنید.") })} aria-invalid={!!errors.name} />
          </FormField>
          <FormField label={t("نام شرکت")} htmlFor="c-company" error={errors.companyName?.message}>
            <Input id="c-company" {...register("companyName")} />
          </FormField>
          <FormField label={t("موبایل")} htmlFor="c-mobile" error={errors.mobile?.message}>
            <Input id="c-mobile" dir="ltr" className="text-end" inputMode="tel" {...register("mobile")} />
          </FormField>
          <FormField label={t("تلفن")} htmlFor="c-phone" error={errors.phone?.message}>
            <Input id="c-phone" dir="ltr" className="text-end" inputMode="tel" {...register("phone")} />
          </FormField>
          <FormField label={t("ایمیل")} htmlFor="c-email" error={errors.email?.message} className="sm:col-span-2">
            <Input id="c-email" dir="ltr" className="text-end" type="email" {...register("email")} />
          </FormField>
          <FormField label={t("شهر")} htmlFor="c-city" error={errors.city?.message}>
            <Input id="c-city" {...register("city")} />
          </FormField>
          <FormField label={t("کد پستی")} htmlFor="c-postal" error={errors.postalCode?.message}>
            <Input id="c-postal" dir="ltr" className="text-end" {...register("postalCode")} />
          </FormField>
          <FormField label={t("نشانی")} htmlFor="c-address" error={errors.address?.message} className="sm:col-span-2">
            <Textarea id="c-address" rows={2} {...register("address")} />
          </FormField>
          <FormField label={t("کد اقتصادی")} htmlFor="c-tax" error={errors.taxId?.message}>
            <Input id="c-tax" dir="ltr" className="text-end" {...register("taxId")} />
          </FormField>
          <FormField label={t("شناسه/کد ملی")} htmlFor="c-nid" error={errors.nationalId?.message}>
            <Input id="c-nid" dir="ltr" className="text-end" {...register("nationalId")} />
          </FormField>
          <FormField label={t("یادداشت")} htmlFor="c-notes" error={errors.notes?.message} hint={t("فقط برای شما؛ روی فاکتور چاپ نمی‌شود.")} className="sm:col-span-2">
            <Textarea id="c-notes" rows={2} {...register("notes")} />
          </FormField>
        </form>
        <DialogFooter>
          <Button type="button" variant="outline" onClick={() => setOpen(false)}>{t("انصراف")}</Button>
          <Button type="submit" form="customer-form" disabled={save.isPending}>
            {save.isPending && <Spinner />} {customer ? t("ذخیره تغییرات") : t("افزودن مشتری")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
