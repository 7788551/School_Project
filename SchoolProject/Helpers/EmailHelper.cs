using Microsoft.Extensions.Configuration;
using System.IO;
using System.Net;
using System.Net.Mail;

namespace SchoolProject.Helpers
{
    public static class EmailHelper
    {
        public static void SendOtpEmail(
            IConfiguration configuration,
            string toEmail,
            string otp)
        {
            string email = configuration["EmailSettings:Email"];
            string password = configuration["EmailSettings:AppPassword"];
            string host = configuration["EmailSettings:Host"];
            int port = int.Parse(configuration["EmailSettings:Port"]);

            // 📄 Read HTML template
            string templatePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Templates",
                "Emails",
                "OtpEmailTemplate.cshtml");

            string htmlBody = File.ReadAllText(templatePath);

            htmlBody = htmlBody.Replace("{{OTP}}", otp);
            htmlBody = htmlBody.Replace("{{YEAR}}", DateTime.Now.Year.ToString());


            var smtp = new SmtpClient
            {
                Host = host,
                Port = port,
                EnableSsl = true,
                Credentials = new NetworkCredential(email, password)
            };

            var mail = new MailMessage
            {
                From = new MailAddress(email, "School ERP"),
                Subject = "Password Reset OTP",
                Body = htmlBody,
                IsBodyHtml = true
            };

            mail.To.Add(toEmail);

            smtp.Send(mail);
        }
    }
}
