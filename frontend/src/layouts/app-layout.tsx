import { NavLink, Outlet, useLocation } from "react-router"
import { LogOut } from "lucide-react"
import {
  Sidebar, SidebarContent, SidebarFooter, SidebarGroup, SidebarGroupContent, SidebarGroupLabel, SidebarHeader,
  SidebarInset, SidebarMenu, SidebarMenuButton, SidebarMenuItem, SidebarProvider, SidebarTrigger, useSidebar,
} from "@/components/ui/sidebar"
import { NAV } from "@/app/nav"
import { useLogout } from "@/hooks/use-auth"
import { useBrandColor, useSettings } from "@/hooks/use-settings"
import { NotificationBell } from "@/features/notifications/notification-bell"
import { Button } from "@/components/ui/button"
import { api } from "@/services/api"
import { applyPrefsAndReload, i18n, t, type Lang } from "@/i18n"
import { toast } from "sonner"
import { errorMessage } from "@/services/api"

function Brand() {
  const { data } = useSettings()
  return (
    <div className="flex items-center gap-2.5 px-2 py-1.5">
      {data?.hasLogo ? (
        <img src="/api/v1/settings/logo" alt="" className="size-9 rounded-md border bg-white object-contain" />
      ) : (
        <div className="grid size-9 place-items-center rounded-md bg-brand text-sm font-bold text-white">{t("ف")}</div>
      )}
      <div className="min-w-0 leading-tight">
        <div className="truncate text-sm font-semibold">{data?.companyName || t("مدیریت فاکتور")}</div>
        <div className="text-xs text-muted-foreground">{t("نسخه محلی")}</div>
      </div>
    </div>
  )
}

function Nav() {
  const { setOpenMobile } = useSidebar()
  return (
    <>
      {NAV.map((g) => (
        <SidebarGroup key={g.group}>
          <SidebarGroupLabel>{g.group}</SidebarGroupLabel>
          <SidebarGroupContent>
            <SidebarMenu>
              {g.items.map((item) => (
                <SidebarMenuItem key={item.to}>
                  <NavLink to={item.to} end={item.end} onClick={() => setOpenMobile(false)}>
                    {({ isActive }) => (
                      <SidebarMenuButton isActive={isActive} tooltip={item.label}>
                        <item.icon />
                        <span>{item.label}</span>
                      </SidebarMenuButton>
                    )}
                  </NavLink>
                </SidebarMenuItem>
              ))}
            </SidebarMenu>
          </SidebarGroupContent>
        </SidebarGroup>
      ))}
    </>
  )
}

/** Switches the whole app (UI, PDFs, messages) between English and Persian. */
function LanguageSwitch() {
  const next: Lang = i18n.lang === "fa" ? "en" : "fa"
  const label = next === "en" ? "EN" : "فا" // i18n-ignore
  const change = async () => {
    try {
      await api.put("/settings/preferences", { language: next, calendar: i18n.calendar })
      applyPrefsAndReload({ language: next, calendar: i18n.calendar })
    } catch (e) {
      toast.error(errorMessage(e))
    }
  }
  return (
    <Button variant="ghost" size="sm" onClick={change} aria-label={t("تغییر زبان")} title={t("تغییر زبان")}>
      {label}
    </Button>
  )
}

export function AppLayout() {
  useBrandColor()
  const logout = useLogout()
  const location = useLocation()
  return (
    <SidebarProvider>
      <Sidebar side={i18n.dir === "rtl" ? "right" : "left"} collapsible="icon" className="no-print">
        <SidebarHeader>
          <Brand />
        </SidebarHeader>
        <SidebarContent>
          <Nav />
        </SidebarContent>
        <SidebarFooter>
          <SidebarMenu>
            <SidebarMenuItem>
              <SidebarMenuButton onClick={() => logout.mutate()} tooltip={t("خروج")}>
                <LogOut />
                <span>{t("خروج")}</span>
              </SidebarMenuButton>
            </SidebarMenuItem>
          </SidebarMenu>
        </SidebarFooter>
      </Sidebar>
      <SidebarInset className="bg-background">
        <header className="no-print sticky top-0 z-10 flex h-14 items-center gap-2 border-b bg-background/90 px-4 backdrop-blur">
          <SidebarTrigger />
          <div className="flex-1" />
          <LanguageSwitch />
          <NotificationBell />
        </header>
        <main key={location.pathname} className="mx-auto w-full max-w-6xl flex-1 px-4 py-6 md:px-8">
          <Outlet />
        </main>
      </SidebarInset>
    </SidebarProvider>
  )
}
