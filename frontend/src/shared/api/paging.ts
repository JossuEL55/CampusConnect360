export interface Paged<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
}

// Algunos servicios responden lista simple y otros paginada; se normaliza a lista.
export function asItems<T>(data: T[] | Paged<T>): T[] {
  return Array.isArray(data) ? data : data.items
}
