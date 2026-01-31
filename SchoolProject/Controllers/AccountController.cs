using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SchoolProject.Helpers;
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
        public async Task<IActionResult> Login(string LoginId, string Password)
        {
            string cs = _configuration.GetConnectionString("DefaultConnection");

            int userId;
            string userName;
            string phone;
            string dbPassword;
            bool forceChangePassword;
            string roleName;
            int roleId;

            string adminImage = "default.png";       // Admin fallback
            string accountantImage = "default.png";  // Accountant fallback

            bool isEmail = LoginId.Contains("@");

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
        WHERE 
        (
            (@IsEmail = 1 AND u.Email = @LoginId)
         OR (@IsEmail = 0 AND u.PhoneNumber = @LoginId)
        )", con))
            {
                cmd.Parameters.AddWithValue("@LoginId", LoginId);
                cmd.Parameters.AddWithValue("@IsEmail", isEmail ? 1 : 0);

                using SqlDataReader reader = cmd.ExecuteReader();
                if (!reader.Read())
                {
                    ViewBag.Error = "Invalid phone/email or password";
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
                ViewBag.Error = "Invalid phone/email or password";
                return View();
            }

            // ================================
            // 3️⃣ FETCH ROLE IMAGE
            // ================================
            if (roleId == 1) // ADMIN
            {
                using SqlCommand imgCmd = new(@"
            SELECT AdminImage
            FROM Admin
            WHERE UserId = @UserId", con);

                imgCmd.Parameters.AddWithValue("@UserId", userId);

                object? img = imgCmd.ExecuteScalar();
                if (img != null && img != DBNull.Value)
                    adminImage = img.ToString()!;
            }
            else if (roleId == 4) // ACCOUNTANT
            {
                using SqlCommand imgCmd = new(@"
            SELECT AccountantImage
            FROM Accountants
            WHERE UserId = @UserId", con);

                imgCmd.Parameters.AddWithValue("@UserId", userId);

                object? img = imgCmd.ExecuteScalar();
                if (img != null && img != DBNull.Value)
                    accountantImage = img.ToString()!;
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
            // 6️⃣ SESSION
            // ================================
            HttpContext.Session.SetInt32("UserId", userId);
            HttpContext.Session.SetString("UserName", userName);
            HttpContext.Session.SetString("Role", roleName);
            HttpContext.Session.SetInt32("RoleId", roleId);
            HttpContext.Session.SetInt32("SessionId", sessionId);
            HttpContext.Session.SetString("SessionName", sessionName);

            // 🔑 ROLE IMAGE SESSION
            HttpContext.Session.SetString("AdminImage", adminImage);
            HttpContext.Session.SetString("AccountantImage", accountantImage);

            // ================================
            // 7️⃣ FORCE PASSWORD CHANGE
            // ================================
            if (forceChangePassword)
                return RedirectToAction("ChangePassword", "Account");

            // ================================
            // 8️⃣ ROLE REDIRECT
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



        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public IActionResult ForgotPassword(string phoneNumber)
        {
            using SqlConnection con = new SqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

            SqlCommand cmd = new SqlCommand(
                @"SELECT UserId, RoleId 
          FROM Users 
          WHERE PhoneNumber = @Phone 
          AND IsActive = 1", con);

            cmd.Parameters.AddWithValue("@Phone", phoneNumber);

            con.Open();
            SqlDataReader reader = cmd.ExecuteReader();

            if (!reader.Read())
            {
                ViewBag.Error = "Phone number not found";
                return View();
            }

            int roleId = Convert.ToInt32(reader["RoleId"]);
            int userId = Convert.ToInt32(reader["UserId"]);

            // ❌ STUDENT BLOCKED (RoleId = 4)
            if (roleId == 4)
            {
                ViewBag.Error = "Please contact school admin for password reset";
                return View();
            }

            // ✅ ONLY Admin, Teacher, Accountant ALLOWED
            if (roleId != 1 && roleId != 2 && roleId != 3)
            {
                ViewBag.Error = "You are not allowed to reset password";
                return View();
            }

            TempData["UserId"] = userId;
            return RedirectToAction("EnterEmail");
        }


        [AllowAnonymous]
        public IActionResult EnterEmail()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public IActionResult EnterEmail(string email)
        {
            if (TempData["UserId"] == null)
                return RedirectToAction("ForgotPassword");

            int userId = Convert.ToInt32(TempData["UserId"]);
            TempData.Keep("UserId");

            string otp = new Random().Next(100000, 999999).ToString();
            DateTime expiry = DateTime.Now.AddMinutes(5);

            using SqlConnection con = new SqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

            con.Open();

            // ✅ 1️⃣ DELETE any existing OTPs for this user (old or unexpired)
            SqlCommand deleteCmd = new SqlCommand(
                "DELETE FROM PasswordResetOtp WHERE UserId = @UserId", con);
            deleteCmd.Parameters.AddWithValue("@UserId", userId);
            deleteCmd.ExecuteNonQuery();

            // ✅ 2️⃣ INSERT new OTP
            SqlCommand insertCmd = new SqlCommand(
                @"INSERT INTO PasswordResetOtp
          (UserId, OtpCode, ExpiryTime)
          VALUES (@UserId, @Otp, @Expiry)", con);

            insertCmd.Parameters.AddWithValue("@UserId", userId);
            insertCmd.Parameters.AddWithValue("@Otp", otp);
            insertCmd.Parameters.AddWithValue("@Expiry", expiry);
            insertCmd.ExecuteNonQuery();

            // ✅ 3️⃣ Send OTP email
            EmailHelper.SendOtpEmail(
                _configuration,
                email,
                otp
            );

            return RedirectToAction("VerifyOtp");
        }

        [AllowAnonymous]
        public IActionResult VerifyOtp()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public IActionResult VerifyOtp(string otp)
        {
            if (TempData["UserId"] == null)
                return RedirectToAction("ForgotPassword");

            int userId = Convert.ToInt32(TempData["UserId"]);
            TempData.Keep("UserId");

            using SqlConnection con = new SqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

            // 🔍 Check OTP (no IsUsed column)
            SqlCommand cmd = new SqlCommand(
                @"SELECT OtpId 
          FROM dbo.PasswordResetOtp
          WHERE UserId = @UserId
          AND OtpCode = @Otp
          AND ExpiryTime >= GETDATE()", con);

            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@Otp", otp);

            con.Open();
            object otpId = cmd.ExecuteScalar();

            if (otpId == null)
            {
                ViewBag.Error = "Invalid or expired OTP";
                return View();
            }

            // 🗑️ DELETE OTP AFTER SUCCESSFUL USE
            SqlCommand deleteCmd = new SqlCommand(
                "DELETE FROM dbo.PasswordResetOtp WHERE OtpId = @Id", con);

            deleteCmd.Parameters.AddWithValue("@Id", otpId);
            deleteCmd.ExecuteNonQuery();

            TempData["ResetUserId"] = userId;
            return RedirectToAction("ResetPassword");
        }


        [AllowAnonymous]
        public IActionResult ResetPassword()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public IActionResult ResetPassword(string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword)
            {
                ViewBag.Error = "Passwords do not match";
                return View();
            }

            if (TempData["ResetUserId"] == null)
                return RedirectToAction("ForgotPassword");

            int userId = Convert.ToInt32(TempData["ResetUserId"]);

            using SqlConnection con = new SqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

            SqlCommand cmd = new SqlCommand(
                @"UPDATE Users 
          SET Password = @Pwd,
              ForceChangePassword = 0
          WHERE UserId = @Id", con);

            cmd.Parameters.AddWithValue("@Pwd", newPassword);
            cmd.Parameters.AddWithValue("@Id", userId);

            con.Open();
            cmd.ExecuteNonQuery();

            return RedirectToAction("Login");
        }



    }
}
