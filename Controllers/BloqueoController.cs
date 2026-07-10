using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ProyectoKpote.Models;

namespace ProyectoKpote.Controllers;

public class BloqueoController : Controller
{
        public IActionResult Index()
    {

        if (TempData["UsuarioBloqueado"] == null)
        {
            return RedirectToAction("Index", "Login");
        }

        ViewBag.MensajeBloqueo = TempData["MensajeBloqueo"];

        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}