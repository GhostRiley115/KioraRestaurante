using Microsoft.AspNetCore.Mvc;

namespace KioraRestaurante.Controllers
{
    public class ProdutoController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
