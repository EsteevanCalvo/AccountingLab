# Migraciones de MySQL

El origen único del esquema son las migraciones EF Core de `src/AccountingLab.Infrastructure/`. Este directorio documenta su aplicación; no contiene un segundo esquema SQL mantenido en paralelo.

`InitialAccounting.sql` es una exportación idempotente del esquema inicial para inspección. Los archivos C# y su snapshot describen el esquema completo. A `20261023054030_InitialAccounting` le sigue `20261124050604_AdvancedAccountingOperations`, que agrega recibos inmutables de operaciones identificadas y su vínculo con asientos de la misma empresa. Los importes persistidos usan `DECIMAL(19,4)`.

Compose ejecuta el servicio `migrate` con `dotnet AccountingLab.Api.dll --migrate-only`. El proceso espera la base saludable, aplica las migraciones y los datos iniciales y finaliza. La API depende de su salida correcta. El proceso HTTP normal no elimina bases ni vuelve a crear el esquema.

En ejecución directa, configura `ConnectionStrings__AccountingLab`, `Demo__Email` y `Demo__Password` y ejecuta:

```powershell
dotnet run --project src/AccountingLab.Api -- --migrate-only
```

Ejecuta una sola instancia de la migración para una misma base. La repetición conserva los datos existentes. Las pruebas usan una base separada de la demostración. [Evidencia del esquema aplicado](../../reports/student/EVIDENCIA.md)

La existencia de esta guía no acredita que una migración se haya ejecutado. Ese resultado requiere su informe real.
