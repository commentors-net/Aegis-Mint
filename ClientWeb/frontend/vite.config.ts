import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";

// https://vitejs.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), "");
  const base = env.VITE_BASE_PATH || "/";

  return {
    base,
    plugins: [react()],
    server: {
      host: "127.0.0.1",
      port: 5174,
      proxy: {
        "/api": {
          target: "http://127.0.0.1:8001",
          changeOrigin: true,
        },
      },
    },
  };
});
