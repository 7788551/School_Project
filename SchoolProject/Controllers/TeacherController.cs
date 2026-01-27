
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Security.Claims;

namespace SchoolProject.Controllers
{
    public class TeacherController : Controller
    {
        private readonly IConfiguration _configuration;

        // ✅ ONLY ONE CONSTRUCTOR
        public TeacherController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IActionResult TeacherDashboard()
        {
            // Safety check (important)
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim))
            {
                return RedirectToAction("Login", "Account");
            }

            int userId = int.Parse(userIdClaim);

            string cs = _configuration.GetConnectionString("DefaultConnection");

            using SqlConnection con = new SqlConnection(cs);
            using SqlCommand cmd = new SqlCommand(@"
                SELECT TeacherImage
                FROM Teachers
                WHERE UserId = @UserId AND IsActive = 1
            ", con);

            cmd.Parameters.AddWithValue("@UserId", userId);

            con.Open();
            var image = cmd.ExecuteScalar();

            // Pass image to view / layout
            ViewBag.TeacherImage = image == DBNull.Value ? null : image?.ToString();

            return View();
        }
    }
}
