import { BarChart3, FilePlus2, FileText, LayoutDashboard, Package, ReceiptText, Repeat, Settings, Users, Wallet } from "lucide-react"
import type { LucideIcon } from "lucide-react"
import { t } from "@/i18n"

export interface NavItem { to: string; label: string; icon: LucideIcon; end?: boolean }

/** Main navigation (PRD §10.1). Grouped by what the owner does most. */
export const NAV: { group: string; items: NavItem[] }[] = [
  {
    group: t("کار روزانه"),
    items: [
      { to: "/", label: t("داشبورد"), icon: LayoutDashboard, end: true },
      { to: "/invoices", label: t("فاکتورها"), icon: FileText, end: true },
      { to: "/invoices/new", label: t("فاکتور جدید"), icon: FilePlus2 },
      { to: "/quotations", label: t("پیش‌فاکتورها"), icon: ReceiptText },
      { to: "/payments", label: t("پرداخت‌ها"), icon: Wallet },
    ],
  },
  {
    group: t("اطلاعات پایه"),
    items: [
      { to: "/customers", label: t("مشتریان"), icon: Users },
      { to: "/products", label: t("کالاها و خدمات"), icon: Package },
      { to: "/recurring", label: t("فاکتورهای تکرارشونده"), icon: Repeat },
    ],
  },
  {
    group: t("گزارش و تنظیمات"),
    items: [
      { to: "/reports", label: t("گزارش‌ها"), icon: BarChart3 },
      { to: "/settings", label: t("تنظیمات"), icon: Settings },
    ],
  },
]
