import en from "./en"

/**
 * Tiny i18n without a library. Keys are the Persian UI strings; `./en/*` holds the English text.
 * Language and calendar are read once at start-up from localStorage (the server value is cached there
 * by `syncPrefs`); changing them reloads the page, so labels built at module load are correct too.
 */
export type Lang = "fa" | "en"
export type CalendarKind = "Jalali" | "Gregorian"

const LANG_KEY = "lim.lang"
const CAL_KEY = "lim.calendar"

function read(key: string): string | null {
  try {
    return localStorage.getItem(key)
  } catch {
    return null
  }
}

function write(key: string, value: string) {
  try {
    localStorage.setItem(key, value)
  } catch {
    /* private mode: the server value is used on every load instead */
  }
}

const browserLang: Lang = typeof navigator !== "undefined" && navigator.language?.startsWith("fa") ? "fa" : "en"
const initialLang: Lang = (read(LANG_KEY) as Lang | null) ?? browserLang

export const i18n = {
  lang: (initialLang === "fa" ? "fa" : "en") as Lang,
  calendar: ((read(CAL_KEY) as CalendarKind | null) ?? (initialLang === "fa" ? "Jalali" : "Gregorian")) as CalendarKind,
  get dir(): "rtl" | "ltr" {
    return this.lang === "fa" ? "rtl" : "ltr"
  },
}

/** For tests. */
export function configureI18n(lang: Lang, calendar: CalendarKind) {
  i18n.lang = lang
  i18n.calendar = calendar
}

type Vars = Record<string, string | number> | (string | number)[]

/** Translates a Persian UI string. `{name}` or `{0}` placeholders are filled from `vars`. */
export function t(fa: string, vars?: Vars): string {
  let s = i18n.lang === "en" ? (en[fa] ?? fa) : fa
  if (vars) {
    s = s.replace(/\{(\w+)\}/g, (m, k: string) => {
      const v = Array.isArray(vars) ? vars[Number(k)] : vars[k]
      return v === undefined ? m : String(v)
    })
  }
  return s
}

/** Stores the server's language and calendar. Returns true when the page must reload to apply them. */
export function syncPrefs(p: { language?: string | null; calendar?: string | null }): boolean {
  let changed = false
  if (p.language && p.language !== i18n.lang) {
    write(LANG_KEY, p.language)
    changed = true
  }
  if (p.calendar && p.calendar !== i18n.calendar) {
    write(CAL_KEY, p.calendar)
    changed = true
  }
  return changed
}

export function applyPrefsAndReload(p: { language?: string | null; calendar?: string | null }) {
  if (syncPrefs(p)) window.location.reload()
}

/** Sets <html lang dir> and the page title. Called once from main.tsx. */
export function applyDocumentLanguage() {
  document.documentElement.lang = i18n.lang
  document.documentElement.dir = i18n.dir
  document.title = t("مدیریت فاکتور")
}
