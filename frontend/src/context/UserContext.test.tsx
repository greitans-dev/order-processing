import { act, render, screen } from "@testing-library/react";
import { STORAGE_KEY, UserProvider, useUser } from "./UserContext";

function Probe() {
  const { userId, isReady, setUserId, logout } = useUser();
  return (
    <div>
      <span data-testid="state">{isReady ? `ready:${userId ?? "none"}` : "loading"}</span>
      <button onClick={() => setUserId("alice")}>login</button>
      <button onClick={logout}>logout</button>
    </div>
  );
}

beforeEach(() => window.localStorage.clear());

it("starts logged out once hydrated", () => {
  render(<UserProvider><Probe /></UserProvider>);
  expect(screen.getByTestId("state")).toHaveTextContent("ready:none");
});

it("restores the user id from localStorage", () => {
  window.localStorage.setItem(STORAGE_KEY, "bob");
  render(<UserProvider><Probe /></UserProvider>);
  expect(screen.getByTestId("state")).toHaveTextContent("ready:bob");
});

it("persists login and clears it on logout", () => {
  render(<UserProvider><Probe /></UserProvider>);

  act(() => screen.getByText("login").click());
  expect(screen.getByTestId("state")).toHaveTextContent("ready:alice");
  expect(window.localStorage.getItem(STORAGE_KEY)).toBe("alice");

  act(() => screen.getByText("logout").click());
  expect(screen.getByTestId("state")).toHaveTextContent("ready:none");
  expect(window.localStorage.getItem(STORAGE_KEY)).toBeNull();
});

it("throws when used outside a provider", () => {
  const spy = jest.spyOn(console, "error").mockImplementation(() => {});
  expect(() => render(<Probe />)).toThrow(/UserProvider/);
  spy.mockRestore();
});
