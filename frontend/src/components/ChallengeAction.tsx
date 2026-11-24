import { useId, useState } from "react";
import { ArrowUpRight } from "lucide-react";
import { request } from "../services/api";
import { ErrorNotice } from "./Feedback";
import { challengeEndpoints } from "../services/challengeEndpoints";

export function ChallengeAction({
  ids,
  token,
  companyId,
  onExecuted,
}: {
  ids: string[];
  token: string;
  companyId: string;
  onExecuted?: () => void;
}) {
  const [active, setActive] = useState("");
  const [error, setError] = useState<unknown>();
  const [result, setResult] = useState<{ label: string; data: unknown }>();
  const [operation, setOperation] = useState("");
  const [payload, setPayload] = useState("");
  const [snapshotId, setSnapshotId] = useState("");
  const fieldId = useId();
  const endpoints = ids.flatMap((id) => challengeEndpoints[id] ?? []);
  const endpoint = operation ? endpoints[Number(operation) - 1] : undefined;
  const needsSnapshot = endpoint?.path.includes("{snapshotId}");

  async function execute(label: string, path: string, options: RequestInit, refresh = false) {
    setActive(label);
    setError(undefined);
    setResult(undefined);
    try {
      const data = await request(path, token, companyId, options);
      setResult({ label, data });
      if (refresh) onExecuted?.();
    } catch (err) {
      setError(err);
    } finally {
      setActive("");
    }
  }
  return (
    <section className="challenge-strip">
      <div>
        <span className="eyebrow">Capacidades académicas</span>
        <p>
          Ejecuta un ejemplo o envía tus datos a la API.
        </p>
      </div>
      <div className="button-row">
        {ids.map((id) => (
          <button
            key={id}
            className="button ghost"
            onClick={() => execute(id, `/challenges/${id}/execute`, { method: "POST", body: "{}" })}
            disabled={!!active}
          >
            {active === id ? "Consultando…" : id}
            <ArrowUpRight size={15} />
          </button>
        ))}
      </div>
      {endpoints.length > 0 && (
        <details className="challenge-workbench">
          <summary>Ejecutar con datos propios</summary>
          <form onSubmit={(event) => {
            event.preventDefault();
            if (!endpoint) return;
            const body = endpoint.example ? payload : undefined;
            if (body !== undefined) {
              try { JSON.parse(body); }
              catch {
                setResult(undefined);
                setError(new Error("El cuerpo debe ser JSON válido."));
                return;
              }
            }
            const path = endpoint.path.replace("{snapshotId}", encodeURIComponent(snapshotId.trim()));
            void execute(endpoint.label, path, { method: endpoint.method ?? "POST", body }, true);
          }}>
            <div className="challenge-field">
              <label htmlFor={`${fieldId}-operation`}>Operación</label>
              <select id={`${fieldId}-operation`} value={operation} onChange={(event) => {
                const selection = event.target.value;
                setOperation(selection);
                const example = endpoints[Number(selection) - 1]?.example;
                const template = example
                  ?.replaceAll("00000000-0000-0000-0000-000000000000", companyId)
                  .replace(/("(?:operationId|eventId)":)""/, (_, field) => `${field}"${crypto.randomUUID()}"`);
                setPayload(template ? JSON.stringify(JSON.parse(template), null, 2) : "");
              }}>
                <option value="">Selecciona una operación</option>
                {endpoints.map((entry, index) => (
                  <option key={entry.label} value={index + 1}>{entry.label}</option>
                ))}
              </select>
            </div>
            {endpoint?.example && (
              <div className="challenge-field">
                <label htmlFor={`${fieldId}-payload`}>Solicitud JSON</label>
                <textarea id={`${fieldId}-payload`} required value={payload} rows={7} spellCheck={false}
                  onChange={(event) => setPayload(event.target.value)} />
              </div>
            )}
            {needsSnapshot && <div className="challenge-field">
              <label htmlFor={`${fieldId}-snapshot`}>Identificador de la versión</label>
              <input id={`${fieldId}-snapshot`} required value={snapshotId}
                onChange={(event) => setSnapshotId(event.target.value)} />
            </div>}
            {endpoint && <button className="button secondary" disabled={!!active}>{endpoint.label}</button>}
          </form>
        </details>
      )}
      <ErrorNotice error={error} />
      {result && <div className="challenge-result" role="status">
        <strong>{result.label}</strong>
        <pre className="json-result">{JSON.stringify(result.data, null, 2)}</pre>
      </div>}
    </section>
  );
}
