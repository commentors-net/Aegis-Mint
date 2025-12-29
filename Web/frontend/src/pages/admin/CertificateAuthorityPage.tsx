import CAStatusSection from "../../components/ca/CAStatusSection";
import PendingCertificatesSection from "../../components/ca/PendingCertificatesSection";

export default function CertificateAuthorityPage() {
  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold mb-2">Certificate Authority Management</h1>
        <p className="text-gray-600">
          Manage certificate-based authentication for desktop applications
        </p>
      </div>

      <CAStatusSection />
      <PendingCertificatesSection />
    </div>
  );
}
