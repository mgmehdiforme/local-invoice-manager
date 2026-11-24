import { Toaster as Sonner, type ToasterProps } from "sonner"

/** App-wide toasts: bottom-left in RTL, Persian font, plain styling. */
function Toaster(props: ToasterProps) {
  return (
    <Sonner
      dir="rtl"
      position="bottom-left"
      richColors
      closeButton
      toastOptions={{ style: { fontFamily: "var(--font-sans)" } }}
      {...props}
    />
  )
}

export { Toaster }
