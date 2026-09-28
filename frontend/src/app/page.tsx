"use client";

import { useState } from "react";
import { ErrorDisplay } from "@/components/ErrorDisplay";
import { LoginForm } from "@/components/LoginForm";
import { OrderForm } from "@/components/OrderForm";
import { OrderHistory } from "@/components/OrderHistory";
import { ReceiptDisplay } from "@/components/ReceiptDisplay";
import { UserProvider, useUser } from "@/context/UserContext";
import { OrderOutcome, orderApiClient } from "@/services/orderApiClient";

function OrderApp() {
  const { userId, isReady, setUserId, logout } = useUser();
  const [outcome, setOutcome] = useState<OrderOutcome | null>(null);
  const [refreshKey, setRefreshKey] = useState(0);

  if (!isReady) return null;
  if (!userId) return <LoginForm onLogin={setUserId} />;

  function handleOutcome(result: OrderOutcome) {
    setOutcome(result);
    setRefreshKey((k) => k + 1);
  }

  return (
    <>
      <header className="top">
        <span>Signed in as <strong>{userId}</strong></span>
        <button type="button" className="link" onClick={() => { setOutcome(null); logout(); }}>Sign out</button>
      </header>
      <OrderForm client={orderApiClient} userId={userId} onOutcome={handleOutcome} />
      {outcome?.status === "paid" && <ReceiptDisplay receipt={outcome.receipt} />}
      {outcome?.status === "failed" && <ErrorDisplay error={outcome.error} />}
      <OrderHistory client={orderApiClient} userId={userId} refreshKey={refreshKey} onOutcome={handleOutcome} />
    </>
  );
}

export default function Home() {
  return (
    <main>
      <h1>XYZ Inc. Orders</h1>
      <UserProvider>
        <OrderApp />
      </UserProvider>
    </main>
  );
}
