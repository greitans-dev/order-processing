import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Client-components-only app: build to plain static files that the ASP.NET Core host serves from wwwroot.
  output: "export",
};

export default nextConfig;
