import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "XYZ Inc. Orders",
  description: "Submit orders and review receipts.",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
