import { useEffect } from "react";

type ToastProps = {
  message: string;
  type?: "success" | "error" | "info";
  onClose: () => void;
  duration?: number;
};

export default function Toast({ message, type = "success", onClose, duration = 5000 }: ToastProps) {
  useEffect(() => {
    const timer = setTimeout(onClose, duration);
    return () => clearTimeout(timer);
  }, [onClose, duration]);

  const colors = {
    success: { bg: "rgba(76, 175, 80, 0.95)", border: "rgba(76, 175, 80, 1)" },
    error: { bg: "rgba(249, 112, 102, 0.95)", border: "rgba(249, 112, 102, 1)" },
    info: { bg: "rgba(124, 92, 255, 0.95)", border: "rgba(124, 92, 255, 1)" },
  };

  const color = colors[type];

  return (
    <div
      style={{
        position: "fixed",
        top: "20px",
        right: "20px",
        zIndex: 10000,
        background: color.bg,
        border: `1px solid ${color.border}`,
        borderRadius: "12px",
        padding: "1rem 1.5rem",
        minWidth: "250px",
        maxWidth: "400px",
        boxShadow: "0 4px 12px rgba(0, 0, 0, 0.3)",
        display: "flex",
        alignItems: "center",
        gap: "0.75rem",
        animation: "slideIn 0.3s ease-out",
      }}
    >
      <div style={{ flex: 1, fontWeight: 500, fontSize: "14px", color: "#fff" }}>
        {message}
      </div>
      <button
        onClick={onClose}
        style={{
          background: "transparent",
          border: "none",
          color: "#fff",
          cursor: "pointer",
          fontSize: "18px",
          padding: "0",
          lineHeight: 1,
          opacity: 0.8,
        }}
        onMouseEnter={(e) => (e.currentTarget.style.opacity = "1")}
        onMouseLeave={(e) => (e.currentTarget.style.opacity = "0.8")}
      >
        ×
      </button>
      <style>
        {`
          @keyframes slideIn {
            from {
              transform: translateX(100%);
              opacity: 0;
            }
            to {
              transform: translateX(0);
              opacity: 1;
            }
          }
        `}
      </style>
    </div>
  );
}
