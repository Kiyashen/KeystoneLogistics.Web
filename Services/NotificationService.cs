using System;
using System.Net;
using System.Net.Mail;

namespace KeystoneLogistics.Services
{
    public class NotificationService
    {
        private const string SmtpUser = "keyram.smma.18@gmail.com";
        private const string SmtpPass = "mkkpkkmxdleikmjb";

        public static void SendNotificationEmail(string recipientEmail, string subject, string messageBody)
        {
            SendNotificationEmail(recipientEmail, subject, messageBody, null);
        }

        public static void SendNotificationEmail(string recipientEmail, string subject, string messageBody, Attachment attachment)
        {
            try
            {
                var client = new SmtpClient("smtp.gmail.com", 587)
                {
                    Credentials = new NetworkCredential(SmtpUser, SmtpPass),
                    EnableSsl = true
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(SmtpUser, "Keystone Logistics Dispatch"),
                    Subject = subject,
                    Body = GetProfessionalTemplate(subject, messageBody),
                    IsBodyHtml = true,
                };

                mailMessage.To.Add(recipientEmail);

                if (attachment != null)
                {
                    mailMessage.Attachments.Add(attachment);
                }

                client.Send(mailMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Notification failed: {ex.Message}");
            }
        }

        public static void SendTemporaryPassword(string recipientEmail, string tempPassword)
        {
            string subject = "Password Reset Request";
            string messageBody = $@"
                <p>Hello,</p>
                <p>We received a request to reset your password for your Keystone Logistics account.</p>
                <p>Your temporary password is: <strong style='color: #0f172a; font-size: 16px;'>{tempPassword}</strong></p>
                <p>Please log in using this temporary password and update it immediately in your account settings for security purposes.</p>
                <br/>
                <p>If you did not request a password reset, please ignore this email or contact support.</p>";

            SendNotificationEmail(recipientEmail, subject, messageBody);
        }

        private static string GetProfessionalTemplate(string title, string content)
        {
            return $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #dcdcdc; border-radius: 8px; overflow: hidden;'>
                <div style='background-color: #0f172a; color: #ffffff; padding: 24px; text-align: center;'>
                    <h2 style='margin: 0; font-size: 20px; letter-spacing: 1px;'>KEYSTONE LOGISTICS</h2>
                    <p style='margin: 6px 0 0 0; font-size: 12px; color: #94a3b8;'>Enterprise Freight & Supply Chain Management</p>
                </div>
                <div style='padding: 24px; background-color: #ffffff;'>
                    <h3 style='color: #1e293b; margin-top: 0; border-bottom: 2px solid #f1f5f9; padding-bottom: 8px;'>{title}</h3>
                    <div style='color: #334155; font-size: 14px; line-height: 1.6;'>
                        {content}
                    </div>
                </div>
                <div style='background-color: #f8fafc; color: #64748b; padding: 16px; text-align: center; font-size: 11px; border-top: 1px solid #e2e8f0;'>
                    <p style='margin: 0;'>&copy; {DateTime.Now.Year} Keystone Logistics System. Automated Dispatch & Verification Notice.</p>
                </div>
            </div>";
        }
    }
}