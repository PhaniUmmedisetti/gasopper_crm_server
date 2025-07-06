using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;

namespace gasopper_crm_server.Services
{
    public interface IEmailService
    {
        Task<bool> SendOtpEmailAsync(string email, string otpCode, string userName);
        Task<bool> SendWelcomeEmailAsync(string email, string userName);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> SendOtpEmailAsync(string email, string otpCode, string userName)
        {
            try
            {
                _logger.LogInformation($"📧 Starting OTP email send to: {email}");

                // Get configuration
                var smtpHost = _configuration["EmailSettings:SmtpHost"];
                var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
                var smtpUsername = _configuration["EmailSettings:SmtpUsername"];
                var smtpPassword = _configuration["EmailSettings:SmtpPassword"];
                var fromEmail = _configuration["EmailSettings:FromEmail"];
                var fromName = _configuration["EmailSettings:FromName"] ?? "GasopperCRM";

                _logger.LogInformation($"📧 Email Config: Host={smtpHost}, Port={smtpPort}, From={fromEmail}");

                // Validate configuration
                if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(smtpUsername) || 
                    string.IsNullOrEmpty(smtpPassword) || string.IsNullOrEmpty(fromEmail))
                {
                    _logger.LogError("❌ Missing email configuration");
                    return false;
                }

                // Create message
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(fromName, fromEmail));
                message.To.Add(new MailboxAddress(userName, email));
                message.Subject = "Your GasopperCRM Login Code";

                var htmlBody = $@"
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <meta charset='utf-8'>
                        <meta name='viewport' content='width=device-width, initial-scale=1'>
                        <title>Your Login Code</title>
                        <style>
                            body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; margin: 0; padding: 20px; background-color: #f4f4f4; }}
                            .container {{ max-width: 600px; margin: 0 auto; background: white; padding: 30px; border-radius: 10px; box-shadow: 0 0 10px rgba(0,0,0,0.1); }}
                            .header {{ text-align: center; border-bottom: 2px solid #007bff; padding-bottom: 20px; margin-bottom: 30px; }}
                            .logo {{ color: #007bff; font-size: 28px; font-weight: bold; }}
                            .otp-code {{ font-size: 36px; font-weight: bold; color: #007bff; text-align: center; letter-spacing: 5px; margin: 30px 0; padding: 20px; background: #f8f9fa; border-radius: 8px; border: 2px dashed #007bff; }}
                            .content {{ text-align: center; }}
                            .footer {{ margin-top: 30px; text-align: center; color: #666; font-size: 14px; border-top: 1px solid #eee; padding-top: 20px; }}
                            .warning {{ background: #fff3cd; color: #856404; padding: 15px; border-radius: 5px; margin: 20px 0; border-left: 4px solid #ffc107; }}
                        </style>
                    </head>
                    <body>
                        <div class='container'>
                            <div class='header'>
                                <div class='logo'> GasopperCRM</div>
                            </div>
                            <div class='content'>
                                <h2>Hello {userName},</h2>
                                <p>Here is your secure login code:</p>
                                <div class='otp-code'>{otpCode}</div>
                                <p><strong>This code will expire in 5 minutes.</strong></p>
                                <div class='warning'>
                                    <strong>Security Notice:</strong><br>
                                    If you didn't request this code, please ignore this email and contact support if you have concerns.
                                </div>
                            </div>
                            <div class='footer'>
                                <p>Best regards,<br>GasopperCRM Team</p>
                                <p><small>This is an automated message. Please do not reply to this email.</small></p>
                            </div>
                        </div>
                    </body>
                    </html>";

                var textBody = $@"
GasopperCRM - Login Code

Hello {userName},

Your secure login code is: {otpCode}

This code will expire in 5 minutes.

If you didn't request this code, please ignore this email.

Best regards,
GasopperCRM Team

This is an automated message. Please do not reply to this email.";

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = htmlBody,
                    TextBody = textBody
                };
                message.Body = bodyBuilder.ToMessageBody();

                _logger.LogInformation($"📧 Message created. Subject: {message.Subject}");

                // Send email with detailed logging
                using var client = new SmtpClient();
                
                // Enable detailed SMTP logging
                client.ServerCertificateValidationCallback = (s, c, h, e) => true;
                
                _logger.LogInformation($"📧 Connecting to SMTP server: {smtpHost}:{smtpPort}");
                
                await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.StartTls);
                _logger.LogInformation($"✅ Connected to SMTP server successfully");
                
                _logger.LogInformation($"📧 Authenticating with username: {smtpUsername}");
                await client.AuthenticateAsync(smtpUsername, smtpPassword);
                _logger.LogInformation($"✅ SMTP authentication successful");
                
                _logger.LogInformation($"📧 Sending email to: {email}");
                await client.SendAsync(message);
                _logger.LogInformation($"✅ Email sent successfully");
                
                await client.DisconnectAsync(true);
                _logger.LogInformation($"📧 Disconnected from SMTP server");

                _logger.LogInformation($"✅ OTP email sent successfully to {email}");
                return true;
            }
            catch (AuthenticationException authEx)
            {
                _logger.LogError(authEx, $"❌ SMTP Authentication failed for {email}. Check username/password.");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Failed to send OTP email to {email}. Error: {ex.Message}");
                return false;
            }
        }

       // REPLACE the SendWelcomeEmailAsync method in your EmailService.cs with this:

public async Task<bool> SendWelcomeEmailAsync(string email, string userName)
{
    try
    {
        // For now, just return true - implement this later when needed
        await Task.CompletedTask;
        _logger.LogInformation($"Welcome email functionality not yet implemented for {email}");
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, $"Failed to send welcome email to {email}");
        return false;
    }
}
    }
}