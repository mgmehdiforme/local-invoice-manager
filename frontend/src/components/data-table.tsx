import type { ReactNode } from "react"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { Skeleton } from "@/components/ui/skeleton"
import { cn } from "@/lib/utils"

export interface Column<T> {
  key: string
  header: ReactNode
  cell: (row: T) => ReactNode
  className?: string
  /** Hide on small screens. */
  hideOnMobile?: boolean
  align?: "start" | "end"
}

/** Simple list table on a paper surface. Rows are clickable when onRowClick is set. */
export function DataTable<T>({ rows, columns, loading, onRowClick, rowKey, empty, rowClassName }: {
  rows: T[] | undefined
  columns: Column<T>[]
  loading?: boolean
  onRowClick?: (row: T) => void
  rowKey: (row: T) => string | number
  empty?: ReactNode
  rowClassName?: (row: T) => string | undefined
}) {
  if (!loading && rows && rows.length === 0 && empty) return <>{empty}</>
  return (
    <div className="overflow-hidden rounded-xl border bg-paper">
      <Table>
        <TableHeader>
          <TableRow className="bg-muted/40 hover:bg-muted/40">
            {columns.map((c) => (
              <TableHead key={c.key} className={cn("h-10 text-xs font-medium text-muted-foreground", c.align === "end" && "text-end", c.hideOnMobile && "hidden md:table-cell", c.className)}>
                {c.header}
              </TableHead>
            ))}
          </TableRow>
        </TableHeader>
        <TableBody>
          {loading && !rows
            ? Array.from({ length: 5 }, (_, i) => (
                <TableRow key={i}>
                  {columns.map((c) => (
                    <TableCell key={c.key} className={cn(c.hideOnMobile && "hidden md:table-cell")}>
                      <Skeleton className="h-4 w-full max-w-32" />
                    </TableCell>
                  ))}
                </TableRow>
              ))
            : rows?.map((row) => (
                <TableRow
                  key={rowKey(row)}
                  onClick={onRowClick ? () => onRowClick(row) : undefined}
                  className={cn(onRowClick && "cursor-pointer", rowClassName?.(row))}
                >
                  {columns.map((c) => (
                    <TableCell key={c.key} className={cn("py-3", c.align === "end" && "text-end", c.hideOnMobile && "hidden md:table-cell", c.className)}>
                      {c.cell(row)}
                    </TableCell>
                  ))}
                </TableRow>
              ))}
        </TableBody>
      </Table>
    </div>
  )
}

/** Stops row click when using a menu or button inside a row. */
export function StopRowClick({ children }: { children: ReactNode }) {
  return <div onClick={(e) => e.stopPropagation()}>{children}</div>
}
