using System.Net.Http.Headers;
using System.Text.Json;

namespace Dukan.API.Services;

public class SmsService : ISmsService
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmsService> _logger;
    private readonly HttpClient _httpClient;

    public SmsService(IConfiguration config, ILogger<SmsService> logger)
    {
        _config = config;
        _logger = logger;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    }

    public async Task<bool> SendOtpSmsAsync(string mobileNumber, string otp, string purpose = "login")
    {
        var enabled = _config.GetValue<bool>("SmsSettings:Enabled", false);
        var provider = _config.GetValue<string>("SmsSettings:Provider") ?? "Fast2SMS";
        var apiKey = _config.GetValue<string>("SmsSettings:ApiKey") ?? string.Empty;

        // Clean mobile number (strip +91 and keep 10 digits)
        var digitsOnly = new string(mobileNumber.Where(char.IsDigit).ToArray());
        var phone10 = digitsOnly.Length >= 10 ? digitsOnly[^10..] : digitsOnly;

        _logger.LogInformation("[SMS Service] Outgoing OTP: {Otp} to +91 {Phone} (Purpose: {Purpose}, Live Provider Enabled: {Enabled})", 
            otp, phone10, purpose, enabled);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n========================================");
        Console.WriteLine($"📱 [SMS DISPATCH] To: +91 {phone10}");
        Console.WriteLine($"🔑 OTP CODE: {otp}");
        Console.WriteLine($"💬 Message: Your OM General Store verification code is {otp}. Valid for 10 minutes.");
        Console.WriteLine($"========================================\n");
        Console.ResetColor();

        if (!enabled || string.IsNullOrWhiteSpace(apiKey))
        {
            // Dev simulation mode - success without external network call
            return true;
        }

        try
        {
            if (provider.Equals("Fast2SMS", StringComparison.OrdinalIgnoreCase))
            {
                // Fast2SMS Quick OTP Gateway Integration (Popular Indian SMS Gateway)
                var request = new HttpRequestMessage(HttpMethod.Post, "https://www.fast2sms.com/dev/bulkV2");
                request.Headers.Add("authorization", apiKey);
                
                var payload = new
                {
                    variables_values = otp,
                    route = "otp",
                    numbers = phone10
                };
                request.Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");

                var res = await _httpClient.SendAsync(request);
                var content = await res.Content.ReadAsStringAsync();
                _logger.LogInformation("[Fast2SMS] Response: {Content}", content);
                return res.IsSuccessStatusCode;
            }
            
            // Add custom HTTP providers easily here
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SMS Service] Error sending SMS via {Provider} to {Phone}", provider, phone10);
            return false;
        }
    }
}

