/// <reference types="node" />
import { readdirSync, readFileSync, statSync } from "node:fs"
import { join, relative } from "node:path"
import { describe, expect, it } from "vitest"
import en from "./en"

/**
 * Guards the translation: every t("…") key must have an English entry, and no Persian text may
 * appear outside t("…"). Exceptions: files below, comments, and lines marked `i18n-ignore`.
 */
const SRC = join(__dirname, "..")
const SKIP = ["i18n/", "components/ui/", "lib/calendar.ts", "lib/jalali.ts", "lib/format.ts", ".test.ts"]
const PERSIAN = /[؀-ۿ]/

function files(dir: string): string[] {
  return readdirSync(dir).flatMap((f) => {
    const p = join(dir, f)
    return statSync(p).isDirectory() ? files(p) : /\.(ts|tsx)$/.test(f) ? [p] : []
  })
}

const sources = files(SRC)
  .map((p) => ({ path: relative(SRC, p).replaceAll("\\", "/"), text: readFileSync(p, "utf8") }))
  .filter((f) => !SKIP.some((s) => f.path.includes(s)))

const T_CALL = /\bt\(\s*(["'`])((?:\\.|(?!\1).)*)\1/gs

describe("i18n", () => {
  it("has an English entry for every t() key", () => {
    const missing = new Set<string>()
    for (const f of sources)
      for (const m of f.text.matchAll(T_CALL)) if (PERSIAN.test(m[2]) && !(m[2] in en)) missing.add(`${f.path}: ${m[2]}`)
    expect([...missing]).toEqual([])
  })

  it("has no Persian text outside t()", () => {
    const found: string[] = []
    for (const f of sources) {
      const stripped = f.text
        .replace(/\/\*[\s\S]*?\*\//g, "")
        .replace(T_CALL, "t()")
      stripped.split("\n").forEach((line, i) => {
        const code = line.replace(/(^|[^:])\/\/.*$/, "$1")
        if (PERSIAN.test(code) && !line.includes("i18n-ignore")) found.push(`${f.path}:${i + 1}: ${code.trim()}`)
      })
    }
    expect(found).toEqual([])
  })
})
