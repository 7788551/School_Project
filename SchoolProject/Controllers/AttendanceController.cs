using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SchoolProject.Helpers;
using SchoolProject.Models;
using System.Data;

namespace SchoolProject.Controllers
{
    [Authorize(Roles = "Teacher")]
    public class AttendanceController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly TeacherContextHelper _teacherHelper;

        // ✅ Constructor Injection
        public AttendanceController(
            IConfiguration configuration,
            TeacherContextHelper teacherHelper)
        {
            _configuration = configuration;
            _teacherHelper = teacherHelper;
        }

        // ==============================
        // LOGGED-IN TEACHER ID (FIXED)
        // ==============================
        private int GetLoggedInTeacherId()
        {
            var userIdClaim = User.FindFirst("UserId");
            if (userIdClaim == null)
                throw new UnauthorizedAccessException("UserId claim missing.");

            int userId = int.Parse(userIdClaim.Value);
            return _teacherHelper.GetTeacherIdFromUserId(userId);
        }

        // ==================================================
        // ATTENDANCE INDEX
        // ==================================================
        [HttpGet]
        public IActionResult AttendanceIndex()
        {
            // ✅ FETCH LOGGED-IN TEACHER USER ID (NOT TeacherId)
            var userIdClaim = User.FindFirst("UserId");
            if (userIdClaim == null)
            {
                TempData["Error"] = "User not authenticated.";
                return RedirectToAction("Login", "Account");
            }

            int teacherUserId = int.Parse(userIdClaim.Value);

            int? sessionId = HttpContext.Session.GetInt32("SessionId");

            // 🔁 Reload session if missing
            if (sessionId == null)
            {
                using SqlConnection con = new SqlConnection(
                    _configuration.GetConnectionString("DefaultConnection"));

                using SqlCommand cmd = new SqlCommand(@"
            SELECT TOP 1 SessionId, SessionName
            FROM AcademicSessions
            ORDER BY SessionId DESC", con);

                con.Open();
                using var dr = cmd.ExecuteReader();

                if (!dr.Read())
                {
                    TempData["Error"] = "No academic session found.";
                    return RedirectToAction("Login", "Account");
                }

                sessionId = Convert.ToInt32(dr["SessionId"]);
                HttpContext.Session.SetInt32("SessionId", sessionId.Value);
                HttpContext.Session.SetString("SessionName", dr["SessionName"].ToString()!);
            }

            // ✅ USE Teacher USER ID IN QUERY
            using SqlConnection con2 = new SqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

            using SqlCommand cmd2 = new SqlCommand(@"
        SELECT 
            a.ClassId,
            a.SectionId,
            c.ClassName,
            s.SectionName
        FROM ClassTeacherAssignments a
        INNER JOIN Classes c ON a.ClassId = c.ClassId
        INNER JOIN Sections s ON a.SectionId = s.SectionId
        WHERE a.TeacherId = @TeacherUserId
          AND a.SessionId = @SessionId
          AND a.IsActive = 1", con2);

            cmd2.Parameters.Add("@TeacherUserId", System.Data.SqlDbType.Int)
                .Value = teacherUserId;
            cmd2.Parameters.Add("@SessionId", System.Data.SqlDbType.Int)
                .Value = sessionId.Value;

            con2.Open();
            using var dr2 = cmd2.ExecuteReader();

            if (!dr2.Read())
                return View("NoClassAssigned");

            ViewBag.ClassId = dr2["ClassId"];
            ViewBag.SectionId = dr2["SectionId"];
            ViewBag.ClassName = dr2["ClassName"]?.ToString();
            ViewBag.SectionName = dr2["SectionName"]?.ToString();
            ViewBag.Today = DateTime.Today.ToString("dd-MM-yyyy");

            return View();
        }

        // ==================================================
        // LOAD STUDENTS (AJAX)
        // ==================================================


        [HttpGet]
        public IActionResult LoadStudents()
        {
            // ✅ Logged-in Teacher USER ID (from Claims)
            var userIdClaim = User.FindFirst("UserId");
            if (userIdClaim == null)
            {
                return Json(new { error = "User not authenticated." });
            }

            int teacherUserId = int.Parse(userIdClaim.Value);

            // 🔐 Current academic session
            int? sessionId = HttpContext.Session.GetInt32("SessionId");
            if (sessionId == null)
            {
                return Json(new { error = "Academic session not found." });
            }

            var list = new List<Attendance>();

            using SqlConnection con = new SqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

            using SqlCommand cmd = new SqlCommand(@"
        SELECT 
            st.StudentId,
            u.Name AS StudentName
        FROM ClassTeacherAssignments a
        INNER JOIN Students st
            ON st.ClassId = a.ClassId
           AND st.SectionId = a.SectionId
        INNER JOIN Users u 
            ON st.UserId = u.UserId
        WHERE a.TeacherId = @TeacherUserId
          AND a.SessionId = @SessionId
          AND a.IsActive = 1
        ORDER BY u.Name;", con);

            cmd.Parameters.Add("@TeacherUserId", System.Data.SqlDbType.Int)
                .Value = teacherUserId;
            cmd.Parameters.Add("@SessionId", System.Data.SqlDbType.Int)
                .Value = sessionId.Value;

            con.Open();
            using SqlDataReader dr = cmd.ExecuteReader();

            while (dr.Read())
            {
                list.Add(new Attendance
                {
                    StudentId = Convert.ToInt32(dr["StudentId"]),
                    StudentName = dr["StudentName"]?.ToString(),
                    IsPresent = true   // default checked
                });
            }

            return Json(list);
        }


        [HttpGet]
        public IActionResult NoClassAssigned()
        {
            return View();
        }

        // ==================================================
        // MARK ATTENDANCE
        // ==================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MarkAttendance(
    List<Attendance> model,
    DateTime attendanceDate)
        {
            // 🔐 Attendance can be marked only for today
            if (attendanceDate.Date != DateTime.Today)
            {
                TempData["Error"] = "Attendance can be marked only for today.";
                return RedirectToAction(nameof(AttendanceIndex));
            }

            // ✅ Logged-in Teacher USER ID (from Claims)
            var userIdClaim = User.FindFirst("UserId");
            if (userIdClaim == null)
            {
                TempData["Error"] = "User not authenticated.";
                return RedirectToAction(nameof(AttendanceIndex));
            }

            int teacherUserId = int.Parse(userIdClaim.Value);

            // 🔐 Current academic session
            int? sessionId = HttpContext.Session.GetInt32("SessionId");
            if (sessionId == null)
            {
                TempData["Error"] = "Academic session not found.";
                return RedirectToAction(nameof(AttendanceIndex));
            }

            using SqlConnection con = new SqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

            con.Open();

            foreach (var item in model)
            {
                using SqlCommand cmd = new SqlCommand(@"
            INSERT INTO StudentAttendance
                (SessionId, ClassId, SectionId, StudentId, AttendanceDate, Status, MarkedBy)
            SELECT TOP 1
                a.SessionId,
                a.ClassId,
                a.SectionId,
                @StudentId,
                @AttendanceDate,
                @Status,
                @TeacherUserId
            FROM ClassTeacherAssignments a
            WHERE a.TeacherId = @TeacherUserId
              AND a.SessionId = @SessionId
              AND a.IsActive = 1;", con);

                cmd.Parameters.Add("@StudentId", System.Data.SqlDbType.Int).Value = item.StudentId;
                cmd.Parameters.Add("@AttendanceDate", System.Data.SqlDbType.Date).Value = attendanceDate.Date;
                cmd.Parameters.Add("@Status", System.Data.SqlDbType.NVarChar, 10)
                    .Value = item.IsPresent ? "Present" : "Absent";
                cmd.Parameters.Add("@TeacherUserId", System.Data.SqlDbType.Int).Value = teacherUserId;
                cmd.Parameters.Add("@SessionId", System.Data.SqlDbType.Int).Value = sessionId.Value;

                cmd.ExecuteNonQuery();
            }

            TempData["Success"] = "Attendance marked successfully.";
            return RedirectToAction(nameof(AttendanceList));
        }




        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateSingleAttendance(
    int studentId,
    DateTime attendanceDate,
    bool isPresent)
        {
            // 🔐 Only today
            if (attendanceDate.Date != DateTime.Today)
            {
                return Json(new { success = false, message = "Editing allowed only for today." });
            }

            var userIdClaim = User.FindFirst("UserId");
            if (userIdClaim == null)
            {
                return Json(new { success = false, message = "Unauthorized." });
            }

            int teacherUserId = int.Parse(userIdClaim.Value);

            int? sessionId = HttpContext.Session.GetInt32("SessionId");
            if (sessionId == null)
            {
                return Json(new { success = false, message = "Session not found." });
            }

            using SqlConnection con = new SqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

            using SqlCommand cmd = new SqlCommand(@"
        UPDATE sa
        SET sa.Status = @Status
        FROM StudentAttendance sa
        INNER JOIN ClassTeacherAssignments a
            ON a.ClassId = sa.ClassId
           AND a.SectionId = sa.SectionId
           AND a.SessionId = sa.SessionId
        WHERE sa.StudentId = @StudentId
          AND sa.AttendanceDate = @AttendanceDate
          AND a.TeacherId = @TeacherUserId
          AND a.SessionId = @SessionId
          AND a.IsActive = 1", con);

            cmd.Parameters.Add("@StudentId", SqlDbType.Int).Value = studentId;
            cmd.Parameters.Add("@AttendanceDate", SqlDbType.Date).Value = attendanceDate.Date;
            cmd.Parameters.Add("@Status", SqlDbType.NVarChar, 10)
                .Value = isPresent ? "Present" : "Absent";
            cmd.Parameters.Add("@TeacherUserId", SqlDbType.Int).Value = teacherUserId;
            cmd.Parameters.Add("@SessionId", SqlDbType.Int).Value = sessionId.Value;

            con.Open();
            int rows = cmd.ExecuteNonQuery();

            return Json(new
            {
                success = rows > 0,
                message = rows > 0 ? "Attendance updated." : "Update failed."
            });
        }


        // ==================================================
        // ATTENDANCE LIST
        // ==================================================
        [HttpGet]
        public IActionResult AttendanceList()
        {
            var userIdClaim = User.FindFirst("UserId");
            if (userIdClaim == null)
            {
                TempData["Error"] = "User not authenticated.";
                return RedirectToAction("Login", "Account");
            }

            int teacherUserId = int.Parse(userIdClaim.Value);

            int? sessionId = HttpContext.Session.GetInt32("SessionId");
            if (sessionId == null)
            {
                TempData["Error"] = "Academic session not found.";
                return RedirectToAction(nameof(AttendanceIndex));
            }

            var list = new List<AttendanceList>();

            using SqlConnection con = new SqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

            using SqlCommand cmd = new SqlCommand(@"
        SELECT 
            sa.AttendanceId,
            sa.StudentId,
            c.ClassName,
            s.SectionName,
            ISNULL(u.Name, 'Student') AS StudentName,
            sa.AttendanceDate,
            sa.Status,
            sa.CreatedDate
        FROM StudentAttendance sa
        INNER JOIN ClassTeacherAssignments cta
            ON cta.ClassId = sa.ClassId
           AND cta.SectionId = sa.SectionId
           AND cta.SessionId = sa.SessionId
        LEFT JOIN Students st ON sa.StudentId = st.StudentId
        LEFT JOIN Users u ON st.UserId = u.UserId
        INNER JOIN Classes c ON sa.ClassId = c.ClassId
        INNER JOIN Sections s ON sa.SectionId = s.SectionId
        WHERE cta.TeacherId = @TeacherUserId
          AND cta.SessionId = @SessionId
          AND cta.IsActive = 1
        ORDER BY sa.AttendanceDate DESC;", con);

            cmd.Parameters.Add("@TeacherUserId", System.Data.SqlDbType.Int)
                .Value = teacherUserId;
            cmd.Parameters.Add("@SessionId", System.Data.SqlDbType.Int)
                .Value = sessionId.Value;

            con.Open();
            using SqlDataReader dr = cmd.ExecuteReader();

            while (dr.Read())
            {
                list.Add(new AttendanceList
                {
                    AttendanceId = Convert.ToInt32(dr["AttendanceId"]),
                    StudentId = Convert.ToInt32(dr["StudentId"]),
                    ClassName = dr["ClassName"]?.ToString(),
                    SectionName = dr["SectionName"]?.ToString(),
                    StudentName = dr["StudentName"]?.ToString(),
                    AttendanceDate = Convert.ToDateTime(dr["AttendanceDate"]),
                    Status = dr["Status"]?.ToString(),
                    CreatedDate = Convert.ToDateTime(dr["CreatedDate"])
                });
            }

            return View(list);
        }


    }
}
