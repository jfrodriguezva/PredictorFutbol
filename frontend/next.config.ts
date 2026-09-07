import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  /* config options here */
  agentRules: false,
  // Self-contained server.js output for the installer/desktop launcher — see
  // installer/build-release.ps1. Local `npm run dev`/`npm run build && npm run start`
  // are unaffected by this.
  output: "standalone",
};

export default nextConfig;
