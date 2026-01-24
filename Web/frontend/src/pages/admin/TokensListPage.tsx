import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";

import * as sharesApi from "../../api/shares";
import Badge from "../../components/Badge";
import Button from "../../components/Button";
import { Table, Td, Th } from "../../components/Table";
import Toast from "../../components/Toast";
import { useAuth } from "../../auth/useAuth";

export default function TokensListPage() {
  const { token } = useAuth();
  const navigate = useNavigate();
  const [tokens, setTokens] = useState<sharesApi.TokenDeployment[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [filterUploaded, setFilterUploaded] = useState<"all" | "uploaded" | "pending">("all");
  const [toast, setToast] = useState<{ message: string; type: "success" | "error" } | null>(null);

  const filteredTokens = useMemo(() => {
    let result = tokens;

    // Filter by upload status
    if (filterUploaded === "uploaded") {
      result = result.filter((t) => t.shares_uploaded);
    } else if (filterUploaded === "pending") {
      result = result.filter((t) => !t.shares_uploaded);
    }

    // Filter by search
    if (search) {
      const q = search.toLowerCase();
      result = result.filter(
        (t) =>
          t.token_name.toLowerCase().includes(q) ||
          t.token_symbol.toLowerCase().includes(q) ||
          t.contract_address.toLowerCase().includes(q)
      );
    }

    return result;
  }, [tokens, search, filterUploaded]);

  const refresh = async () => {
    if (!token) return;
    setLoading(true);
    setError(null);
    try {
      const data = await sharesApi.listTokenDeployments(token);
      // Sort by created date descending (newest first)
      setTokens(data.sort((a, b) => new Date(b.created_at_utc).getTime() - new Date(a.created_at_utc).getTime()));
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : "Failed to load tokens";
      setError(errorMsg);
      setToast({ message: errorMsg, type: "error" });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    refresh();
  }, [token]);

  const handleViewShares = (tokenId: string) => {
    navigate(`/admin/tokens/${tokenId}/shares`);
  };

  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleString("en-US", {
      year: "numeric",
      month: "short",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit",
    });
  };

  return (
    <div className="p-6">
      {toast && (
        <Toast
          message={toast.message}
          type={toast.type}
          onClose={() => setToast(null)}
        />
      )}

      <div className="mb-6">
        <h1 className="text-2xl font-bold text-gray-900">Token Deployments</h1>
        <p className="mt-2 text-sm text-gray-600">
          Manage Shamir secret shares for deployed tokens. View shares and assign them to governance authority users.
        </p>
      </div>

      {/* Filters */}
      <div className="mb-4 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex items-center gap-2">
          <input
            type="text"
            placeholder="Search tokens..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
          />
          <select
            value={filterUploaded}
            onChange={(e) => setFilterUploaded(e.target.value as any)}
            className="rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
          >
            <option value="all">All Tokens</option>
            <option value="uploaded">Shares Uploaded</option>
            <option value="pending">Upload Pending</option>
          </select>
        </div>

        <Button onClick={refresh} disabled={loading} variant="secondary" size="sm">
          {loading ? "Refreshing..." : "Refresh"}
        </Button>
      </div>

      {error && (
        <div className="mb-4 rounded-md bg-red-50 p-4 text-sm text-red-700">
          <strong>Error:</strong> {error}
        </div>
      )}

      {/* Tokens Table */}
      <div className="overflow-x-auto rounded-lg border border-gray-200 bg-white shadow">
        <Table>
          <thead className="bg-gray-50">
            <tr>
              <Th>Token Name</Th>
              <Th>Symbol</Th>
              <Th>Network</Th>
              <Th>Contract Address</Th>
              <Th>Shares Status</Th>
              <Th>Created</Th>
              <Th>Actions</Th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-200">
            {loading && (
              <tr>
                <td colSpan={7} className="text-center">
                  Loading tokens...
                </td>
              </tr>
            )}

            {!loading && filteredTokens.length === 0 && (
              <tr>
                <td colSpan={7} className="text-center text-gray-500">
                  {search || filterUploaded !== "all"
                    ? "No tokens match your filters"
                    : "No token deployments found"}
                </td>
              </tr>
            )}

            {!loading &&
              filteredTokens.map((tok) => (
                <tr key={tok.id} className="hover:bg-gray-50">
                  <Td>{tok.token_name}</Td>
                  <Td>{tok.token_symbol}</Td>
                  <Td>
                    <Badge tone="neutral">{tok.network}</Badge>
                  </Td>
                  <Td>
                    <span title={tok.contract_address}>
                      {tok.contract_address.slice(0, 6)}...{tok.contract_address.slice(-4)}
                    </span>
                  </Td>
                  <Td>
                    {tok.shares_uploaded ? (
                      <div className="flex flex-col gap-1">
                        <Badge tone="good">
                          ✓ {tok.shares_uploaded_count} share{tok.shares_uploaded_count !== 1 ? "s" : ""}
                        </Badge>
                        {tok.shares_uploaded_at && (
                          <span className="text-xs text-gray-500">
                            {formatDate(tok.shares_uploaded_at)}
                          </span>
                        )}
                      </div>
                    ) : (
                      <Badge tone="warn">⏳ Pending</Badge>
                    )}
                  </Td>
                  <Td>{formatDate(tok.created_at_utc)}</Td>
                  <Td>
                    <Button
                      onClick={() => handleViewShares(tok.id)}
                      disabled={!tok.shares_uploaded}
                      size="sm"
                      variant={tok.shares_uploaded ? "primary" : "secondary"}
                    >
                      {tok.shares_uploaded ? "Manage Shares" : "No Shares"}
                    </Button>
                  </Td>
                </tr>
              ))}
          </tbody>
        </Table>
      </div>

      {/* Summary Stats */}
      {!loading && tokens.length > 0 && (
        <div className="mt-4 flex gap-4 text-sm text-gray-600">
          <span>
            <strong>{tokens.length}</strong> total token{tokens.length !== 1 ? "s" : ""}
          </span>
          <span>•</span>
          <span>
            <strong>{tokens.filter((t) => t.shares_uploaded).length}</strong> with shares uploaded
          </span>
          <span>•</span>
          <span>
            <strong>{tokens.filter((t) => !t.shares_uploaded).length}</strong> pending upload
          </span>
        </div>
      )}
    </div>
  );
}
