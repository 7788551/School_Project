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

        public AttendanceController(
            IConfiguration configuration,
            TeacherContextHelper teacherHelper)
        {
            _configuration = configuration;
            _teacherHelper = teacherHelper;
        }

        // ==================================================
        // LOGGED-IN TEACHER ID (SESSION-WISE, FIXED)
        // ==================================================
        private int GetLoggedInTeacherId()
        {
            var userIdClaim = User.FindFirst("UserId");
            if (userIdClaim == null)
                throw new UnauthorizedAccessException("UserId claim missing.");

            int userId = int.Parse(userIdClaim.Value);

            // ✅ Converts UserId → TeacherId (CURRENT SESSION)
            return _teacherHelper.GetTeacherIdFromUserId(userId);
        }

        // ==================================================
        // CURRENT SESSION ID (AUTHORITATIVE)
        // ==================================================
        private int GetCurrentSessionId()
        {
            using SqlConnection con = new SqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

            using SqlCommand cmd = new SqlCommand(@"
                SELECT SessionId
                FROM AcademicSessions
                WHERE IsCurrent = 1", con);

            con.Open();
            object? result = cmd.ExecuteScalar();

            if (result == null || result == DBNull.Value)
                throw new Exception("Current academic session not configured.");

            return Convert.ToInt32(result);
        }

        // ==================================================
        // ATTENDANCE INDEX
        // ==================================================
        [HttpGet]
        public IActionResult AttendanceIndex()
        {
            int teacherId = GetLoggedInTeacherId();
            int sessionId = GetCurrentSessionId();

            using SqlConnection con = new SqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

            using SqlCommand cmd = new SqlCommand(@"
                SELECT 
                    a.ClassId,
                    a.SectionId,
                    c.ClassName,
                    s.SectionName
                FROM ClassTeacherAssignments a
                INNER JOIN Classes c ON a.ClassId = c.ClassId
                INNER JOIN Sections s ON a.SectionId = s.SectionId
                WHERE a.TeacherId = @TeacherId
                  AND a.SessionId = @SessionId
                  AND a.IsActive = 1", con);

            cmd.Parameters.Add("@TeacherId", SqlDbType.Int).Value = teacherId;
            cmd.Parameters.Add("@SessionId", SqlDbType.Int).Value = sessionId;

            con.Open();
            using var dr = cmd.ExecuteReader();

            if (!dr.Read())
                return View("NoClassAssigned");

            ViewBag.ClassId = dr["ClassId"];
            ViewBag.SectionId = dr["SectionId"];
            ViewBag.ClassName = dr["ClassName"]?.ToString();
            ViewBag.SectionName = dr["SectionName"]?.ToString();
            ViewBag.Today = DateTime.Today.ToString("dd-MM-yyyy");

            return View();
        }

        // ==================================================
        // LOAD STUDENTS (AJAX)
        // ==================================================
        [HttpGet]
        public IActionResult LoadStudents()
        {
            int teacherId = GetLoggedInTeacherId();
            int sessionId = GetCurrentSessionId();

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
                WHERE a.TeacherId = @TeacherId
                  AND a.SessionId = @SessionId
                  AND a.IsActive = 1
                ORDER BY u.Name", con);

            cmd.Parameters.Add("@TeacherId", SqlDbType.Int).Value = teacherId;
            cmd.Parameters.Add("@SessionId", SqlDbType.Int).Value = sessionId;

            con.Open();
            using SqlDataReader dr = cmd.ExecuteReader();

            while (dr.Read())
            {
                list.Add(new Attendance
                {
                    StudentId = Convert.ToInt32(dr["StudentId"]),
                    StudentName = dr["StudentName"]?.ToString(),
                    IsPresent = true
                });
            }

            return Json(list);
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
            if (attendanceDate.Date != DateTime.Today)
            {
                TempData["Error"] = "Attendance can be marked only for today.";
                return RedirectToAction(nameof(AttendanceIndex));
            }

            int teacherId = GetLoggedInTeacherId();
            int sessionId = GetCurrentSessionId();

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
                        @TeacherId
                    FROM ClassTeacherAssignments a
                    WHERE a.TeacherId = @TeacherId
                      AND a.SessionId = @SessionId
                      AND a.IsActive = 1", con);

                cmd.Parameters.Add("@StudentId", SqlDbType.Int).Value = item.StudentId;
                cmd.Parameters.Add("@AttendanceDate", SqlDbType.Date).Value = attendanceDate.Date;
                cmd.Parameters.Add("@Status", SqlDbType.NVarChar, 10)
                    .Value = item.IsPresent ? "Present" : "Absent";
                cmd.Parameters.Add("@TeacherId", SqlDbType.Int).Value = teacherId;
                cmd.Parameters.Add("@SessionId", SqlDbType.Int).Value = sessionId;

                cmd.ExecuteNonQuery();
            }

            TempData["Success"] = "Attendance marked successfully.";
            return RedirectToAction(nameof(AttendanceList));
        }

        // ==================================================
        // UPDATE SINGLE ATTENDANCE
        // ==================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateSingleAttendance(
            int studentId,
            DateTime attendanceDate,
            bool isPresent)
        {
            if (attendanceDate.Date != DateTime.Today)
            {
                return Json(new { success = false, message = "Editing allowed only for today." });
            }

            int teacherId = GetLoggedInTeacherId();
            int sessionId = GetCurrentSessionId();

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
                  AND a.TeacherId = @TeacherId
                  AND a.SessionId = @SessionId
                  AND a.IsActive = 1", con);

            cmd.Parameters.Add("@StudentId", SqlDbType.Int).Value = studentId;
            cmd.Parameters.Add("@AttendanceDate", SqlDbType.Date).Value = attendanceDate.Date;
            cmd.Parameters.Add("@Status", SqlDbType.NVarChar, 10)
                .Value = isPresent ? "Present" : "Absent";
            cmd.Parameters.Add("@TeacherId", SqlDbType.Int).Value = teacherId;
            cmd.Parameters.Add("@SessionId", SqlDbType.Int).Value = sessionId;

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
            int teacherId = GetLoggedInTeacherId();
            int sessionId = GetCurrentSessionId();

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
                WHERE cta.TeacherId = @TeacherId
                  AND cta.SessionId = @SessionId
                  AND cta.IsActive = 1
                ORDER BY sa.AttendanceDate DESC", con);

            cmd.Parameters.Add("@TeacherId", SqlDbType.Int).Value = teacherId;
            cmd.Parameters.Add("@SessionId", SqlDbType.Int).Value = sessionId;

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

        [HttpGet]
        public IActionResult NoClassAssigned()
        {
            return View();
        }
    }
}
