import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { LoginForm } from "./LoginForm";

function renderLogin() {
  const onLogin = jest.fn();
  render(<LoginForm onLogin={onLogin} />);
  return {
    onLogin,
    signIn: () => userEvent.click(screen.getByRole("button", { name: /sign in/i })),
  };
}

it("submits the trimmed user id", async () => {
  const { onLogin, signIn } = renderLogin();

  await userEvent.type(screen.getByLabelText(/user id/i), "  alice ");
  await signIn();

  expect(onLogin).toHaveBeenCalledWith("alice");
});

it("blocks an empty user id and shows an error", async () => {
  const { onLogin, signIn } = renderLogin();

  await signIn();

  expect(onLogin).not.toHaveBeenCalled();
  expect(screen.getByRole("alert")).toHaveTextContent(/user id is required/i);
});
