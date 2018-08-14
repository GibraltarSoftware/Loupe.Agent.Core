using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using AspNetCore2.Sandbox.Models;

namespace AspNetCore2.Sandbox.Controllers
{
    public class HomeController : Controller
    {
        [HttpGet(Name = nameof(Index))]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet("about", Name = nameof(About))]
        public IActionResult About()
        {
            ViewData["Message"] = "Your application description page.";

            return View();
        }

        [HttpGet("contact", Name = nameof(Contact))]
        public async Task<IActionResult> Contact()
        {
            await Task.Delay(100);
            
            ViewData["Message"] = "Your contact page.";

            return View();
        }

        [HttpGet("throw", Name = nameof(Throw))]
        public IActionResult Throw()
        {
            throw new NotImplementedException();
        }

        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
