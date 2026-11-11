# Infiltrate us — Proyecto Unity

Proyecto Unity 6 (2D, URP) del juego **Infiltrate us** con base de datos MariaDB `infiltrate_us_v1`.

## Cómo abrirlo
1. Clonar el repo.
2. En Unity Hub: **Add → Add project from disk** y elegir la carpeta `InfiltrateUs_Unity`.
3. Usar la misma versión de Unity que figura en `ProjectSettings/ProjectVersion.txt`.
4. La primera vez Unity regenera `Library/` (tarda un rato, es normal).

## Base de datos
Los scripts están en `Database/`:
- `01b_triggers_procedimientos_roles.sql`
- `02_semillas_y_extras.sql`

La conexión se configura en el Inspector del objeto `DatabaseManager` (por defecto XAMPP local: `127.0.0.1:3306`, usuario `root`, sin contraseña).
