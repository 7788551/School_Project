using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SchoolProject.Models;
using System.Data;
using System.Security.Claims;

namespace SchoolProject.Controllers
{
    public class AccountController : Controller
    {
        private readonly IConfiguration _configuration;

        // ✅ Constructor injection
        public AccountController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Login(string PhoneNumber, string Password)
        {
            string cs = _configuration.GetConnectionString("DefaultConnection");

            int userId;
            string userName;
            string phone;
            string dbPassword;
            bool forceChangePassword;
            string roleName;
            int roleId;
            string adminImage = "default.png"; // ✅ fallback

            using SqlConnection con = new SqlConnection(cs);
            con.Open();

            // ================================
            // 1️⃣ LOGIN QUERY
            // ================================
            using (SqlCommand cmd = new SqlCommand(@"
        SELECT  
            u.UserId,
            u.Name,
            u.PhoneNumber,
            u.Password,
            u.ForceChangePassword,
            r.RoleName,
            r.RoleId
        FROM Users u
        INNER JOIN Roles r ON u.RoleId = r.RoleId
        WHERE u.PhoneNumber = @PhoneNumber", con))
            {
                cmd.Parameters.AddWithValue("@PhoneNumber", PhoneNumber);

                using SqlDataReader reader = cmd.ExecuteReader();
                if (!reader.Read())
                {
                    ViewBag.Error = "Invalid phone number or password";
                    return View();
                }

                userId = Convert.ToInt32(reader["UserId"]);
                userName = reader["Name"].ToString()!;
                phone = reader["PhoneNumber"].ToString()!;
                dbPassword = reader["Password"].ToString()!;
                forceChangePassword = Convert.ToBoolean(reader["ForceChangePassword"]);
                roleName = reader["RoleName"].ToString()!;
                roleId = Convert.ToInt32(reader["RoleId"]);
            }

            // ================================
            // 2️⃣ PASSWORD CHECK
            // ================================
            if (Password != dbPassword)
            {
                ViewBag.Error = "Invalid phone number or password";
                return View();
            }

            // ================================
            // 3️⃣ FETCH ADMIN IMAGE (ONLY IF ADMIN)
            // ================================
            if (roleId == 1) // Admin
            {
                using SqlCommand imgCmd = new(@"
            SELECT AdminImage
            FROM Admin
            WHERE UserId = @UserId", con);

                imgCmd.Parameters.AddWithValue("@UserId", userId);

                object? imgResult = imgCmd.ExecuteScalar();
                if (imgResult != null && imgResult != DBNull.Value)
                {
                    adminImage = imgResult.ToString()!;
                }
            }

            // ================================
            // 4️⃣ GET ACADEMIC SESSION
            // ================================
            int sessionId;
            string sessionName;

            using (SqlCommand sessionCmd = new SqlCommand(@"
        SELECT TOP 1 SessionId, SessionName
        FROM AcademicSessions
        ORDER BY SessionId DESC", con))
            {
                using SqlDataReader sdr = sessionCmd.ExecuteReader();
                if (!sdr.Read())
                {
                    ViewBag.Error = "No academic session found";
                    return View();
                }

                sessionId = Convert.ToInt32(sdr["SessionId"]);
                sessionName = sdr["SessionName"].ToString()!;
            }

            // ================================
            // 5️⃣ CLAIMS
            // ================================
            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
        new Claim("UserId", userId.ToString()),
        new Claim(ClaimTypes.Name, userName),
        new Claim(ClaimTypes.MobilePhone, phone),
        new Claim(ClaimTypes.Role, roleName)
    };

            if (forceChangePassword)
                claims.Add(new Claim("ForceChangePassword", "true"));

            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));

            // ================================
            // 6️⃣ SESSION (FINAL & CORRECT)
            // ================================
            HttpContext.Session.SetInt32("UserId", userId);
            HttpContext.Session.SetString("UserName", userName);
            HttpContext.Session.SetString("Role", roleName);
            HttpContext.Session.SetInt32("RoleId", roleId);
            HttpContext.Session.SetInt32("SessionId", sessionId);
            HttpContext.Session.SetString("SessionName", sessionName);
            HttpContext.Session.SetString("AdminImage", adminImage); // ✅ FIXED

            // ================================
            // 7️⃣ FORCE PASSWORD CHANGE
            // ================================
            if (forceChangePassword)
                return RedirectToAction("ChangePassword", "Account");

            // ================================
            // 8️⃣ ROLE-BASED REDIRECT
            // ================================
            return roleId switch
            {
                1 => RedirectToAction("AdminDashboard", "Admin"),
                2 => RedirectToAction("TeacherDashboard", "Teacher"),
                3 => RedirectToAction("StudentDashboard", "Student"),
                4 => RedirectToAction("AccountantDashboard", "Accountant"),
                _ => RedirectToAction("Login", "Account")
            };
        }


        [HttpGet]
        [Authorize]
        public IActionResult ChangePassword()
        {
            string role = User.FindFirst(ClaimTypes.Role)?.Value ?? "";

            // ❌ Only Teacher & Accountant are allowed here
            if ((role != "Teacher" && role != "Accountant") ||
                !User.HasClaim("ForceChangePassword", "true"))
            {
                return RedirectToAction("Login", "Account");
            }

            return View();
        }


        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(
     string NewPassword,
     string ConfirmPassword)
        {
            if (NewPassword != ConfirmPassword)
            {
                ViewBag.Error = "Passwords do not match";
                return View();
            }

            // ================================
            // Get user details from claims
            // ================================
            int userId = int.Parse(
                User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            string roleName = User.FindFirst(ClaimTypes.Role)!.Value;
            string userName = User.FindFirst(ClaimTypes.Name)!.Value;
            string phone = User.FindFirst(ClaimTypes.MobilePhone)!.Value;

            // ❌ Extra safety: only Teacher & Accountant
            if (roleName != "Teacher" && roleName != "Accountant")
            {
                return RedirectToAction("Login", "Account");
            }

            string cs = _configuration.GetConnectionString("DefaultConnection");

            // ================================
            // Update password & clear force flag
            // ================================
            string updateQuery = @"
        UPDATE Users
        SET Password = @Password,
            ForceChangePassword = 0
        WHERE UserId = @UserId";

            using SqlConnection con = new SqlConnection(cs);
            using SqlCommand cmd = new SqlCommand(updateQuery, con);

            cmd.Parameters.AddWithValue("@Password", NewPassword); // learning purpose
            cmd.Parameters.AddWithValue("@UserId", userId);

            con.Open();
            cmd.ExecuteNonQuery();

            // ================================
            // Re-sign in WITHOUT ForceChangePassword claim
            // ================================
            var claims = new List<Claim>
{
    new Claim("UserId", userId.ToString()),          // ✅ REQUIRED
    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
    new Claim(ClaimTypes.Name, userName),
    new Claim(ClaimTypes.MobilePhone, phone),
    new Claim(ClaimTypes.Role, roleName)
};



            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));

            // ================================
            // Redirect based on role
            // ================================
            return roleName switch
            {
                "Teacher" => RedirectToAction("TeacherDashboard", "Teacher"),
                "Accountant" => RedirectToAction("AccountantDashboard", "Accountant"),
                _ => RedirectToAction("Login", "Account")
            };
        }


        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            // 1️⃣ Remove authentication cookie
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            // 2️⃣ Clear session data (UserName, Role, PhoneNumber)
            HttpContext.Session.Clear();

            // 3️⃣ Redirect to login page
            return RedirectToAction("Index", "Home");
        }

       
    }
}
