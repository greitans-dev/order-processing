import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { LoginForm } from "./LoginForm";

it("submits the trimmed user id", async () => {
  const onLogin = jest.fn();
  render(<LoginForm onLogin={onLogin} />);

  await userEvent.type(screen.getByLabelText(/user id/i), "  alice ");
  await userEvent.click(screen.getByRole("button", { name: /sign in/i }));

  expect(onLogin).toHaveBeenCalledWith("alice");
});

it("blocks an empty user id and shows an error", async () => {
  const onLogin = jest.fn();
  render(<LoginForm onLogin={onLogin} />);

  await userEvent.click(screen.getByRole("button", { name: /sign in/i }));

  expect(onLogin).not.toHaveBeenCalled();
  expect(screen.getByRole("alert")).toHaveTextContent(/user id is required/i);
});
