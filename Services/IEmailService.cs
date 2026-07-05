using System.Threading.Tasks;

namespace ProyectoKpote.Services
{
    public interface IEmailService
    {
        Task EnviarCorreoBloqueoAsync(string destinatario, string nombreUsuario, int minutosBloqueo);
    }
}