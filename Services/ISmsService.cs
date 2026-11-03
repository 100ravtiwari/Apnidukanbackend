namespace Dukan.API.Services;

public interface ISmsService
{
    Task<bool> SendOtpSmsAsync(string mobileNumber, string otp, string purpose = "login");
}

