using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using AegisMint.Core.Services;

namespace AegisMint.Core.Security
{
    /// <summary>
    /// Certificate utilities for desktop authentication
    /// </summary>
    public static class CertificateManager
    {
        /// <summary>
        /// Generate RSA key pair and Certificate Signing Request (CSR)
        /// </summary>
        /// <param name="desktopAppId">Desktop application ID</param>
        /// <param name="machineName">Machine name</param>
        /// <param name="osUser">OS user</param>
        /// <returns>Tuple of (CSR in PEM format, Private key in PEM format)</returns>
        public static (string csrPem, string privateKeyPem) GenerateCertificateRequest(
            string desktopAppId, 
            string machineName, 
            string osUser)
        {
            // Generate 2048-bit RSA key pair
            using (var rsa = RSA.Create(2048))
            {
                // Create certificate request
                var request = new CertificateRequest(
                    new X500DistinguishedName($"CN={desktopAppId}, O=AegisMint Desktop, OU={machineName}, L={osUser}"),
                    rsa,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1
                );

                // Add Subject Alternative Name
                var sanBuilder = new SubjectAlternativeNameBuilder();
                sanBuilder.AddDnsName(machineName);
                request.CertificateExtensions.Add(sanBuilder.Build());

                // Add Extended Key Usage for client authentication
                request.CertificateExtensions.Add(
                    new X509EnhancedKeyUsageExtension(
                        new OidCollection { new Oid("1.3.6.1.5.5.7.3.2") }, // Client Authentication
                        critical: false
                    )
                );

                // Export private key to PEM
                var privateKeyPem = ExportPrivateKeyToPem(rsa);

                // Create CSR
                var csr = request.CreateSigningRequest();
                var csrPem = ExportCsrToPem(csr);

                return (csrPem, privateKeyPem);
            }
        }

        /// <summary>
        /// Store certificate and private key in vault
        /// </summary>
        public static void StoreCertificate(
            VaultManager vaultManager,
            string certificatePem,
            string privateKeyPem)
        {
            vaultManager.SaveCertificate(certificatePem);
            vaultManager.SavePrivateKey(privateKeyPem);
        }

        /// <summary>
        /// Load certificate and private key from vault
        /// </summary>
        public static (string certificatePem, string privateKeyPem)? LoadCertificate(
            VaultManager vaultManager)
        {
            var cert = vaultManager.GetCertificate();
            var key = vaultManager.GetPrivateKey();

            if (string.IsNullOrEmpty(cert) || string.IsNullOrEmpty(key))
            {
                return null;
            }

            return (cert, key);
        }

        /// <summary>
        /// Create X509Certificate2 object from PEM strings
        /// </summary>
        public static X509Certificate2 CreateCertificateFromPem(
            string certificatePem,
            string privateKeyPem)
        {
            var cert = X509Certificate2.CreateFromPem(certificatePem, privateKeyPem);
            return cert;
        }

        /// <summary>
        /// Check if certificate is expiring soon (within 60 days)
        /// </summary>
        public static bool IsCertificateExpiringSoon(X509Certificate2 certificate)
        {
            var expiresAt = certificate.NotAfter;
            var daysUntilExpiry = (expiresAt - DateTime.UtcNow).TotalDays;
            return daysUntilExpiry <= 60;
        }

        private static string ExportPrivateKeyToPem(RSA rsa)
        {
            var privateKeyBytes = rsa.ExportRSAPrivateKey();
            var builder = new StringBuilder();
            builder.AppendLine("-----BEGIN RSA PRIVATE KEY-----");
            builder.AppendLine(Convert.ToBase64String(privateKeyBytes, Base64FormattingOptions.InsertLineBreaks));
            builder.AppendLine("-----END RSA PRIVATE KEY-----");
            return builder.ToString();
        }

        private static string ExportCsrToPem(byte[] csr)
        {
            var builder = new StringBuilder();
            builder.AppendLine("-----BEGIN CERTIFICATE REQUEST-----");
            builder.AppendLine(Convert.ToBase64String(csr, Base64FormattingOptions.InsertLineBreaks));
            builder.AppendLine("-----END CERTIFICATE REQUEST-----");
            return builder.ToString();
        }
    }
}
