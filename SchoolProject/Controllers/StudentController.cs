using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SchoolProject.Models;

namespace SchoolProject.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentController : Controller
    {
        private readonly IConfiguration _configuration;

        // ✅ Constructor Injection
        public StudentController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // ================================
        // STUDENT DASHBOARD
        // ================================

        public IActionResult StudentDashboard() {
            return View();
        }

      
        public IActionResult StudentAttendanceList()
        {
            // 🔐 Logged-in user id
            int userId = Convert.ToInt32(User.FindFirst("UserId")!.Value);

            string cs = _configuration.GetConnectionString("DefaultConnection");

            // ================================
            // 🔁 STEP 0: Ensure SessionId exists (Auto Reload)
            // ================================
            int? sessionIdObj = HttpContext.Session.GetInt32("SessionId");

            if (sessionIdObj == null)
            {
                using SqlConnection con = new SqlConnection(cs);
                using SqlCommand cmd = new SqlCommand(@"
            SELECT TOP 1 SessionId
            FROM AcademicSessions
            ORDER BY SessionId DESC", con);

                con.Open();
                object result = cmd.ExecuteScalar();

                if (result == null || result == DBNull.Value)
                {
                    return Content("❌ No academic session found. Please contact admin.");
                }

                sessionIdObj = Convert.ToInt32(result);

                // 🔐 Restore session
                HttpContext.Session.SetInt32("SessionId", sessionIdObj.Value);
            }

            int sessionId = sessionIdObj.Value;

            int studentId = 0;

            // ================================
            // STEP 1: Get StudentId from UserId
            // ================================
            using (SqlConnection con = new SqlConnection(cs))
            {
                using SqlCommand cmd = new SqlCommand(
                    "SELECT StudentId FROM Students WHERE UserId = @UserId", con);

                cmd.Parameters.AddWithValue("@UserId", userId);

                con.Open();
                object result = cmd.ExecuteScalar();

                if (result == null || result == DBNull.Value)
                {
                    // 🚫 No student record linked with this user
                    return Content($"❌ No Student record found for UserId = {userId}. Please contact admin.");
                }

                studentId = Convert.ToInt32(result);
            }

            StudentAttendanceSummary summary = new StudentAttendanceSummary();

            // ================================
            // STEP 2: Fetch Attendance Summary
            // ================================
            using (SqlConnection con = new SqlConnection(cs))
            {
                using SqlCommand cmd = new SqlCommand(@"
SELECT  
    COUNT(*) AS TotalDays,
    ISNULL(SUM(CASE WHEN Status = 'Present' THEN 1 ELSE 0 END), 0) AS PresentDays,
    ISNULL(SUM(CASE WHEN Status = 'Absent' THEN 1 ELSE 0 END), 0) AS AbsentDays
FROM StudentAttendance
WHERE StudentId = @StudentId
  AND SessionId = @SessionId", con);

                cmd.Parameters.AddWithValue("@StudentId", studentId);
                cmd.Parameters.AddWithValue("@SessionId", sessionId);

                con.Open();
                using SqlDataReader dr = cmd.ExecuteReader();

                if (dr.Read())
                {
                    summary.TotalDays = Convert.ToInt32(dr["TotalDays"]);
                    summary.PresentDays = Convert.ToInt32(dr["PresentDays"]);
                    summary.AbsentDays = Convert.ToInt32(dr["AbsentDays"]);
                }
            }

            return View(summary);
        }

    }
}
