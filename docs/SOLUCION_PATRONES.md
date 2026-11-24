# Solución técnica de los 22 patrones

AccountingLab aplica los patrones en las zonas de desafío de Domain, Application e Infrastructure. La API conserva los contratos de entrada y la web consume sus resultados por HTTP. Las operaciones de módulo que confirman hechos financieros leen los datos de la empresa autenticada y escriben el asiento, el estado relacionado y la auditoría en una transacción. Los ejemplos del catálogo ilustran las decisiones con entradas del laboratorio.

Las reglas de cálculo y los modelos de los desafíos evitan dependencias de ASP.NET Core y Entity Framework. Los importes viajan como cadenas decimales y los datos persistidos conservan hasta cuatro posiciones. Cada sección enlaza el UML de sus participantes y describe cómo se ejercita la solución. [Arquitectura](ARQUITECTURA.md) · [Contratos HTTP](CONTRATOS_API.md) · [Evidencia](../reports/student/EVIDENCIA.md) · [Catálogo conceptual de Refactoring.Guru](https://refactoring.guru/es/design-patterns/catalog)

## DP-01 Factory Method

Los comprobantes de ingreso, egreso, ajuste y traslado necesitan identidad y datos válidos antes de poder reutilizarse como borradores.

VoucherCreationChallenge resuelve el creador. La clase Creator valida datos comunes y delega CreateDraft a ReceiptCreator, PaymentCreator, AdjustmentCreator o TransferCreator. Domain concentra la regla y abre la creación a variantes mediante polimorfismo.

El registro de creadores requiere una entrada por tipo. Los cuatro productos comparten el DTO VoucherDraft y su creación carece de efectos en el libro.

**Verificación.** Identidad nueva, preservación del importe y la moneda, rechazo de tipo desconocido y soporte de los cuatro tipos a importe cero. [UML](UML_PATRONES.md#dp-01-factory-method) · [Código](../src/AccountingLab.Domain/Challenges/DP-01_FACTORY_METHOD/Challenge.cs) · [Pruebas](../tests/AccountingLab.PatternChallenges/DP-01_FACTORY_METHOD/ChallengeTests.cs)

## DP-02 Abstract Factory

Una empresa necesita que cálculo, validación e informes compartan las convenciones de su perfil y régimen ficticio.

AccountingProfileChallenge elige una FamilyFactory. CommercialFactory y ServicesFactory crean los tres roles bajo una misma clave. La abstracción de Domain permite consumir la familia completa sin acoplar clientes a cada variante.

Agregar perfiles exige una fábrica adicional. Agregar un rol cambia la interfaz de creación y todas las familias. La salida enumera descriptores de servicio compatibles.

**Verificación.** Tres roles, clave uniforme, distinción entre perfiles y rechazo del perfil desconocido. [UML](UML_PATRONES.md#dp-02-abstract-factory) · [Código](../src/AccountingLab.Domain/Challenges/DP-02_ABSTRACT_FACTORY/Challenge.cs) · [Pruebas](../tests/AccountingLab.PatternChallenges/DP-02_ABSTRACT_FACTORY/ChallengeTests.cs)

## DP-03 Builder

Los informes deben conservar orden, periodo, secciones y anexos sin multiplicar constructores para cada combinación.

ReportAssemblyChallenge dirige la construcción. FinancialReportBuilder valida el intervalo, recibe los componentes en pasos y publica un FinancialReport con listas independientes. Application mantiene el armado separado de la representación y de los datos persistidos.

El paso de armado agrega un objeto temporal. El contrato de este informe describe estructura y filtros.

**Verificación.** Preservación del orden y las fechas, informe mínimo y rechazo de intervalo invertido. [UML](UML_PATRONES.md#dp-03-builder) · [Código](../src/AccountingLab.Application/Challenges/DP-03_BUILDER/Challenge.cs) · [Pruebas](../tests/AccountingLab.PatternChallenges/DP-03_BUILDER/ChallengeTests.cs)

## DP-04 Prototype

Una plantilla recurrente sirve como punto de partida para otra operación sin que modificaciones en montos o etiquetas alcancen a la original.

TemplateCopyChallenge valida importes y solicita la copia al prototipo RecurringTemplate. La plantilla genera un nuevo identificador y cada TemplateLine copia sus etiquetas. Domain conserva la responsabilidad de clonar junto al objeto que conoce su estructura.

La copia profunda requiere memoria y tiempo según el tamaño. Un nuevo campo mutable requiere incorporarse a la copia.

**Verificación.** Identidad distinta, preservación de valores, independencia de renglones y etiquetas y rechazo de fuente nula. [UML](UML_PATRONES.md#dp-04-prototype) · [Código](../src/AccountingLab.Domain/Challenges/DP-04_PROTOTYPE/Challenge.cs) · [Pruebas](../tests/AccountingLab.PatternChallenges/DP-04_PROTOTYPE/ChallengeTests.cs)

## DP-05 Singleton

Versión y precisión contable deben consultarse desde una referencia estable dentro del proceso.

GlobalRegistryChallenge expone el acceso al único GlobalAccountingRegistry registrado en un campo estático de solo lectura. El runtime de .NET protege la inicialización. Domain concentra una configuración inmutable y los consumidores pueden depender de IGlobalRegistryChallenge.

El alcance es cada proceso y los valores se fijan al compilar. Una réplica crea su propia identidad de objeto.

**Verificación.** Identidad compartida entre servicios, concurrencia y preservación de la configuración ante copias del record. [UML](UML_PATRONES.md#dp-05-singleton) · [Código](../src/AccountingLab.Domain/Challenges/DP-05_SINGLETON/Challenge.cs) · [Pruebas](../tests/AccountingLab.PatternChallenges/DP-05_SINGLETON/ChallengeTests.cs)

## DP-06 Adapter

Dos bancos ficticios nombran los datos de forma incompatible y uno expresa el débito y el crédito mediante el signo de un único importe.

BankImportChallenge selecciona IBankAdapter y valida la forma del JSON. BankAAdapter traduce débitos y créditos separados. BankBAdapter parte el importe firmado. Infrastructure contiene la dependencia del formato de integración y publica ImportedBankMovement.

Cada proveedor nuevo requiere un adaptador y su registro. La salida normalizada se devuelve al solicitante para su revisión.

**Verificación.** Lectura de los dos proveedores, importes sin redondeo, conversión de débito y cero, y rechazo de JSON mal formado. [UML](UML_PATRONES.md#dp-06-adapter) · [Código](../src/AccountingLab.Infrastructure/Challenges/DP-06_ADAPTER/Challenge.cs) · [Pruebas](../tests/AccountingLab.PatternChallenges/DP-06_ADAPTER/ChallengeTests.cs)

## DP-07 Bridge

Un balance y un estado de resultados deben poder representarse en JSON y CSV sin duplicar las combinaciones.

BalanceReport y ResultsReport especializan FinancialReport. Cada instancia delega la representación a IReportRenderer. JsonRenderer y CsvRenderer escriben el importe decimal con cuatro posiciones. Application mantiene ambos ejes de variación con dependencias hacia una abstracción.

Dos jerarquías agregan tipos e instancias. La salida contiene el par de campos reportType y amount del contrato.

**Verificación.** Cuatro combinaciones, tipo de contenido correcto, formato inválido y representación exacta del importe cero. [UML](UML_PATRONES.md#dp-07-bridge) · [Código](../src/AccountingLab.Application/Challenges/DP-07_BRIDGE/Challenge.cs) · [Pruebas](../tests/AccountingLab.PatternChallenges/DP-07_BRIDGE/ChallengeTests.cs)

## DP-08 Composite

Una consulta debe sumar por igual una cuenta de movimiento o una agrupación con más niveles.

AccountTreeChallenge adapta AccountHierarchy a IAccountComponent. PostingAccount aporta su saldo y AccountGroup suma componentes hijos por la misma propiedad Balance. Domain encapsula la recursión y permite que el cliente trate ambas variantes de forma uniforme.

La proyección del árbol usa memoria y la suma recursiva consume pila en función de la profundidad. Se rechazan ciclos, saldos propios de agrupadoras y descendientes de cuentas de movimiento.

**Verificación.** Cuentas y grupos anidados, agrupadora vacía y rechazo de hija bajo una cuenta de movimiento. [UML](UML_PATRONES.md#dp-08-composite) · [Código](../src/AccountingLab.Domain/Challenges/DP-08_COMPOSITE/Challenge.cs) · [Pruebas](../tests/AccountingLab.PatternChallenges/DP-08_COMPOSITE/ChallengeTests.cs)

## DP-09 Decorator

Un mismo documento se exporta con auditoría, metadatos, ambos o ninguno.

DocumentEnrichmentChallenge empieza con PlainDocument y compone capas que implementan IDocument. DocumentDecorator delega en el elemento interno. AuditDocument agrega la marca y MetadataDocument adjunta una copia de los datos. Application mantiene cada extensión en una clase independiente.

El orden de capas queda reflejado en las características aplicadas. La composición genera resultados intermedios según el número de capas.

**Verificación.** Combinación de características, preservación del original y del contenido simple y rechazo de características desconocidas. [UML](UML_PATRONES.md#dp-09-decorator) · [Código](../src/AccountingLab.Application/Challenges/DP-09_DECORATOR/Challenge.cs) · [Pruebas](../tests/AccountingLab.PatternChallenges/DP-09_DECORATOR/ChallengeTests.cs)

## DP-10 Facade

Para cerrar un periodo hay que resolver fechas, verificar documentos pendientes y comprobar que débitos y créditos coinciden.

PeriodClosingChallenge ofrece Close como punto de entrada de Application y coordina las comprobaciones. EfAdvancedAccountingStore obtiene saldos y pendientes de la empresa y confirma estado y auditoría en una transacción. El puerto IAdvancedAccountingStore mantiene Entity Framework fuera de Application.

El cierre busca las fechas exactas del periodo y considera facturas con fecha de emisión y asientos del intervalo. Los borradores de pago carecen de fecha contable en el modelo original. El bloqueo por empresa coordina el cierre con la contabilización y serializa esas escrituras.

**Verificación.** Cierre balanceado, rechazo por pendientes o descuadre de 0.0001 y prueba HTTP de bloqueo de contabilización tras el cierre. [UML](UML_PATRONES.md#dp-10-facade) · [Código](../src/AccountingLab.Application/Challenges/DP-10_FACADE/Challenge.cs) · [Pruebas](../tests/AccountingLab.PatternChallenges/DP-10_FACADE/ChallengeTests.cs)

## DP-11 Flyweight

Un lote masivo puede repetir etiquetas, códigos y ámbitos ficticios en miles de movimientos.

ClassificationFactory reutiliza una ClassificationMetadata inmutable por combinación de código, etiqueta y ámbito. ClassifiedMovement mantiene importe e identificador propios. Domain aísla el estado particular del estado compartido.

La tabla vive durante un lote y su costo depende de las combinaciones distintas. Al cruzar HTTP la serialización comunica valores y cada elemento puede repetir los metadatos.

**Verificación.** Identidad compartida para claves iguales, distinción entre ámbitos y preservación de importes e identificadores independientes. [UML](UML_PATRONES.md#dp-11-flyweight) · [Código](../src/AccountingLab.Domain/Challenges/DP-11_FLYWEIGHT/Challenge.cs) · [Pruebas](../tests/AccountingLab.PatternChallenges/DP-11_FLYWEIGHT/ChallengeTests.cs)

## DP-12 Proxy

El acceso a un reporte confidencial necesita autorización previa a una lectura y comprobación de pertenencia después de ella.

SensitiveReportChallenge representa la fuente mediante una función de carga diferida. El proxy verifica permiso, invoca el proveedor y comprueba empresa y clave. El controlador suministra autorización desde TenantContext y carga el resumen de la empresa en forma asíncrona.

Cada consulta hace una lectura. El proveedor actual usa el total de débitos del resumen bajo la clave solicitada.

**Verificación.** Acceso autorizado, cero lecturas al denegar y rechazo de resultados de otra empresa. [UML](UML_PATRONES.md#dp-12-proxy) · [Código](../src/AccountingLab.Application/Challenges/DP-12_PROXY/Challenge.cs) · [Pruebas](../tests/AccountingLab.PatternChallenges/DP-12_PROXY/ChallengeTests.cs)

## DP-13 Chain of Responsibility

Los procesos de pago requieren un orden configurable de disponibilidad, política y límites de autorización.

PaymentApprovalChallenge construye los eslabones de ApprovalCheck. FundsCheck, PolicyCheck y LimitCheck evalúan una condición cada uno y el eslabón base registra la visita y decide si delega. Domain conserva la responsabilidad de cada regla y la composición documenta el orden.

El tamaño de la cadena afecta las llamadas. Una configuración vacía aplica cero controles. El resultado evalúa los datos suministrados.

**Verificación.** Secuencia configurable, interrupción en el primer rechazo y límites inclusivos. [UML](UML_PATRONES.md#dp-13-chain-of-responsibility) · [Código](../src/AccountingLab.Domain/Challenges/DP-13_CHAIN_OF_RESPONSIBILITY/Challenge.cs) · [Pruebas](../tests/AccountingLab.PatternChallenges/DP-13_CHAIN_OF_RESPONSIBILITY/ChallengeTests.cs)

## DP-14 Command

Cobros, pagos y reversiones requieren trazabilidad y el mismo resultado al reintentar una operación confirmada.

IdentifiedOperationChallenge despacha un AccountingCommand por tipo y reconoce reintentos a partir de la historia. EfAdvancedAccountingStore materializa el efecto con saldo de MySQL, asiento balanceado, recibo y auditoría en una transacción. El identificador se hace único por empresa y una reversión registra un asiento inverso vinculado.

La API usa la fecha corriente de Bogotá y exige un periodo abierto. La caja es 1105, clientes 1305 y proveedores 2205. Una reversión de factura exige revertir primero sus cobros activos. El importe original y el historial de cada obligación se conservan.

**Verificación.** Aplicación y reversión vinculadas, replay, rechazo del borrador e integración de concurrencia, rollback y aislamiento. [UML](UML_PATRONES.md#dp-14-command) · [Código](../src/AccountingLab.Domain/Challenges/DP-14_COMMAND/Challenge.cs) · [Pruebas](../tests/AccountingLab.PatternChallenges/DP-14_COMMAND/ChallengeTests.cs)

## DP-15 Iterator

El consumidor del libro necesita recorrer todos los registros en orden aunque las páginas intermedias estén vacías.

LedgerTraversalChallenge abre un PagedLedgerCursor bajo ILedgerCursor. El cursor mantiene su posición y expone Current solo cuando el recorrido apunta a un registro. El adaptador HTTP consume el cursor y devuelve su secuencia.

El cursor presupone que las páginas entregadas permanecen estables. El controlador materializa la respuesta para enviarla como JSON.

**Verificación.** Orden entre páginas, páginas vacías y error de Current fuera del recorrido. [UML](UML_PATRONES.md#dp-15-iterator) · [Código](../src/AccountingLab.Application/Challenges/DP-15_ITERATOR/Challenge.cs) · [Pruebas](../tests/AccountingLab.PatternChallenges/DP-15_ITERATOR/ChallengeTests.cs)

## DP-16 Mediator

Validación, contabilización y publicación se ejecutan en un orden fijo que evita coordinarse entre sí.

InvoiceCoordinationChallenge registra InvoiceValidator, InvoicePosting e InvoicePublication. InvoiceMediator recibe los resultados de cada participante y decide qué etapa invocar. IInvoiceProcessingEffects permite que la infraestructura registre el asiento, la cuenta por cobrar y el estado Issued atómicamente.

La emisión usa 1305 para clientes y 4135 para ingresos. El borrador del laboratorio representa una factura de venta por el importe registrado. La coordinación y el orden de etapas dependen del mediador.

**Verificación.** Tres participantes únicos, interrupción tras validación inválida y prueba de publicación idempotente conjunta con cierre. [UML](UML_PATRONES.md#dp-16-mediator) · [Código](../src/AccountingLab.Domain/Challenges/DP-16_MEDIATOR/Challenge.cs) · [Pruebas](../tests/AccountingLab.PatternChallenges/DP-16_MEDIATOR/ChallengeTests.cs)

## DP-17 Memento

Una persona necesita recuperar valores previos de un borrador presupuestal sin alterar la versión guardada ni modificar un presupuesto aprobado.

BudgetHistoryChallenge crea BudgetSnapshot con los valores privados y solo admite restaurarla sobre el mismo borrador. IBudgetSnapshot es el contrato opaco. ChallengeSessions custodia un identificador aleatorio por empresa y usuario.

Cada versión ocupa memoria según sus líneas y caduca una hora después. La API exige que el presupuesto registrado permanezca en borrador y devuelve los valores reconstruidos para revisión. Las versiones desaparecen al reiniciar el proceso.

**Verificación.** Restauración de nombre e importes, independencia de líneas, rechazo de aprobado y aislamiento de la versión por empresa. [UML](UML_PATRONES.md#dp-17-memento) · [Código](../src/AccountingLab.Domain/Challenges/DP-17_MEMENTO/Challenge.cs) · [Pruebas](../tests/AccountingLab.PatternChallenges/DP-17_MEMENTO/ChallengeTests.cs)

## DP-18 Observer

Tesorería y otras partes interesadas necesitan recibir hechos financieros conforme a sus suscripciones activas.

FinancialNotificationsChallenge mantiene las funciones receptoras, toma una copia de las suscripciones y entrega el evento a cada observador. ChallengeSessions instala una bandeja privada por usuario y un publicador por empresa. El emisor depende del contrato de publicación en Domain.

La entrega se hace en el proceso y es síncrona. La bandeja conserva hasta 100 eventos y desaparece al cancelar o reiniciar. Los observadores del ejemplo son distintos de las suscripciones de la empresa.

**Verificación.** Recepción una vez por suscriptor activo, baja, rechazo de duplicados en el dominio e integración de aislamiento. [UML](UML_PATRONES.md#dp-18-observer) · [Código](../src/AccountingLab.Domain/Challenges/DP-18_OBSERVER/Challenge.cs) · [Pruebas](../tests/AccountingLab.PatternChallenges/DP-18_OBSERVER/ChallengeTests.cs)

## DP-19 State

Las transiciones de facturas cambian según el estado actual, el saldo y la fecha de vencimiento.

InvoiceLifecycleChallenge delega la operación a InvoiceState. DraftState emite. IssuedState evalúa el vencimiento. CollectibleState comprueba el saldo al pagar y PaidState protege el estado final. Las reglas específicas permanecen en Domain.

Agregar un estado requiere registrar sus transiciones. La API evalúa el contrato con datos proporcionados y devuelve el nuevo estado y la posibilidad de pago.

**Verificación.** Emisión y pago, rechazo a emitir una factura pagada y comparación de la fecha exacta con el día posterior. [UML](UML_PATRONES.md#dp-19-state) · [Código](../src/AccountingLab.Domain/Challenges/DP-19_STATE/Challenge.cs) · [Pruebas](../tests/AccountingLab.PatternChallenges/DP-19_STATE/ChallengeTests.cs)

## DP-20 Strategy

Un impuesto ficticio puede ser fijo o porcentual y depende de la fecha y el tipo de operación.

ConfigurableTaxChallenge filtra las reglas vigentes y delega el cálculo en ITaxStrategy. PercentageTax multiplica la base. FixedTax devuelve el monto fijo. Domain aplica redondeo monetario común de cuatro posiciones con el punto medio alejado de cero.

Se rechazan vigencias simultáneas para la misma operación. Agregar un algoritmo requiere registrar una estrategia. Las tasas porcentuales aceptan hasta seis posiciones.

**Verificación.** Cálculos fijo y porcentual, ausencia de vigencia y redondeo en el límite inclusivo de vigencia. [UML](UML_PATRONES.md#dp-20-strategy) · [Código](../src/AccountingLab.Domain/Challenges/DP-20_STRATEGY/Challenge.cs) · [Pruebas](../tests/AccountingLab.PatternChallenges/DP-20_STRATEGY/ChallengeTests.cs)

## DP-21 Template Method

La conciliación comparte la comparación por referencia y fecha, el consumo único de registros y el listado de diferencias.

ReconciliationProcedure fija los pasos comunes en Run. ExactReconciliation y ToleranceReconciliation cambian el criterio de diferencia. Application puede probar ambos recorridos con colecciones sin dependencias de persistencia.

Se elige la menor diferencia disponible para cada entrada en orden de extracto. Con candidatos compartidos pueden quedar coincidencias que un optimizador global encontraría. La salida identifica partidas emparejadas y pendientes de revisión.

**Verificación.** Coincidencia exacta, umbral inclusivo, tolerancia negativa y ausencia de reutilización. [UML](UML_PATRONES.md#dp-21-template-method) · [Código](../src/AccountingLab.Application/Challenges/DP-21_TEMPLATE_METHOD/Challenge.cs) · [Pruebas](../tests/AccountingLab.PatternChallenges/DP-21_TEMPLATE_METHOD/ChallengeTests.cs)

## DP-22 Visitor

El mismo conjunto de facturas, pagos y asientos necesita totales por naturaleza y conteos por tipo.

DocumentAnalysisChallenge adapta cada documento a InvoiceElement, PaymentElement o JournalElement. Cada elemento llama al Visit de su tipo. GrossVisitor y CountVisitor implementan operaciones independientes de los modelos de entrada de Domain.

Agregar un análisis requiere un visitante. Agregar un tipo de documento amplía el contrato de todos los visitantes y el discriminador HTTP. Las sumas monetarias mantienen precisión decimal.

**Verificación.** Totales heterogéneos, conteos, vista inválida y colección vacía. [UML](UML_PATRONES.md#dp-22-visitor) · [Código](../src/AccountingLab.Domain/Challenges/DP-22_VISITOR/Challenge.cs) · [Pruebas](../tests/AccountingLab.PatternChallenges/DP-22_VISITOR/ChallengeTests.cs)

