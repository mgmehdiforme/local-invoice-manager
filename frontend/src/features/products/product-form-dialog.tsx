import { t } from "@/i18n"
import { useEffect, useState, type ReactNode } from "react"
import { Controller, useForm } from "react-hook-form"
import { toast } from "sonner"
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from "@/components/ui/dialog"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { Switch } from "@/components/ui/switch"
import { Spinner } from "@/components/ui/spinner"
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group"
import { Label } from "@/components/ui/label"
import { FormField, applyServerErrors } from "@/components/form-field"
import { NumberInput } from "@/components/number-input"
import { useCurrency } from "@/hooks/use-settings"
import { errorMessage } from "@/services/api"
import { UNITS, useSaveProduct, type Product, type ProductInput } from "./api"

const empty: ProductInput = { name: "", sku: "", kind: "Product", unit: t("عدد"), description: "", defaultPrice: 0, isTaxable: true }

export function ProductFormDialog({ product, trigger, open: openProp, onOpenChange, onSaved }: {
  product?: Product; trigger?: ReactNode; open?: boolean; onOpenChange?: (o: boolean) => void; onSaved?: (p: Product) => void
}) {
  const [openState, setOpenState] = useState(false)
  const open = openProp ?? openState
  const setOpen = onOpenChange ?? setOpenState
  const currency = useCurrency()
  const save = useSaveProduct()
  const { register, control, handleSubmit, reset, setError, formState: { errors } } = useForm<ProductInput>({ defaultValues: empty })

  useEffect(() => {
    if (open) reset(product ? { ...empty, ...product } : empty)
  }, [open, product, reset])

  const onSubmit = handleSubmit(async (input) => {
    try {
      const saved = await save.mutateAsync({ id: product?.id, input })
      toast.success(product ? t("تغییرات ذخیره شد.") : t("به فهرست اضافه شد."))
      setOpen(false)
      onSaved?.(saved)
    } catch (err) {
      if (!applyServerErrors(err, setError)) toast.error(errorMessage(err))
    }
  })

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      {trigger && <DialogTrigger asChild>{trigger}</DialogTrigger>}
      <DialogContent className="sm:max-w-xl">
        <DialogHeader>
          <DialogTitle>{product ? t("ویرایش کالا/خدمت") : t("کالا یا خدمت جدید")}</DialogTitle>
          <DialogDescription>{t("قیمت پیش‌فرض هنگام افزودن به فاکتور پر می‌شود و قابل تغییر است.")}</DialogDescription>
        </DialogHeader>
        <form id="product-form" onSubmit={onSubmit} className="grid gap-4 sm:grid-cols-2">
          <FormField label={t("نوع")} className="sm:col-span-2">
            <Controller
              control={control}
              name="kind"
              render={({ field }) => (
                <RadioGroup value={field.value} onValueChange={field.onChange} className="flex gap-6">
                  <div className="flex items-center gap-2"><RadioGroupItem value="Product" id="k-p" /><Label htmlFor="k-p">{t("کالا")}</Label></div>
                  <div className="flex items-center gap-2"><RadioGroupItem value="Service" id="k-s" /><Label htmlFor="k-s">{t("خدمت")}</Label></div>
                </RadioGroup>
              )}
            />
          </FormField>
          <FormField label={t("نام")} htmlFor="p-name" error={errors.name?.message} required className="sm:col-span-2">
            <Input id="p-name" autoFocus {...register("name", { required: t("نام را وارد کنید.") })} aria-invalid={!!errors.name} />
          </FormField>
          <FormField label={t("قیمت پیش‌فرض ({0})", [currency.currencySymbol])} htmlFor="p-price" error={errors.defaultPrice?.message}>
            <Controller control={control} name="defaultPrice" render={({ field }) => (
              <NumberInput id="p-price" value={field.value} onChange={field.onChange} decimals={currency.currencyDecimalPlaces} invalid={!!errors.defaultPrice} />
            )} />
          </FormField>
          <FormField label={t("واحد")} htmlFor="p-unit" error={errors.unit?.message} required>
            <Input id="p-unit" list="units" {...register("unit", { required: t("واحد را وارد کنید.") })} />
            <datalist id="units">{UNITS.map((u) => <option key={u} value={u} />)}</datalist>
          </FormField>
          <FormField label={t("کد کالا (SKU)")} htmlFor="p-sku" error={errors.sku?.message} hint={t("اختیاری؛ باید یکتا باشد.")}>
            <Input id="p-sku" dir="ltr" className="text-end" {...register("sku")} />
          </FormField>
          <FormField label={t("مالیات")} htmlFor="p-tax">
            <Controller control={control} name="isTaxable" render={({ field }) => (
              <div className="flex h-9 items-center gap-2">
                <Switch id="p-tax" checked={field.value} onCheckedChange={field.onChange} />
                <span className="text-sm text-muted-foreground">{field.value ? t("مشمول مالیات") : t("معاف از مالیات")}</span>
              </div>
            )} />
          </FormField>
          <FormField label={t("توضیحات")} htmlFor="p-desc" error={errors.description?.message} className="sm:col-span-2">
            <Textarea id="p-desc" rows={2} {...register("description")} />
          </FormField>
        </form>
        <DialogFooter>
          <Button type="button" variant="outline" onClick={() => setOpen(false)}>{t("انصراف")}</Button>
          <Button type="submit" form="product-form" disabled={save.isPending}>
            {save.isPending && <Spinner />} {product ? t("ذخیره تغییرات") : t("افزودن")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
