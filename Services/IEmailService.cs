namespace Dukan.API.Services;

public interface IEmailService
{
    Task<bool> SendDispatchOtpEmailAsync(
        string toEmail,
        string customerName,
        string orderNumber,
        string otp,
        string deliveryAddress,
        decimal totalAmount);

    Task<bool> SendOrderConfirmationEmailAsync(
        string toEmail,
        string customerName,
        string orderNumber,
        decimal totalAmount);
}
