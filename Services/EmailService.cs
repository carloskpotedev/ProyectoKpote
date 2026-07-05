using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ProyectoKpote.Services
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

        public async Task EnviarCorreoBloqueoAsync(string destinatario, string nombreUsuario, int minutosBloqueo)
        {
            if (string.IsNullOrWhiteSpace(destinatario))
            {
                _logger.LogWarning("No se pudo enviar correo de bloqueo: el usuario {Usuario} no tiene un correo registrado.", nombreUsuario);
                return;
            }

            try
            {
                var smtpHost = _configuration["Smtp:Host"];
                var smtpPort = int.Parse(_configuration["Smtp:Port"] ?? "587");
                var smtpUser = _configuration["Smtp:User"];
                var smtpPassword = _configuration["Smtp:Password"];
                var remitente = _configuration["Smtp:From"] ?? smtpUser;
                var usarSsl = bool.Parse(_configuration["Smtp:EnableSsl"] ?? "true");

                using var mensaje = new MailMessage
                {
                    From = new MailAddress(remitente, "ProyectoKpote - Seguridad"),
                    Subject = "Su cuenta ha sido bloqueada temporalmente",
                    Body = $@"Hola {nombreUsuario},

Su cuenta ha sido bloqueada debido a múltiples intentos fallidos de inicio de sesión.
Podrá intentar nuevamente en {minutosBloqueo} minuto(s).

Si usted no realizó estos intentos, le recomendamos cambiar su contraseña tan pronto como se restablezca el acceso.

Este es un mensaje automático, por favor no responda a este correo.",
                    IsBodyHtml = false
                };
                mensaje.To.Add(destinatario);

                using var cliente = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUser, smtpPassword),
                    EnableSsl = usarSsl
                };

                await cliente.SendMailAsync(mensaje);
                _logger.LogInformation("Correo de bloqueo enviado a {Destinatario} para el usuario {Usuario}.", destinatario, nombreUsuario);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar correo de bloqueo al usuario {Usuario}.", nombreUsuario);
            }
        }
    }
}