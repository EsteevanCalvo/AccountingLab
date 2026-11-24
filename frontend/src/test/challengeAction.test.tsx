import { fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { expect, it, vi } from "vitest";
import { ChallengeAction } from "../components/ChallengeAction";

it("muestra el resultado decimal devuelto por un ejemplo académico", async () => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue(Response.json("125.0001")));
  render(<ChallengeAction ids={["DP-08"]} token="test-token" companyId="test-company" />);

  await userEvent.click(screen.getByRole("button", { name: "DP-08" }));

  expect(await screen.findByRole("status")).toHaveTextContent("125.0001");
});

it("envía los datos del formulario al endpoint real conservando el texto decimal", async () => {
  const fetchMock = vi.fn().mockResolvedValue(Response.json("999999999999999.1250"));
  const onExecuted = vi.fn();
  vi.stubGlobal("fetch", fetchMock);
  render(<ChallengeAction ids={["DP-08"]} token="test-token" companyId="test-company" onExecuted={onExecuted} />);
  const user = userEvent.setup();
  await user.click(screen.getByText("Ejecutar con datos propios"));
  await user.selectOptions(screen.getByLabelText("Operación"), "1");
  const body = '{"code":"1105","allowsPosting":true,"ownBalance":"999999999999999.1250","children":[]}';
  fireEvent.change(screen.getByLabelText("Solicitud JSON"), { target: { value: body } });

  await user.click(screen.getByRole("button", { name: "Totalizar jerarquía" }));

  expect(await screen.findByRole("status")).toHaveTextContent("999999999999999.1250");
  expect(fetchMock).toHaveBeenCalledWith(expect.stringMatching(/\/accounts\/tree-total$/),
    expect.objectContaining({ method: "POST", body }));
  expect(onExecuted).toHaveBeenCalledOnce();
});
