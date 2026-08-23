using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using wenu.Models;

namespace wenu.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendContactEmailAsync(ContactRequestDTO request)
        {
            var settings = _configuration.GetSection("EmailSettings");

            var host        = settings["Host"]        ?? throw new InvalidOperationException("EmailSettings:Host is missing");
            var port        = int.Parse(settings["Port"] ?? "465");
            var senderName  = settings["SenderName"]  ?? "App";
            var senderEmail = settings["SenderEmail"] ?? throw new InvalidOperationException("EmailSettings:SenderEmail is missing");
            var appPassword = settings["AppPassword"] ?? throw new InvalidOperationException("EmailSettings:AppPassword is missing");

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(senderName, senderEmail));
            // Deliver to the inbox owner (you), not back to the submitter
            message.To.Add(new MailboxAddress(senderName, senderEmail));
            // Keep the submitter's address as Reply-To so you can reply directly to them
            message.ReplyTo.Add(new MailboxAddress(request.Name, request.Email));
            message.Subject = $"[{request.InquiryType}] New Inquiry from {request.Name}";

            var body = new BodyBuilder
            {
                HtmlBody = BuildHtmlBody(request),
                TextBody = BuildPlainTextBody(request)
            };

            message.Body = body.ToMessageBody();

            using var client = new SmtpClient();

            // Accept the certificate when the only error is that revocation status
            // could not be checked (CRL servers unreachable). All other TLS errors
            // are still rejected.
            client.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
            {
                if (sslPolicyErrors == SslPolicyErrors.None)
                    return true;

                // Allow only if the sole chain error is RevocationStatusUnknown
                if (sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors && chain is not null)
                {
                    var onlyRevocationIssues = chain.ChainStatus.All(s =>
                        s.Status == X509ChainStatusFlags.RevocationStatusUnknown ||
                        s.Status == X509ChainStatusFlags.OfflineRevocation);

                    if (onlyRevocationIssues)
                    {
                        _logger.LogWarning("SMTP certificate revocation check skipped (CRL unreachable). Certificate: {Subject}", certificate?.Subject);
                        return true;
                    }
                }

                _logger.LogError("SMTP certificate validation failed: {Errors}", sslPolicyErrors);
                return false;
            };

            try
            {
                // Port 465 with SslOnConnect (implicit SSL) — works on Render and most cloud hosts.
                // Port 587 with STARTTLS is blocked on Render's free tier and hangs indefinitely.
                await client.ConnectAsync(host, port, SecureSocketOptions.SslOnConnect);
                await client.AuthenticateAsync(senderEmail, appPassword);
                await client.SendAsync(message);
                _logger.LogInformation("Email sent to {Email} for inquiry type '{InquiryType}'", request.Email, request.InquiryType);
            }
            finally
            {
                await client.DisconnectAsync(true);
            }
        }

        private static string BuildHtmlBody(ContactRequestDTO r)
        {
            var now = DateTime.UtcNow.ToString("dddd, MMMM dd yyyy  HH:mm UTC");
            var badgeColor = r.InquiryType.ToLower() switch
            {
                "support"     => "#e74c3c",
                "sales"       => "#27ae60",
                "partnership" => "#8e44ad",
                "billing"     => "#e67e22",
                _             => "#2c3e50"
            };

            return $"""
                <!DOCTYPE html>
                <html lang="en">
                <head>
                  <meta charset="UTF-8" />
                  <meta name="viewport" content="width=device-width, initial-scale=1.0"/>
                  <title>Enterprise Inquiry</title>
                </head>
                <body style="margin:0;padding:0;background-color:#f0f2f5;font-family:'Segoe UI',Arial,sans-serif;">

                  <!-- Wrapper -->
                  <table width="100%" cellpadding="0" cellspacing="0" style="background:#f0f2f5;padding:40px 0;">
                    <tr>
                      <td align="center">

                        <!-- Card -->
                        <table width="620" cellpadding="0" cellspacing="0"
                               style="background:#ffffff;border-radius:8px;overflow:hidden;
                                      box-shadow:0 2px 8px rgba(0,0,0,0.10);">

                          <!-- ── Header ── -->
                          <tr>
                            <td style="background:linear-gradient(135deg,#1a1a2e 0%,#16213e 60%,#0f3460 100%);
                                        padding:36px 40px;">
                              <table width="100%" cellpadding="0" cellspacing="0">
                                <tr>
                                  <td>
                                    <p style="margin:0;font-size:11px;letter-spacing:3px;
                                               text-transform:uppercase;color:#a0aec0;">
                                      Enterprise Communication
                                    </p>
                                    <h1 style="margin:8px 0 0;font-size:26px;font-weight:700;color:#ffffff;">
                                      New Client Inquiry
                                    </h1>
                                  </td>
                                  <td align="right" valign="middle">
                                    <!-- Inquiry type badge -->
                                    <span style="display:inline-block;background:{badgeColor};
                                                 color:#fff;font-size:12px;font-weight:700;
                                                 letter-spacing:1.5px;text-transform:uppercase;
                                                 padding:6px 16px;border-radius:20px;">
                                      {r.InquiryType}
                                    </span>
                                  </td>
                                </tr>
                              </table>
                            </td>
                          </tr>

                          <!-- ── Timestamp bar ── -->
                          <tr>
                            <td style="background:#f7f8fa;border-bottom:1px solid #e2e8f0;
                                        padding:10px 40px;">
                              <p style="margin:0;font-size:12px;color:#718096;">
                                &#128337;&nbsp; Received on <strong>{now}</strong>
                              </p>
                            </td>
                          </tr>

                          <!-- ── Section: Contact Details ── -->
                          <tr>
                            <td style="padding:32px 40px 0;">
                              <p style="margin:0 0 16px;font-size:11px;font-weight:700;
                                         letter-spacing:2px;text-transform:uppercase;color:#a0aec0;">
                                Contact Details
                              </p>

                              <table width="100%" cellpadding="0" cellspacing="0"
                                     style="border:1px solid #e2e8f0;border-radius:6px;
                                            overflow:hidden;border-collapse:collapse;">

                                <!-- Row 1 -->
                                <tr style="background:#f7f8fa;">
                                  <td width="38%" style="padding:14px 20px;
                                                          border-right:1px solid #e2e8f0;
                                                          border-bottom:1px solid #e2e8f0;">
                                    <p style="margin:0;font-size:11px;font-weight:600;
                                               letter-spacing:1px;text-transform:uppercase;
                                               color:#718096;">Full Name</p>
                                  </td>
                                  <td style="padding:14px 20px;border-bottom:1px solid #e2e8f0;">
                                    <p style="margin:0;font-size:14px;font-weight:600;color:#1a202c;">
                                      {r.Name}
                                    </p>
                                  </td>
                                </tr>

                                <!-- Row 2 -->
                                <tr>
                                  <td style="padding:14px 20px;
                                              border-right:1px solid #e2e8f0;
                                              border-bottom:1px solid #e2e8f0;background:#f7f8fa;">
                                    <p style="margin:0;font-size:11px;font-weight:600;
                                               letter-spacing:1px;text-transform:uppercase;
                                               color:#718096;">Email Address</p>
                                  </td>
                                  <td style="padding:14px 20px;border-bottom:1px solid #e2e8f0;">
                                    <a href="mailto:{r.Email}"
                                       style="font-size:14px;color:#0f3460;text-decoration:none;
                                              font-weight:500;">
                                      {r.Email}
                                    </a>
                                  </td>
                                </tr>

                                <!-- Row 3 -->
                                <tr style="background:#f7f8fa;">
                                  <td style="padding:14px 20px;
                                              border-right:1px solid #e2e8f0;
                                              border-bottom:1px solid #e2e8f0;">
                                    <p style="margin:0;font-size:11px;font-weight:600;
                                               letter-spacing:1px;text-transform:uppercase;
                                               color:#718096;">Phone Number</p>
                                  </td>
                                  <td style="padding:14px 20px;border-bottom:1px solid #e2e8f0;">
                                    <p style="margin:0;font-size:14px;color:#1a202c;">
                                      {r.Phone}
                                    </p>
                                  </td>
                                </tr>

                                <!-- Row 4 -->
                                <tr>
                                  <td style="padding:14px 20px;
                                              border-right:1px solid #e2e8f0;
                                              border-bottom:1px solid #e2e8f0;background:#f7f8fa;">
                                    <p style="margin:0;font-size:11px;font-weight:600;
                                               letter-spacing:1px;text-transform:uppercase;
                                               color:#718096;">WhatsApp</p>
                                  </td>
                                  <td style="padding:14px 20px;border-bottom:1px solid #e2e8f0;">
                                    <p style="margin:0;font-size:14px;color:#1a202c;">
                                      {r.Whatsapp}
                                    </p>
                                  </td>
                                </tr>

                                <!-- Row 5 -->
                                <tr style="background:#f7f8fa;">
                                  <td style="padding:14px 20px;border-right:1px solid #e2e8f0;">
                                    <p style="margin:0;font-size:11px;font-weight:600;
                                               letter-spacing:1px;text-transform:uppercase;
                                               color:#718096;">Inquiry Type</p>
                                  </td>
                                  <td style="padding:14px 20px;">
                                    <span style="display:inline-block;background:{badgeColor}20;
                                                 color:{badgeColor};font-size:12px;font-weight:700;
                                                 letter-spacing:1px;text-transform:uppercase;
                                                 padding:4px 12px;border-radius:4px;
                                                 border:1px solid {badgeColor}40;">
                                      {r.InquiryType}
                                    </span>
                                  </td>
                                </tr>

                              </table>
                            </td>
                          </tr>

                          <!-- ── Section: Message ── -->
                          <tr>
                            <td style="padding:28px 40px 0;">
                              <p style="margin:0 0 16px;font-size:11px;font-weight:700;
                                         letter-spacing:2px;text-transform:uppercase;color:#a0aec0;">
                                Client Message
                              </p>
                              <table width="100%" cellpadding="0" cellspacing="0"
                                     style="background:#f7f8fa;border:1px solid #e2e8f0;
                                            border-radius:6px;border-left:4px solid #0f3460;">
                                <tr>
                                  <td style="padding:20px 24px;">
                                    <p style="margin:0;font-size:15px;line-height:1.7;
                                               color:#2d3748;white-space:pre-wrap;">
                                      {r.Message}
                                    </p>
                                  </td>
                                </tr>
                              </table>
                            </td>
                          </tr>

                          <!-- ── Action Buttons ── -->
                          <tr>
                            <td style="padding:28px 40px 0;">
                              <table cellpadding="0" cellspacing="0">
                                <tr>
                                  <td style="padding-right:12px;">
                                    <a href="mailto:{r.Email}?subject=Re: {r.InquiryType} Inquiry"
                                       style="display:inline-block;background:#0f3460;color:#ffffff;
                                              font-size:13px;font-weight:600;text-decoration:none;
                                              padding:12px 24px;border-radius:5px;">
                                      &#9993;&nbsp; Reply to Client
                                    </a>
                                  </td>
                                  <td>
                                    <a href="https://wa.me/{r.Whatsapp}"
                                       style="display:inline-block;background:#25d366;color:#ffffff;
                                              font-size:13px;font-weight:600;text-decoration:none;
                                              padding:12px 24px;border-radius:5px;">
                                      &#128172;&nbsp; WhatsApp
                                    </a>
                                  </td>
                                </tr>
                              </table>
                            </td>
                          </tr>

                          <!-- ── Divider ── -->
                          <tr>
                            <td style="padding:32px 40px 0;">
                              <hr style="border:none;border-top:1px solid #e2e8f0;margin:0;" />
                            </td>
                          </tr>

                          <!-- ── Footer ── -->
                          <tr>
                            <td style="padding:24px 40px 36px;">
                              <table width="100%" cellpadding="0" cellspacing="0">
                                <tr>
                                  <td>
                                    <p style="margin:0;font-size:12px;color:#a0aec0;line-height:1.6;">
                                      This email was generated automatically by your contact system.<br/>
                                      Please do not reply directly to this notification.
                                    </p>
                                  </td>
                                  <td align="right" valign="middle">
                                    <p style="margin:0;font-size:11px;font-weight:700;
                                               letter-spacing:2px;text-transform:uppercase;
                                               color:#cbd5e0;">
                                      Powered by Wemu
                                    </p>
                                  </td>
                                </tr>
                              </table>
                            </td>
                          </tr>

                        </table>
                        <!-- /Card -->

                      </td>
                    </tr>
                  </table>
                  <!-- /Wrapper -->

                </body>
                </html>
                """;
        }

        private static string BuildPlainTextBody(ContactRequestDTO r)
        {
            var now = DateTime.UtcNow.ToString("dddd, MMMM dd yyyy  HH:mm UTC");
            return $"""
                ╔══════════════════════════════════════════╗
                     NEW CLIENT INQUIRY — {r.InquiryType.ToUpper()}
                ╚══════════════════════════════════════════╝

                Received : {now}

                CONTACT DETAILS
                ───────────────────────────────────────────
                Full Name    : {r.Name}
                Email        : {r.Email}
                Phone        : {r.Phone}
                WhatsApp     : {r.Whatsapp}
                Inquiry Type : {r.InquiryType}

                CLIENT MESSAGE
                ───────────────────────────────────────────
                {r.Message}

                ───────────────────────────────────────────
                This message was sent automatically.
                Do not reply directly to this notification.
                Powered by Wemu
                """;
        }
    }
}