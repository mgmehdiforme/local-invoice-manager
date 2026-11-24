export type SymbolPosition = "Before" | "After"
export type Language = "fa" | "en"
export type CalendarKind = "Jalali" | "Gregorian"

/** One enabled currency. The default one is `Settings.currencyCode`. */
export interface CurrencyDef {
  code: string
  symbol: string
  decimalPlaces: number
  symbolPosition: SymbolPosition
}

export interface Settings {
  language: Language
  calendar: CalendarKind
  /** Enabled currencies; must include `currencyCode` (the default). The server keeps the default's symbol fields in sync. */
  currencies: CurrencyDef[]
  companyName: string
  legalName?: string | null
  economicCode?: string | null
  nationalId?: string | null
  registrationNumber?: string | null
  phone?: string | null
  email?: string | null
  website?: string | null
  address?: string | null
  postalCode?: string | null
  hasLogo: boolean
  bankName?: string | null
  bankAccountNumber?: string | null
  bankCardNumber?: string | null
  bankIban?: string | null
  bankAccountHolder?: string | null
  currencyCode: string
  currencySymbol: string
  currencyDecimalPlaces: number
  thousandsSeparator: string
  decimalSeparator: string
  currencySymbolPosition: SymbolPosition
  defaultTaxRate: number
  taxLabel: string
  invoicePrefix: string
  quotationPrefix: string
  numberPadding: number
  resetNumberingYearly: boolean
  defaultDueDays: number
  defaultPaymentTerms?: string | null
  defaultNotes?: string | null
  defaultTemplate: string
  accentColor: string
  sessionTimeoutMinutes: number
  backupEnabled: boolean
  backupHour: number
  backupRetentionCount: number
  remindersEnabled: boolean
  reminderDaysBefore: number
  emailRemindersToCustomers: boolean
  smtpHost?: string | null
  smtpPort: number
  smtpUser?: string | null
  smtpPassword?: string | null
  smtpPasswordSet: boolean
  smtpUseSsl: boolean
  smtpFromEmail?: string | null
  smtpFromName?: string | null
  nextInvoiceNumberPreview?: string | null
}

export interface AuthStatus {
  isSetUp: boolean
  isAuthenticated: boolean
  sessionTimeoutMinutes: number
  language: Language
  calendar: CalendarKind
}

export interface Paged<T> {
  items: T[]
  page: number
  pageSize: number
  total: number
}
