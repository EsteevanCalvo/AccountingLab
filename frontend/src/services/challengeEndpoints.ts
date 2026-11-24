type Endpoint = {
  path: string;
  label: string;
  method?: "GET" | "DELETE";
  example?: string;
};

export const challengeEndpoints: Record<string, Endpoint[]> = {
  "DP-01": [{ path: "/vouchers/generate", label: "Generar borrador de comprobante", example: '{"companyId":"00000000-0000-0000-0000-000000000000","kind":"ingreso","amount":"100.0000","currency":"COP"}' }],
  "DP-02": [{ path: "/companies/profile-services", label: "Crear familia de servicios", example: '{"profile":"comercial","fictionalRegime":"DEMO-A"}' }],
  "DP-03": [{ path: "/reports/build", label: "Armar informe", example: '{"title":"Balance","from":"2026-01-01","to":"2026-12-31","sections":["Activo"],"annexes":[]}' }],
  "DP-04": [{ path: "/templates/copy", label: "Copiar plantilla", example: '{"name":"Arriendo","lines":[{"accountCode":"5105","amount":"450.0000","tags":["mensual"]}]}' }],
  "DP-05": [{ path: "/configuration/registry", label: "Consultar registro" }],
  "DP-06": [{ path: "/bank-statements/import", label: "Normalizar extracto", example: '{"provider":"BancoA","payload":"[]"}' }],
  "DP-07": [{ path: "/reports/render", label: "Representar informe", example: '{"reportType":"balance","format":"csv","amount":"100.0000"}' }],
  "DP-08": [{ path: "/accounts/tree-total", label: "Totalizar jerarquía", example: '{"code":"1105","allowsPosting":true,"ownBalance":"25.0000","children":[]}' }],
  "DP-09": [{ path: "/exports/enriched", label: "Exportar con características", example: '{"body":"Comprobante","features":["auditoria","metadatos"],"metadata":{"origen":"laboratorio"}}' }],
  "DP-10": [{ path: "/accounting-periods/close", label: "Confirmar cierre de periodo", example: '{"companyId":"00000000-0000-0000-0000-000000000000","from":"2026-01-01","to":"2026-12-31","balances":[],"unpostedDocuments":0}' }],
  "DP-11": [{ path: "/movements/classifications", label: "Clasificar movimientos", example: '[]' }],
  "DP-12": [{ path: "/reports/confidential", label: "Consultar informe confidencial", example: '{"companyId":"00000000-0000-0000-0000-000000000000","mayRead":true,"reportKey":"balance"}' }],
  "DP-13": [{ path: "/payments/approve", label: "Evaluar aprobación", example: '{"amount":"100.0000","availableFunds":"500.0000","policyAllows":true,"authorizationLimit":"200.0000","checks":["fondos","politica","limite"]}' }],
  "DP-14": [{ path: "/operations/execute", label: "Confirmar cobro, pago o reversión", example: '{"request":{"companyId":"00000000-0000-0000-0000-000000000000","operationId":"","kind":"apply-receipt","targetId":"","amount":"100.0000"},"context":{"outstandingAmount":"0.0000","targetIsPublished":false,"completedOperations":[]}}' }],
  "DP-15": [{ path: "/ledger/traverse", label: "Recorrer páginas", example: '[{"records":[{"entryCode":"DEMO","date":"2026-01-01","debit":"25.0000","credit":"0.0000"}]}]' }],
  "DP-16": [{ path: "/invoices/process", label: "Coordinar procesamiento", example: '{"invoiceId":"","amount":"100.0000","isValid":true,"participants":["validacion","contabilizacion","publicacion"]}' }],
  "DP-17": [
    { path: "/budgets/snapshots", label: "Guardar versión de borrador", example: '{"id":"","name":"Presupuesto","approved":false,"lines":[{"accountCode":"5105","amount":"100.0000"}]}' },
    { path: "/budgets/snapshots/{snapshotId}/restore", label: "Restaurar versión de borrador", example: '{"id":"","name":"Presupuesto modificado","approved":false,"lines":[]}' },
  ],
  "DP-18": [
    { path: "/notifications/publish", label: "Publicar evento", example: '{"eventId":"","kind":"pago-registrado","amount":"100.0000"}' },
    { path: "/notifications/subscriptions", label: "Activar mi suscripción" },
    { path: "/notifications/subscriptions", label: "Cancelar mi suscripción", method: "DELETE" },
    { path: "/notifications/inbox", label: "Leer mis eventos", method: "GET" },
  ],
  "DP-19": [{ path: "/invoices/transition", label: "Evaluar transición", example: '{"currentState":"draft","action":"emit","outstandingAmount":"100.0000","dueDate":"2026-12-31","businessDate":"2026-10-23"}' }],
  "DP-20": [{ path: "/taxes/calculate", label: "Calcular impuesto ficticio", example: '{"baseAmount":"100.0000","operationKind":"venta","accountingDate":"2026-10-23","rules":[{"name":"DEMO-A","operationKind":"venta","validFrom":"2026-01-01","validTo":"2026-12-31","calculationKind":"percentage","rate":"0.1375","fixedAmount":"0.0000"}]}' }],
  "DP-21": [{ path: "/bank-reconciliation/reconcile", label: "Conciliar movimientos", example: '{"mode":"exact","bankItems":[],"ledgerItems":[],"tolerance":"0.0000"}' }],
  "DP-22": [{ path: "/documents/analyze", label: "Analizar documentos", example: '{"view":"gross","documents":[]}' }],
};
