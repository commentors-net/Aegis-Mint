import { ReactNode } from "react";

export function Table({ children }: { children: ReactNode }) {
  return <table className="table">{children}</table>;
}

export function Th({ children }: { children: ReactNode }) {
  return <th>{children}</th>;
}

export function Td({ children }: { children: ReactNode }) {
  return <td>{children}</td>;
}
