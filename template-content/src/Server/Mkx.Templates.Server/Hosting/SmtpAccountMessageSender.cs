using System.Net;
using System.Net.Mail;
using Mkx.Templates.Application.Abstractions;

namespace Mkx.Templates.Server.Hosting;

/// <summary>Opt-in SMTP STARTTLS delivery. Credentials only through configuration/secret manager.</summary>
public sealed class SmtpAccountMessageSender(IConfiguration config) : IAccountMessageSender
{
    public async Task SendAsync(string recipient, string subject, string message, CancellationToken ct)
    {
        var host = config["AccountLifecycle:Smtp:Host"]!;
        var port = config.GetValue<int>("AccountLifecycle:Smtp:Port");
        var username = config["AccountLifecycle:Smtp:Username"]!;
        var password = config["AccountLifecycle:Smtp:Password"]!;
        var from = config["AccountLifecycle:Smtp:From"]!;

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = true, // Require STARTTLS, not insecure plaintext SMTP.
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(username, password),
            Timeout = 10000
        };
        using var mail = new MailMessage(from, recipient, subject, message);
        await client.SendMailAsync(mail, ct);
    }
}
