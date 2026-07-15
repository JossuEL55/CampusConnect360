import type { SVGProps } from 'react'

type IconProps = SVGProps<SVGSVGElement> & { size?: number }

function Svg({ size = 17, children, ...props }: IconProps) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 20 20"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.4"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      {...props}
    >
      {children}
    </svg>
  )
}

// Marca: círculo con radios, evoca el "360" del ecosistema.
export function Wordmark({ size = 17 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 20 20" aria-hidden="true">
      <circle cx="10" cy="10" r="7.2" fill="none" stroke="currentColor" strokeWidth="1.7" />
      <circle cx="10" cy="10" r="2.4" fill="currentColor" />
      <path d="M10 2.8v3M10 14.2v3M2.8 10h3M14.2 10h3" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" />
    </svg>
  )
}

export function LogOutIcon(props: IconProps) {
  return (
    <svg width={18} height={18} viewBox="0 0 24 24" fill="none" aria-hidden="true" {...props}>
      <path
        d="M14 4H9a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h5M10 12h9M16 8l4 4-4 4"
        stroke="currentColor"
        strokeWidth="1.7"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  )
}

export function StudentsIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <circle cx="7" cy="7" r="2.6" />
      <path d="M2.5 16c0-2.6 2-4 4.5-4s4.5 1.4 4.5 4" />
      <path d="M13 8h5M13 11h4" />
    </Svg>
  )
}

export function CalendarIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <rect x="3" y="3.5" width="14" height="13" rx="2" />
      <path d="M3 7.5h14M7 2.5v2M13 2.5v2" />
    </Svg>
  )
}

export function AlertIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <path d="M10 2.5 17 14H3L10 2.5z" />
      <path d="M10 7v3.5M10 12.4v.2" strokeWidth="1.5" />
    </Svg>
  )
}

export function HistoryIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <path d="M4 4h12v12l-2-1.5L12 16l-2-1.5L8 16l-2-1.5L4 16V4z" />
    </Svg>
  )
}

export function GridIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <rect x="3" y="3" width="6" height="6" rx="1" />
      <rect x="11" y="3" width="6" height="6" rx="1" />
      <rect x="3" y="11" width="6" height="6" rx="1" />
      <rect x="11" y="11" width="6" height="6" rx="1" />
    </Svg>
  )
}

export function LogIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <path d="M4 15V9M8 15V5M12 15v-4M16 15V7" strokeWidth="1.6" />
    </Svg>
  )
}

export function TraceIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <circle cx="10" cy="10" r="7" />
      <path d="M10 6.5v3.7l2.4 1.4" />
    </Svg>
  )
}

export function MoneyIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <rect x="2.5" y="5" width="15" height="10" rx="2" />
      <circle cx="10" cy="10" r="2.4" />
    </Svg>
  )
}

export function BellIcon(props: IconProps) {
  return (
    <Svg {...props}>
      <path d="M6 8a4 4 0 0 1 8 0c0 4 1.5 5 1.5 5h-11S6 12 6 8z" />
      <path d="M8.5 16a1.7 1.7 0 0 0 3 0" />
    </Svg>
  )
}
