// Sets the page language and direction before the app loads, so Persian (RTL) does not flash as LTR.
// Same values as src/i18n (localStorage "lim.lang"); kept tiny and external because the CSP forbids inline scripts.
try {
  var l = localStorage.getItem("lim.lang") || (navigator.language && navigator.language.indexOf("fa") === 0 ? "fa" : "en")
  document.documentElement.lang = l === "fa" ? "fa" : "en"
  document.documentElement.dir = l === "fa" ? "rtl" : "ltr"
} catch (e) {}
