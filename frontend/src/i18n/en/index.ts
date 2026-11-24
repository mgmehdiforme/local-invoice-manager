import common from "./common"
import app from "./app"
import masterdata from "./masterdata"
import documents from "./documents"

/** Persian UI string → English. Split by area so several people can work on it at once. */
const en: Record<string, string> = { ...common, ...app, ...masterdata, ...documents }
export default en
