"use client";

import { FormEvent, useState } from "react";

export function LoginForm({ onLogin }: { onLogin: (userId: string) => void }) {
  const [userId, setUserId] = useState("");
  const [error, setError] = useState<string | null>(null);

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    const trimmed = userId.trim();
    if (trimmed === "") {
      setError("User ID is required.");
      return;
    }
    setError(null);
    onLogin(trimmed);
  }

  return (
    <form className="card" onSubmit={handleSubmit} noValidate>
      <h2>Sign in</h2>
      <p className="hint">Enter any user ID to continue. There is no password: this is a simulated login.</p>
      <label htmlFor="userId">User ID</label>
      <input id="userId" value={userId} onChange={(e) => setUserId(e.target.value)} autoComplete="username" />
      {error && <p role="alert" className="error">{error}</p>}
      <button type="submit">Sign in</button>
    </form>
  );
}
