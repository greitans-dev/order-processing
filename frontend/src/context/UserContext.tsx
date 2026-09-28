"use client";

import { createContext, ReactNode, useCallback, useContext, useEffect, useMemo, useState } from "react";

export const STORAGE_KEY = "orderProcessing.userId";

interface UserContextValue {
  userId: string | null;
  /** False until localStorage has been read on the client, so the UI never flashes the login form. */
  isReady: boolean;
  setUserId: (userId: string) => void;
  logout: () => void;
}

const UserContext = createContext<UserContextValue | null>(null);

function readStored(): string | null {
  try {
    return window.localStorage.getItem(STORAGE_KEY);
  } catch {
    return null;
  }
}

function writeStored(userId: string | null) {
  try {
    if (userId === null) window.localStorage.removeItem(STORAGE_KEY);
    else window.localStorage.setItem(STORAGE_KEY, userId);
  } catch {
    // storage unavailable: the session simply won't survive a reload
  }
}

/** Simulated login: the user id is only remembered in the browser, the backend trusts whatever it is sent. */
export function UserProvider({ children }: { children: ReactNode }) {
  const [userId, setUserIdState] = useState<string | null>(null);
  const [isReady, setIsReady] = useState(false);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect -- hydrate from localStorage after mount to avoid SSR mismatch
    setUserIdState(readStored());
    setIsReady(true);
  }, []);

  const setUserId = useCallback((id: string) => {
    writeStored(id);
    setUserIdState(id);
  }, []);

  const logout = useCallback(() => {
    writeStored(null);
    setUserIdState(null);
  }, []);

  const value = useMemo(() => ({ userId, isReady, setUserId, logout }), [userId, isReady, setUserId, logout]);
  return <UserContext.Provider value={value}>{children}</UserContext.Provider>;
}

export function useUser(): UserContextValue {
  const ctx = useContext(UserContext);
  if (!ctx) throw new Error("useUser must be used within a UserProvider");
  return ctx;
}
