
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SchoolProject.Models;
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


        [HttpGet]
        [Authorize(Roles = "Teacher")]
        public IActionResult TeacherProfileData()
        {
            int userId = Convert.ToInt32(HttpContext.Session.GetInt32("UserId"));
            int sessionId = Convert.ToInt32(HttpContext.Session.GetInt32("SessionId"));

            TeacherProfile model = new();
            string cs = _configuration.GetConnectionString("DefaultConnection");

            using SqlConnection con = new(cs);
            using SqlCommand cmd = new(@"
        SELECT
            u.UserId,
            u.Name,
            u.Email,
            u.PhoneNumber,

            t.TeacherId,
            t.Gender,
            t.DateOfBirth,
            t.Qualification,
            t.ExperienceYears,
            t.Designation,
            t.Subject,
            t.JoiningDate,
            t.AddressLine1,
            t.AddressLine2,
            t.City,
            t.State,
            t.Pincode,
            t.TeacherImage
        FROM Users u
        LEFT JOIN Teachers t 
            ON t.UserId = u.UserId 
           AND t.SessionId = @SessionId
        WHERE u.UserId = @UserId
    ", con);

            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@SessionId", sessionId);

            con.Open();
            using SqlDataReader dr = cmd.ExecuteReader();

            if (dr.Read())
            {
                model.UserId = userId;
                model.TeacherId = dr["TeacherId"] == DBNull.Value
                    ? 0
                    : Convert.ToInt32(dr["TeacherId"]);

                model.Name = dr["Name"].ToString()!;
                model.Email = dr["Email"]?.ToString();
                model.PhoneNumber = dr["PhoneNumber"]?.ToString();

                model.Gender = dr["Gender"]?.ToString();
                model.DateOfBirth = dr["DateOfBirth"] as DateTime?;
                model.Qualification = dr["Qualification"]?.ToString();
                model.ExperienceYears = dr["ExperienceYears"] as int?;
                model.Designation = dr["Designation"]?.ToString();
                model.Subject = dr["Subject"]?.ToString();
                model.JoiningDate = dr["JoiningDate"] as DateTime?;

                model.AddressLine1 = dr["AddressLine1"]?.ToString();
                model.AddressLine2 = dr["AddressLine2"]?.ToString();
                model.City = dr["City"]?.ToString();
                model.State = dr["State"]?.ToString();
                model.Pincode = dr["Pincode"]?.ToString();

                model.ExistingTeacherImage = dr["TeacherImage"]?.ToString();
            }

            // 🔴 IMAGE SESSION FIX (same pattern as Accountant)
            if (!string.IsNullOrEmpty(model.ExistingTeacherImage))
            {
                HttpContext.Session.SetString("TeacherImage", model.ExistingTeacherImage);
            }
            else
            {
                HttpContext.Session.SetString("TeacherImage", "default.png");
            }

            model.IsEditMode = false;
            return View(model);
        }


        [HttpPost]
        [Authorize(Roles = "Teacher")]
        [ValidateAntiForgeryToken]
        public IActionResult TeacherProfileData(TeacherProfile model)
        {
            int? sessionUserId = HttpContext.Session.GetInt32("UserId");
            if (sessionUserId == null)
                return RedirectToAction("Login", "Account");

            int userId = sessionUserId.Value;
            int sessionId = Convert.ToInt32(HttpContext.Session.GetInt32("SessionId"));

            string fileName = model.ExistingTeacherImage;

            // IMAGE UPLOAD
            if (model.TeacherImage != null && model.TeacherImage.Length > 0)
            {
                fileName = Guid.NewGuid() + Path.GetExtension(model.TeacherImage.FileName);

                string folderPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot/images/Teachers"
                );

                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                string fullPath = Path.Combine(folderPath, fileName);
                using FileStream fs = new(fullPath, FileMode.Create);
                model.TeacherImage.CopyTo(fs);
            }

            using SqlConnection con = new(_configuration.GetConnectionString("DefaultConnection"));
            using SqlCommand cmd = new(@"
        IF EXISTS (
            SELECT 1 FROM Teachers 
            WHERE UserId = @UserId AND SessionId = @SessionId
        )
        BEGIN
            UPDATE Teachers
            SET
                Gender = @Gender,
                DateOfBirth = @DateOfBirth,
                Qualification = @Qualification,
                ExperienceYears = @ExperienceYears,
                Designation = @Designation,
                Subject = @Subject,
                JoiningDate = @JoiningDate,
                AddressLine1 = @AddressLine1,
                AddressLine2 = @AddressLine2,
                City = @City,
                State = @State,
                Pincode = @Pincode,
                TeacherImage = @TeacherImage
            WHERE UserId = @UserId AND SessionId = @SessionId
        END
        ELSE
        BEGIN
            INSERT INTO Teachers
            (
                UserId, SessionId,
                Gender, DateOfBirth, Qualification,
                ExperienceYears, Designation, Subject, JoiningDate,
                AddressLine1, AddressLine2, City, State, Pincode,
                TeacherImage, IsActive, CreatedDate
            )
            VALUES
            (
                @UserId, @SessionId,
                @Gender, @DateOfBirth, @Qualification,
                @ExperienceYears, @Designation, @Subject, @JoiningDate,
                @AddressLine1, @AddressLine2, @City, @State, @Pincode,
                @TeacherImage, 1, GETDATE()
            )
        END
    ", con);

            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@SessionId", sessionId);
            cmd.Parameters.AddWithValue("@Gender", (object?)model.Gender ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DateOfBirth", (object?)model.DateOfBirth ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Qualification", (object?)model.Qualification ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ExperienceYears", (object?)model.ExperienceYears ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Designation", (object?)model.Designation ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Subject", (object?)model.Subject ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@JoiningDate", (object?)model.JoiningDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@AddressLine1", (object?)model.AddressLine1 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@AddressLine2", (object?)model.AddressLine2 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@City", (object?)model.City ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@State", (object?)model.State ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Pincode", (object?)model.Pincode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@TeacherImage", (object?)fileName ?? DBNull.Value);

            con.Open();
            cmd.ExecuteNonQuery();

            HttpContext.Session.SetString("TeacherImage", fileName ?? "");

            TempData["Success"] = "Profile updated successfully";
            return RedirectToAction("TeacherDashboard");
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
