# CampusConnect 360: Frontend

SPA React con los 4 portales del ecosistema (contrato, sección 13): Académico, Financiero,
Docente/Bienestar y Dashboard Directivo, con login único por JWT. Todo el consumo pasa por
el API Gateway; ningún servicio se llama directo.

## Arranque

```bash
cp .env.example .env    # ajustar VITE_API_URL si el Gateway no está en 8088
npm install
npm run dev             # http://localhost:3000 (origen permitido por el CORS del Gateway)
```

Usuarios semilla: `secretaria` (Academic), `finanzas` (Finance), `docente` (Teacher),
`direccion` (Director), `admin` (todos). La contraseña es la definida en `AUTH_DEMO_PASSWORD`
del backend.

## Stack y decisiones

| Decisión | Por qué |
|---|---|
| Vite + React + TypeScript | Tipado estricto y build rápido |
| TanStack Query | Estado de servidor con caché, invalidación tras mutaciones y polling del dashboard cada 10 s (contrato 13.5) |
| React Router con ruta de layout protegida | Una sola verificación de sesión cubre todos los portales hijos vía `<Outlet/>`; guard adicional por rol |
| Axios con interceptores | Inyección de `Authorization` y `X-Correlation-Id` por sesión; normalización de Problem Details (RFC 7807) |
| CSS propio con design tokens | Marca `#C10230`/`#C10130` sobre blancos, responsivo y accesible (focus visible, aria-live, labels) |

Referencias: [guía de autenticación con React Router (WorkOS, 2026)](https://workos.com/blog/react-router-authentication-guide-2026),
[rutas protegidas y RBAC](https://react.wiki/router/protected-routes/),
[prácticas de datos con TanStack Query](https://rtcamp.com/handbook/react-best-practices/data-loading/).

## Estructura

```
src/
├── app/                # composición: providers, router, tema
├── shared/
│   ├── api/            # cliente axios, ApiError, sesión y correlación
│   ├── auth/           # AuthContext, guards por rol, roles del contrato
│   └── ui/             # layout, toasts y componentes base
└── features/           # un módulo por portal: api tipada + página
    ├── login/
    ├── academic/       # rol Academic (contrato 13.2)
    ├── finance/        # rol Finance (13.3)
    ├── teacher/        # rol Teacher (13.4)
    └── director/       # rol Director (13.5)
```

## Manejo de errores (contrato 13.6)

- 401: se limpia el token y se redirige al login.
- 403: aviso de rol insuficiente sin cerrar sesión.
- 409: se muestra el mensaje de negocio del Problem Details.
- 5xx / sin conexión: toast con el `traceId` para soporte, sin bloquear el portal.

Los portales cuyo backend aún no está implementado (Payments, Attendance, Notifications)
muestran un estado "módulo no disponible" y funcionarán sin cambios cuando esos servicios
se publiquen, porque siguen las rutas y payloads del contrato.

## Scripts

- `npm run dev`: servidor de desarrollo en el puerto 3000.
- `npm run build`: verificación de tipos + build de producción.
- `npm run lint`: ESLint.
