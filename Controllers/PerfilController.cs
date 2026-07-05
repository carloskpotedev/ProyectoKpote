using Microsoft.AspNetCore.Mvc;
using ProyectoKpote.Data;
using ProyectoKpote.Models;
using System.Security.Cryptography;
using System.Text;

namespace ProyectoKpote.Controllers
{
    public class PerfilController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<PerfilController> _logger;

        public PerfilController(AppDbContext context, ILogger<PerfilController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("UsuarioLogueado") == null)
            {
                return RedirectToAction("Index", "Login");
            }

            int usuarioId = HttpContext.Session.GetInt32("UsuarioID") ?? 0;

            Usuario? usuario;

            try
            {
                usuario = _context.Usuarios.FirstOrDefault(u => u.UsuarioID == usuarioId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar el perfil del usuario {UsuarioId} en la base de datos.", usuarioId);
                TempData["PasswordError"] = "Ocurrió un error al cargar su perfil. Intente nuevamente.";
                return RedirectToAction("Index", "Home");
            }

            if (usuario == null)
            {
                return RedirectToAction("Index", "Login");
            }

            return View(usuario);
        }

        [HttpPost]
        public IActionResult ChangePassword(string passwordNueva, string passwordConfirmar)
        {
            if (HttpContext.Session.GetString("UsuarioLogueado") == null)
            {
                return RedirectToAction("Index", "Login");
            }

            int usuarioId = HttpContext.Session.GetInt32("UsuarioID") ?? 0;

            Usuario? usuario;

            try
            {
                usuario = _context.Usuarios.FirstOrDefault(u => u.UsuarioID == usuarioId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar el usuario {UsuarioId} para el cambio de contraseña.", usuarioId);
                TempData["PasswordError"] = "Ocurrió un error al procesar su solicitud. Intente nuevamente.";
                return RedirectToAction("Index");
            }

            if (usuario == null)
            {
                return RedirectToAction("Index", "Login");
            }

            if (string.IsNullOrWhiteSpace(passwordNueva) || string.IsNullOrWhiteSpace(passwordConfirmar))
            {
                TempData["PasswordError"] = "Todos los campos son obligatorios.";
                return RedirectToAction("Index");
            }

            if (passwordNueva != passwordConfirmar)
            {
                TempData["PasswordError"] = "La nueva contraseña y su confirmación no coinciden.";
                return RedirectToAction("Index");
            }

            if (passwordNueva.Length < 6)
            {
                TempData["PasswordError"] = "La nueva contraseña debe tener al menos 6 caracteres.";
                return RedirectToAction("Index");
            }

            try
            {
                usuario.PasswordHash = CalcularHashSHA512(passwordNueva);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al calcular el hash de la nueva contraseña para el usuario {UsuarioId}.", usuarioId);
                TempData["PasswordError"] = "Ocurrió un error al procesar su solicitud. Intente nuevamente.";
                return RedirectToAction("Index");
            }

            try
            {
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar la nueva contraseña del usuario {UsuarioId} en la base de datos.", usuarioId);
                TempData["PasswordError"] = "Ocurrió un error al guardar la nueva contraseña. Intente nuevamente.";
                return RedirectToAction("Index");
            }

            _logger.LogInformation("Contraseña actualizada correctamente para el usuario {UsuarioId}.", usuarioId);

            TempData["PasswordExito"] = "Contraseña actualizada correctamente.";
            return RedirectToAction("Index");
        }

        private byte[] CalcularHashSHA512(string texto)
        {
            using (var sha512 = SHA512.Create())
            {
                return sha512.ComputeHash(Encoding.UTF8.GetBytes(texto));
            }
        }
    }
}