using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SchoolProject.Models;
using SchoolProject.Models.ViewModels;

namespace SchoolProject.Controllers
{
    [AllowAnonymous]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration _configuration;

        // ✅ UPDATED CONSTRUCTOR (logger + configuration)
        public HomeController(
            ILogger<HomeController> logger,
            IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        // ✅ PRIVATE DB CONNECTION METHOD (FIXES ERROR)
        private SqlConnection GetConnection()
        {
            return new SqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));
        }

        // ---------------- INDEX ----------------
        [AllowAnonymous]
        public IActionResult Index()
        {
            return View();
        }

        // ---------------- CONTACT ----------------
        [AllowAnonymous]
        public IActionResult Contact()
        {
            return View(new Enquiry());
        }

        // ---------------- ABOUT ----------------
        [AllowAnonymous]
        public IActionResult About()
        {
            var model = new AboutViewModel
            {
                FacultyList = new List<Faculty>(),
                MentorList = new List<Mentor>()
            };

            using SqlConnection con = GetConnection();
            con.Open();

            // -------- FACULTY --------
            using (SqlCommand cmd = new(
                "SELECT Name, Designation, ImagePath FROM Faculty ORDER BY CreatedDate DESC", con))
            {
                using SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    model.FacultyList.Add(new Faculty
                    {
                        Name = reader["Name"].ToString(),
                        Designation = reader["Designation"].ToString(),
                        ImagePath = reader["ImagePath"].ToString()
                    });
                }
            }

            // -------- MENTORS --------
            using (SqlCommand cmd = new(
                "SELECT Name, Designation, ImagePath FROM Mentors ORDER BY DisplayOrder ASC", con))
            {
                using SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    model.MentorList.Add(new Mentor
                    {
                        Name = reader["Name"].ToString(),
                        Designation = reader["Designation"].ToString(),
                        ImagePath = reader["ImagePath"].ToString()
                    });
                }
            }

            return View(model); // ✅ SINGLE VIEWMODEL
        }


        // ---------------- GALLERY (FIXED) ----------------
        [AllowAnonymous]
        public IActionResult Gallery()
        {
            List<GalleryImage> list = new();

            using SqlConnection con = GetConnection();
            SqlCommand cmd = new(
                "SELECT * FROM GalleryImages ORDER BY CreatedDate DESC", con);

            con.Open();
            SqlDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                list.Add(new GalleryImage
                {
                    GalleryImageId = Convert.ToInt32(reader["GalleryImageId"]),
                    Title = reader["Title"].ToString(),
                    ImagePath = reader["ImagePath"].ToString()
                });
            }

            // ✅ MODEL IS PASSED CORRECTLY
            return View(list);
        }

        // ---------------- ERROR ----------------
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}
