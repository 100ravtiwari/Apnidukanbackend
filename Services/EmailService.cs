using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Dukan.API.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<bool> SendDispatchOtpEmailAsync(
        string toEmail,
        string customerName,
        string orderNumber,
        string otp,
        string deliveryAddress,
        decimal totalAmount)
    {
        var subject = $"Order #{orderNumber} Dispatched! Your Delivery OTP: {otp}";

        var bodyHtml = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Order Dispatched</title>
</head>
<body style='font-family: -apple-system, BlinkMacSystemFont, Segoe UI, Roboto, Helvetica, Arial, sans-serif; background-color: #f8fafc; margin: 0; padding: 24px; color: #1e293b;'>
    <div style='max-width: 580px; margin: 0 auto; background-color: #ffffff; border-radius: 14px; overflow: hidden; box-shadow: 0 4px 12px rgba(0,0,0,0.06); border: 1px solid #e2e8f0;'>
        <div style='background: linear-gradient(135deg, #16a34a, #059669); padding: 28px 24px; text-align: center; color: white;'>
            <div style='font-size: 32px; margin-bottom: 8px;'>🛵</div>
            <h1 style='margin: 0; font-size: 22px; font-weight: 800; letter-spacing: -0.5px;'>Your Order is Out for Delivery!</h1>
            <p style='margin: 6px 0 0; opacity: 0.9; font-size: 14px;'>OM General Store</p>
        </div>

        <div style='padding: 28px 24px;'>
            <p style='font-size: 16px; margin: 0 0 16px;'>Hello <strong>{customerName}</strong>,</p>
            <p style='font-size: 14px; color: #475569; margin: 0 0 20px; line-height: 1.5;'>
                Great news! Your order <strong>#{orderNumber}</strong> has been packed and handed over to our delivery partner. It will reach your doorstep shortly.
            </p>

            <!-- Secure OTP Card -->
            <div style='background: #eff6ff; border: 2px dashed #3b82f6; border-radius: 12px; padding: 20px; text-align: center; margin-bottom: 24px;'>
                <div style='font-size: 12px; font-weight: 800; color: #1e40af; text-transform: uppercase; letter-spacing: 1px; margin-bottom: 6px;'>
                    Your Secure Delivery Verification Code (OTP)
                </div>
                <div style='font-size: 38px; font-weight: 900; color: #1d4ed8; letter-spacing: 8px; font-family: monospace;'>
                    {otp}
                </div>
                <p style='font-size: 12px; color: #64748b; margin: 8px 0 0;'>
                    ⚠️ Please share this OTP with the delivery agent <strong>only after</strong> receiving and verifying your items.
                </p>
            </div>

            <!-- Order Summary Snapshot -->
            <div style='background: #f8fafc; border-radius: 10px; padding: 16px; margin-bottom: 20px; font-size: 13px;'>
                <div style='display: flex; justify-content: space-between; margin-bottom: 8px;'>
                    <span style='color: #64748b;'>Order Number:</span>
                    <strong style='font-family: monospace;'>{orderNumber}</strong>
                </div>
                <div style='display: flex; justify-content: space-between; margin-bottom: 8px;'>
                    <span style='color: #64748b;'>Total Payable Amount:</span>
                    <strong style='color: #0f172a; font-size: 15px;'>₹{totalAmount:F0}</strong>
                </div>
                <div style='display: flex; justify-content: space-between;'>
                    <span style='color: #64748b;'>Delivery Address:</span>
                    <span style='text-align: right; max-width: 60%; color: #334155;'>{deliveryAddress}</span>
                </div>
            </div>

            <p style='font-size: 13px; color: #64748b; margin: 0; line-height: 1.5;'>
                If you did not place this order or need instant help, please contact our store support.
            </p>
        </div>

        <div style='background: #f1f5f9; padding: 16px 24px; text-align: center; font-size: 12px; color: #94a3b8;'>
            OM General Store • Fast & Reliable Delivery • Thank you for shopping with us!
        </div>
    </div>
</body>
</html>";

        return await SendEmailAsync(toEmail, subject, bodyHtml);
    }

    public async Task<bool> SendOrderConfirmationEmailAsync(
        string toEmail,
        string customerName,
        string orderNumber,
        decimal totalAmount)
    {
        var subject = $"Order Confirmed! #{orderNumber} - OM General Store";
        var bodyHtml = $@"
<!DOCTYPE html>
<html>
<body style='font-family: sans-serif; padding: 20px; color: #333;'>
    <h2>Thank you for your order, {customerName}!</h2>
    <p>Your order <strong>#{orderNumber}</strong> of <strong>₹{totalAmount:F0}</strong> has been received.</p>
    <p>We will notify you with your delivery OTP as soon as your order is dispatched.</p>
</body>
</html>";
        return await SendEmailAsync(toEmail, subject, bodyHtml);
    }

    private async Task<bool> SendEmailAsync(string toEmail, string subject, string bodyHtml)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            _logger.LogWarning("Email sending skipped: Recipient email is empty.");
            return false;
        }

        var host = _config["EmailSettings:Host"];
        var portStr = _config["EmailSettings:Port"];
        var senderEmail = _config["EmailSettings:SenderEmail"] ?? "orders@omstore.com";
        var senderPassword = _config["EmailSettings:SenderPassword"];
        var senderName = _config["EmailSettings:SenderName"] ?? "OM General Store";
        var enableSsl = bool.TryParse(_config["EmailSettings:EnableSsl"], out var ssl) ? ssl : true;

        // If SMTP server is configured, attempt sending real email via SMTP
        if (!string.IsNullOrWhiteSpace(host) && int.TryParse(portStr, out var port))
        {
            try
            {
                using var client = new SmtpClient(host, port)
                {
                    EnableSsl = enableSsl,
                    Timeout = 10000
                };

                if (!string.IsNullOrWhiteSpace(senderPassword))
                {
                    client.Credentials = new NetworkCredential(senderEmail, senderPassword);
                }

                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(senderEmail, senderName),
                    Subject = subject,
                    Body = bodyHtml,
                    IsBodyHtml = true
                };
                mailMessage.To.Add(toEmail);

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation("Email successfully sent to {ToEmail} with subject: {Subject}", toEmail, subject);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email via SMTP to {ToEmail}. Fallback to simulated delivery.", toEmail);
            }
        }

        // Development / Fallback mode: Log email details cleanly
        _logger.LogInformation("==================================================================");
        _logger.LogInformation("📧 [EMAIL DISPATCHED SIMULATION]");
        _logger.LogInformation("To: {ToEmail}", toEmail);
        _logger.LogInformation("Subject: {Subject}", subject);
        _logger.LogInformation("==================================================================");

        return true;
    }
}
