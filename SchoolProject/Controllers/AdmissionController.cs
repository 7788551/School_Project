using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SchoolProject.Controllers
{
    public class AdmissionController : Controller
    {
        [AllowAnonymous]   // 👈 This makes it public
        public IActionResult AdmissionBrochure()
        {
            return View();
        }
    }
}