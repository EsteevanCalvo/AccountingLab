# Evidencia de ejecución de AccountingLab

La verificación se ejecutó el 24 de noviembre de 2026 en Windows con .NET SDK 8.0.426, Node 22.14.0, MySQL 8.0.46 y Microsoft Edge. La base de pruebas y la base de demostración fueron independientes. La API y la web se ejecutaron en el host. Docker Compose quedó disponible como configuración de arranque del proyecto.

## Compilación y pruebas

```powershell
dotnet restore AccountingLab.sln --locked-mode
dotnet build AccountingLab.sln --configuration Release --no-restore --no-incremental --disable-build-servers -m:1
npm --prefix frontend run build
```

La compilación Release terminó con cero errores y advertencias. TypeScript y Vite construyeron la web. Los reportes de VSTest y Vitest conservan los resultados originales de cada prueba. [Backend](build-backend.log) · [Web](build-frontend.log) · [Resumen](SUMMARY.md)

La batería completa obtuvo salida 0 en Debug. Control de aplicaciones de Windows impidió cargar el ensamblado de arquitectura en Release. Las seis pruebas se ejecutaron en Debug con el mismo código fuente. [Salida del bloqueo](architecture-release-environment.txt) · [Batería completa](test-run.log)

```powershell
dotnet build AccountingLab.sln --configuration Debug --no-restore --disable-build-servers -m:1
pwsh -NoProfile -File scripts/Invoke-Tests.ps1 -Category All -Configuration Debug -NoBuild -ResultsDirectory artifacts/student-debug-run
```

| Suite | Aprobadas | Evidencia |
|---|---:|---|
| Soporte | 93 | [TRX unitario](UnitTests/results.trx) |
| Integración con MySQL | 81 | [TRX de integración](IntegrationTests/results.trx) |
| Arquitectura | 6 | [TRX de arquitectura](ArchitectureTests/results.trx) |
| Patrones | 66 | [TRX académico](PatternChallenges/results.trx) |
| Frontend | 27 | [JSON de Vitest](Frontend/results.json) |
| **Total** | **273** | [Resumen](SUMMARY.md) |

Las pruebas añadidas cubren las 22 rutas educativas, el cierre tras emisión de factura, reversiones contables, reintentos concurrentes, rollback, aislamiento de versiones y eventos y conservación de cadenas decimales en la web. [Casos de integración](../../tests/AccountingLab.IntegrationTests/ImplementedChallengesTests.cs) · [Casos web](../../frontend/src/test/challengeAction.test.tsx)

## Migraciones

Se ejecutó dos veces `dotnet AccountingLab.Api.dll --migrate-only` sobre la base de demostración. La consulta del historial de EF confirmó la migración inicial y la de operaciones avanzadas. Los importes aplicados y remanentes se registran como `DECIMAL(19,4)`. [Consulta real de MySQL](mysql-migrations.txt)

## API y navegador

Se consultaron `/health/ready` y OpenAPI. Después de autenticarse, se recorrieron las 22 rutas educativas sobre la API real y todas devolvieron HTTP 200. En Edge se inició sesión, se abrió el catálogo y se comprobó que Composite devuelve `125` para el ejemplo y `999999999999999.1251` para una jerarquía enviada con datos propios.

- [Las 22 respuestas HTTP y el recorrido en navegador](http-smoke.json)
- [Captura del ejemplo de Composite](web-dp08.png)
- [Captura de Composite con datos propios](web-dp08-datos.png)
- [Validación de sintaxis de los 22 diagramas UML](uml-validation.txt)

Para repetir una consulta con la API y una empresa autenticada, usa un token de inicio de sesión y el identificador de empresa devuelto.

```powershell
$headers = @{ Authorization = "Bearer $token"; 'X-Company-Id' = $companyId }
Invoke-RestMethod http://localhost:5208/health/ready
$catalog = Invoke-RestMethod http://localhost:5208/api/challenges -Headers $headers
foreach ($challenge in $catalog) {
    Invoke-RestMethod "http://localhost:5208/api/challenges/$($challenge.id)/execute" `
        -Method Post -Headers $headers -ContentType 'application/json' -Body '{}'
}
```
