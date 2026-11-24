# AccountingLab

Laboratorio de contabilidad con API .NET 8, MySQL y una aplicación React. El catálogo de la web ejecuta ejemplos para los 22 patrones y permite usar las rutas de módulo con datos propios. Las operaciones de cierre, aplicación de cobros y pagos, reversión y emisión de facturas se confirman en MySQL.

## Ejecutar con Docker Compose

1. Configura `.env` con las variables de `.env.example` y credenciales locales.
2. Ejecuta `docker compose up --build -d --wait mysql api frontend`. El servicio `migrate` aplica el esquema y prepara los datos de demostración.
3. Abre la web en `http://localhost:5173` o Swagger en `http://localhost:5208/swagger`. Usa el correo y la contraseña de demostración configurados en `.env`.

Para detener los servicios sin borrar los datos de MySQL:

```powershell
docker compose down
```

## Ejecutar con MySQL instalado

Crea una base de demostración y otra base desechable para las pruebas. Desde la raíz, configura `ConnectionStrings__AccountingLab`, `Demo__Email`, `Demo__Password` y `Authentication__SigningKey` como variables de entorno. La contraseña demo requiere 12 caracteres y la clave de firma requiere 32 bytes como mínimo.

```powershell
dotnet restore AccountingLab.sln --locked-mode
dotnet run --project src/AccountingLab.Api -- --migrate-only
dotnet run --project src/AccountingLab.Api -- --urls http://localhost:5208
```

En otra terminal, usa Node 22 y ejecuta `npm --prefix frontend ci` y `npm --prefix frontend run dev`.

## Pruebas y documentación

La batería completa necesita una base MySQL desechable. Puedes iniciarla con el perfil de pruebas de Compose y configurar la conexión en la terminal actual:

```powershell
docker compose --profile tests up -d mysql-test
$env:ConnectionStrings__AccountingLabTest = "Server=127.0.0.1;Port=3309;Database=accountinglab_tests;User=accountinglab_test;Password=$env:MYSQL_TEST_PASSWORD;"
```

Si tu instalación de Node muestra una versión 26, el repositorio incluye Node 22 para ejecutar las pruebas del frontend en esta terminal:

```powershell
$node22 = Join-Path (Get-Location) ".tools\verification-node\node_modules\node\bin"
$env:Path = "$node22;$env:Path"
node --version
```

La versión debe comenzar por `v22`. Después instala dependencias y ejecuta la batería:

```powershell
dotnet restore AccountingLab.sln --locked-mode
npm --prefix frontend ci
pwsh -NoProfile -File scripts/Invoke-Tests.ps1 -Category All -ResultsDirectory reports/student
npm --prefix frontend run build
```

La ejecución verificada más reciente aprobó 273 pruebas. Los resultados se guardan en `reports/student/`.

- [Documento técnico de los 22 patrones](docs/SOLUCION_PATRONES.md)
- [Diagramas UML](docs/UML_PATRONES.md)
- [Contratos HTTP](docs/CONTRATOS_API.md)
- [Migraciones](database/migrations/README.md)
- [Resultados de pruebas](reports/student/SUMMARY.md)
- [Evidencia de ejecución](reports/student/EVIDENCIA.md)
