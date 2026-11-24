import { Search } from "lucide-react"
import { InputGroup, InputGroupAddon, InputGroupInput } from "@/components/ui/input-group"
import { t } from "@/i18n"

export function SearchInput({ value, onChange, placeholder = t("جستجو…") }: { value: string; onChange: (v: string) => void; placeholder?: string }) {
  return (
    <InputGroup className="w-full bg-paper sm:w-72">
      <InputGroupAddon>
        <Search />
      </InputGroupAddon>
      <InputGroupInput value={value} onChange={(e) => onChange(e.target.value)} placeholder={placeholder} aria-label={t("جستجو")} />
    </InputGroup>
  )
}
