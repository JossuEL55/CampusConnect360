import { useEffect, useState } from 'react'

// Sección activa compartida vía hash de la URL: la sidebar lo cambia y el portal la muestra.
export function useSection(ids: readonly string[]): [string, (id: string) => void] {
  const read = () => {
    const current = window.location.hash.replace('#', '')
    return ids.includes(current) ? current : ids[0]
  }

  const [active, setActive] = useState(read)

  useEffect(() => {
    const onHashChange = () => setActive(read())
    window.addEventListener('hashchange', onHashChange)
    return () => window.removeEventListener('hashchange', onHashChange)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [ids.join('|')])

  const go = (id: string) => {
    window.location.hash = id
  }

  return [active, go]
}
