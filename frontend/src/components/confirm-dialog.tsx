import { useState, type ReactNode } from "react"
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription,
  AlertDialogFooter, AlertDialogHeader, AlertDialogTitle, AlertDialogTrigger,
} from "@/components/ui/alert-dialog"
import { Textarea } from "@/components/ui/textarea"
import { Label } from "@/components/ui/label"
import { t } from "@/i18n"

interface Props {
  trigger: ReactNode
  title: string
  description: ReactNode
  confirmLabel: string
  destructive?: boolean
  /** Ask for a reason (e.g. cancel invoice, reverse payment). */
  reasonLabel?: string
  onConfirm: (reason: string) => void | Promise<void>
}

export function ConfirmDialog({ trigger, title, description, confirmLabel, destructive, reasonLabel, onConfirm }: Props) {
  const [reason, setReason] = useState("")
  const [busy, setBusy] = useState(false)
  return (
    <AlertDialog onOpenChange={(o) => !o && setReason("")}>
      <AlertDialogTrigger asChild>{trigger}</AlertDialogTrigger>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{title}</AlertDialogTitle>
          <AlertDialogDescription>{description}</AlertDialogDescription>
        </AlertDialogHeader>
        {reasonLabel && (
          <div className="grid gap-2">
            <Label htmlFor="confirm-reason">{reasonLabel}</Label>
            <Textarea id="confirm-reason" value={reason} onChange={(e) => setReason(e.target.value)} rows={2} />
          </div>
        )}
        <AlertDialogFooter>
          <AlertDialogCancel>{t("انصراف")}</AlertDialogCancel>
          <AlertDialogAction
            disabled={busy || (!!reasonLabel && !reason.trim())}
            className={destructive ? "bg-destructive text-white hover:bg-destructive/90" : undefined}
            onClick={async () => {
              setBusy(true)
              try {
                await onConfirm(reason.trim())
              } finally {
                setBusy(false)
              }
            }}
          >
            {confirmLabel}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
