using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ProyectoKpote.Data;
using ProyectoKpote.Models;
using ProyectoKpote.Services;

namespace ProyectoKpote.Controllers
{
    public class LoginController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<LoginController> _logger;

        public LoginController(AppDbContext context, IEmailService emailService, ILogger<LoginController> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("UsuarioLogueado") != null)
            {
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Index(string nombreUsuario, string password)
        {
            if (HttpContext.Session.GetString("UsuarioLogueado") != null)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.NombreUsuarioValor = nombreUsuario;

            bool tieneErrores = false;

            if (string.IsNullOrWhiteSpace(nombreUsuario))
            {
                ViewBag.ErrorUsuario = "Debe ingresar un usuario.";
                tieneErrores = true;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ViewBag.ErrorPassword = "Debe ingresar una contraseña.";
                tieneErrores = true;
            }

            if (tieneErrores)
            {
                return View();
            }

            Usuario? usuario;

            try
            {
                usuario = _context.Usuarios
                    .FirstOrDefault(u => u.NombreUsuario == nombreUsuario);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar el usuario {Usuario} en la base de datos.", nombreUsuario);
                ViewBag.ErrorGeneral = "Ocurrió un error al procesar su solicitud. Intente nuevamente.";
                return View();
            }

            if (usuario == null)
            {
                ViewBag.ErrorUsuario = "El usuario no existe.";
                return View();
            }

            if (usuario.BloqueadoHasta.HasValue && usuario.BloqueadoHasta.Value > DateTime.Now)
            {
                var minutosRestantes = Math.Ceiling((usuario.BloqueadoHasta.Value - DateTime.Now).TotalMinutes);
                TempData["UsuarioBloqueado"] = true;
                TempData["MensajeBloqueo"] = $"Intente nuevamente en {minutosRestantes} minuto(s).";
                return RedirectToAction("Index", "Bloqueo");
            }

            if (usuario.BloqueadoHasta.HasValue && usuario.BloqueadoHasta.Value <= DateTime.Now)
            {
                usuario.IntentosFallidos = 0;
                usuario.BloqueadoHasta = null;
            }

            byte[] hashIngresado;

            try
            {
                hashIngresado = CalcularHashSHA512(password);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al calcular el hash de la contraseña para el usuario {Usuario}.", nombreUsuario);
                ViewBag.ErrorGeneral = "Ocurrió un error al procesar su solicitud. Intente nuevamente.";
                return View();
            }

            if (!hashIngresado.SequenceEqual(usuario.PasswordHash))
            {
                usuario.IntentosFallidos++;

                if (usuario.IntentosFallidos >= 5)
                {
                    usuario.BloqueadoHasta = DateTime.Now.AddMinutes(15);
                    usuario.IntentosFallidos = 0;

                    try
                    {
                        _context.SaveChanges();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al guardar el bloqueo del usuario {Usuario} en la base de datos.", nombreUsuario);
                        ViewBag.ErrorGeneral = "Ocurrió un error al procesar su solicitud. Intente nuevamente.";
                        return View();
                    }

                    try
                    {
                        await _emailService.EnviarCorreoBloqueoAsync(usuario.Email, usuario.NombreUsuario, 15);
                        _logger.LogInformation("Correo de bloqueo enviado a {Destinatario} para el usuario {Usuario}.", usuario.Email, usuario.NombreUsuario);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error al enviar el correo de bloqueo a {Destinatario} para el usuario {Usuario}.", usuario.Email, usuario.NombreUsuario);
                    }

                    TempData["UsuarioBloqueado"] = true;
                    TempData["MensajeBloqueo"] = "Ha superado el número de intentos permitidos. Su usuario ha sido bloqueado por 15 minutos.";
                    return RedirectToAction("Index", "Bloqueo");
                }

                try
                {
                    _context.SaveChanges();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al guardar los intentos fallidos del usuario {Usuario} en la base de datos.", nombreUsuario);
                    ViewBag.ErrorGeneral = "Ocurrió un error al procesar su solicitud. Intente nuevamente.";
                    return View();
                }

                int intentosRestantes = 5 - usuario.IntentosFallidos;
                ViewBag.ErrorPassword = $"La contraseña es incorrecta. Le quedan {intentosRestantes} intento(s).";
                return View();
            }

            usuario.IntentosFallidos = 0;
            usuario.BloqueadoHasta = null;

            try
            {
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al reiniciar el contador de intentos fallidos del usuario {Usuario} en la base de datos.", nombreUsuario);
                ViewBag.ErrorGeneral = "Ocurrió un error al procesar su solicitud. Intente nuevamente.";
                return View();
            }

            try
            {
                HttpContext.Session.SetString("UsuarioLogueado", usuario.NombreUsuario);
                HttpContext.Session.SetInt32("UsuarioID", usuario.UsuarioID);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al establecer la sesión para el usuario {Usuario}.", nombreUsuario);
                ViewBag.ErrorGeneral = "Ocurrió un error al procesar su solicitud. Intente nuevamente.";
                return View();
            }

            _logger.LogInformation("Inicio de sesión exitoso para el usuario {Usuario}.", usuario.NombreUsuario);

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public IActionResult Logout(bool? inactivo)   
        {
            try
            {
                var usuario = HttpContext.Session.GetString("UsuarioLogueado");
                HttpContext.Session.Clear();
                _logger.LogInformation("Cierre de sesión realizado para el usuario {Usuario}.", usuario);

                if (inactivo == true)
                {
                    TempData["MensajeInactividad"] = "Su sesión ha expirado debido a inactividad. Por favor, inicie sesión nuevamente.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cerrar la sesión.");
            }

            return RedirectToAction("Index");
        }

        private byte[] CalcularHashSHA512(string texto)
        {
            using (var sha512 = SHA512.Create())
            {
                return sha512.ComputeHash(Encoding.UTF8.GetBytes(texto));
            }
        }

        [HttpPost]
        public IActionResult ExtenderSesion()
        {
            try
            {
                var usuario = HttpContext.Session.GetString("UsuarioLogueado");

                if (usuario != null)
                {
                    HttpContext.Session.SetString("UsuarioLogueado", usuario);
                    _logger.LogInformation("Sesión extendida para el usuario {Usuario}.", usuario);
                    return Json(new { success = true });
                }

                return Json(new { success = false });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al extender la sesión.");
                return Json(new { success = false });
            }
        }
    }
}