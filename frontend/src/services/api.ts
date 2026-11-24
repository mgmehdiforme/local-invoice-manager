import { i18n, t } from "@/i18n"

/** Thin fetch wrapper: JSON, XSRF header, Problem Details errors, 401 → login. */

export class ApiError extends Error {
  status: number
  code: string
  errors?: Record<string, string[]>
  constructor(status: number, code: string, message: string, errors?: Record<string, string[]>) {
    super(message)
    this.status = status
    this.code = code
    this.errors = errors
  }
}

const BASE = "/api/v1"
let unauthorizedHandler: (() => void) | null = null
export function onUnauthorized(handler: () => void) {
  unauthorizedHandler = handler
}

function xsrfToken(): string | null {
  const m = document.cookie.match(/(?:^|;\s*)XSRF-TOKEN=([^;]+)/)
  return m ? decodeURIComponent(m[1]) : null
}

export async function ensureXsrf(): Promise<void> {
  if (!xsrfToken()) await fetch(`${BASE}/auth/csrf`, { credentials: "same-origin" })
}

type Query = Record<string, string | number | boolean | null | undefined>

export function withQuery(path: string, query?: Query): string {
  if (!query) return path
  const params = new URLSearchParams()
  for (const [k, v] of Object.entries(query)) if (v !== undefined && v !== null && v !== "") params.set(k, String(v))
  const qs = params.toString()
  return qs ? `${path}?${qs}` : path
}

async function request<T>(method: string, path: string, body?: unknown, retry = true): Promise<T> {
  const headers: Record<string, string> = { Accept: "application/json", "Accept-Language": i18n.lang }
  const isForm = body instanceof FormData
  if (method !== "GET") {
    await ensureXsrf()
    const token = xsrfToken()
    if (token) headers["X-XSRF-TOKEN"] = token
  }
  if (body !== undefined && !isForm) headers["Content-Type"] = "application/json"

  const res = await fetch(BASE + path, {
    method,
    headers,
    credentials: "same-origin",
    body: body === undefined ? undefined : isForm ? body : JSON.stringify(body),
  })

  if (res.status === 204) return undefined as T
  const isJson = res.headers.get("content-type")?.includes("json")
  const data = isJson ? await res.json() : null

  if (!res.ok) {
    const code: string = data?.code ?? data?.title ?? "error"
    // XSRF token expired (e.g. after login in another tab): refresh once and retry.
    if (code === "xsrf" && retry) {
      await fetch(`${BASE}/auth/csrf`, { credentials: "same-origin" })
      return request<T>(method, path, body, false)
    }
    if (res.status === 401 && !path.startsWith("/auth/")) unauthorizedHandler?.()
    const message =
      data?.detail ||
      (res.status === 429 ? t("تعداد درخواست‌ها زیاد است. کمی بعد دوباره تلاش کنید.") : t("خطا در ارتباط با سرور ({0})", [res.status]))
    throw new ApiError(res.status, code, message, data?.errors)
  }
  return data as T
}

export const api = {
  get: <T>(path: string, query?: Query) => request<T>("GET", withQuery(path, query)),
  post: <T>(path: string, body?: unknown) => request<T>("POST", path, body ?? {}),
  put: <T>(path: string, body: unknown) => request<T>("PUT", path, body),
  del: <T>(path: string) => request<T>("DELETE", path),
  upload: <T>(path: string, form: FormData) => request<T>("POST", path, form),
}

/** Downloads a file from an authenticated GET endpoint. */
export async function downloadFile(path: string, fallbackName: string): Promise<void> {
  const res = await fetch(BASE + path, { credentials: "same-origin", headers: { "Accept-Language": i18n.lang } })
  if (!res.ok) throw new ApiError(res.status, "download", t("دریافت فایل ناموفق بود."))
  const blob = await res.blob()
  const cd = res.headers.get("content-disposition") ?? ""
  const match = cd.match(/filename\*=UTF-8''([^;]+)/) ?? cd.match(/filename="?([^";]+)"?/)
  const name = match ? decodeURIComponent(match[1]) : fallbackName
  const url = URL.createObjectURL(blob)
  const a = document.createElement("a")
  a.href = url
  a.download = name
  a.click()
  setTimeout(() => URL.revokeObjectURL(url), 10_000)
}

export function errorMessage(e: unknown): string {
  if (e instanceof ApiError) return e.message
  if (e instanceof Error && e.message === "Failed to fetch") return t("ارتباط با برنامه برقرار نشد. آیا برنامه در حال اجراست؟")
  return t("خطای غیرمنتظره رخ داد.")
}
