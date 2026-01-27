using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SchoolProject.Helpers;
using SchoolProject.Models;
using System.Data;

namespace SchoolProject.Controllers
{
    //[Authorize(Roles = "Teacher")]
    public class MarksEntryController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly TeacherContextHelper _teacherHelper;

        public MarksEntryController(
         IConfiguration configuration,
         TeacherContextHelper teacherHelper)
        {
            _configuration = configuration;
            _teacherHelper = teacherHelper;
        }

        private int GetLoggedInTeacherId()
        {
            int userId = int.Parse(User.FindFirst("UserId")!.Value);
            return _teacherHelper.GetTeacherIdFromUserId(userId);
        }

        private SqlConnection GetConnection()
        {
            return new SqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));
        }

        // 🔹 GET: Marks Entry Page
        [HttpGet]
        public IActionResult MarksEntry(int examId, int classId, int subjectId)
        {
            using SqlConnection con = GetConnection();
            con.Open();

            // 1️⃣ Get subject evaluation type & max marks
            SqlCommand subjectCmd = new SqlCommand(@"
        SELECT EvaluationType, MaxMarks
        FROM ExamSubjects
        WHERE ExamId = @ExamId AND SubjectId = @SubjectId",
                con);

            subjectCmd.Parameters.AddWithValue("@ExamId", examId);
            subjectCmd.Parameters.AddWithValue("@SubjectId", subjectId);

            string evaluationType;
            decimal maxMarks;

            using (var reader = subjectCmd.ExecuteReader())
            {
                if (!reader.Read())
                    return BadRequest("Subject not configured for this exam.");

                evaluationType = reader["EvaluationType"]?.ToString() ?? "Marks";
                maxMarks = reader["MaxMarks"] == DBNull.Value
                    ? 0
                    : Convert.ToDecimal(reader["MaxMarks"]);
            }

            // 2️⃣ Load students of the class
            List<Student> students = new List<Student>();

            SqlCommand studentCmd = new SqlCommand(@"
        SELECT StudentId, AdmissionNumber, Name
        FROM Students
        WHERE ClassId = @ClassId AND IsActive = 1
        ORDER BY Name",
                con);

            studentCmd.Parameters.AddWithValue("@ClassId", classId);

            using (var reader = studentCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    students.Add(new Student
                    {
                        StudentId = Convert.ToInt32(reader["StudentId"]),
                        AdmissionNumber = reader["AdmissionNumber"]?.ToString() ?? "",
                        Name = reader["Name"]?.ToString() ?? ""
                    });
                }
            }

            // 3️⃣ Load grades ONLY if subject is grade-based
            List<GradeMaster> grades = new List<GradeMaster>();

            if (evaluationType == "Grade")
            {
                SqlCommand gradeCmd = new SqlCommand(@"
            SELECT GradeId, Grade
            FROM GradeMaster
            WHERE IsActive = 1
            ORDER BY MinPercentage DESC",
                    con);

                using (var reader = gradeCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        grades.Add(new GradeMaster
                        {
                            GradeId = Convert.ToInt32(reader["GradeId"]),
                            Grade = reader["Grade"]?.ToString() ?? ""
                        });
                    }
                }
            }

            // 4️⃣ Send everything to view
            ViewBag.ExamId = examId;
            ViewBag.ClassId = classId;
            ViewBag.SubjectId = subjectId;
            ViewBag.EvaluationType = evaluationType;
            ViewBag.MaxMarks = maxMarks;
            ViewBag.Students = students;
            ViewBag.Grades = grades;

            return View();
        }


        // 🔹 POST: Save Marks / Grade


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Save(StudentMarks model)
        {
            using SqlConnection con = GetConnection();
            con.Open();

            using SqlTransaction tran = con.BeginTransaction();

            try
            {
                // 1️⃣ Get EvaluationType & MaxMarks for subject
                SqlCommand subjectCmd = new SqlCommand(@"
            SELECT EvaluationType, MaxMarks
            FROM ExamSubjects
            WHERE ExamId = @ExamId AND SubjectId = @SubjectId",
                    con, tran);

                subjectCmd.Parameters.AddWithValue("@ExamId", model.ExamId);
                subjectCmd.Parameters.AddWithValue("@SubjectId", model.SubjectId);

                string evaluationType;
                decimal maxMarks;

                using (var rdr = subjectCmd.ExecuteReader())
                {
                    if (!rdr.Read())
                        throw new Exception("Exam subject not configured.");

                    evaluationType = rdr["EvaluationType"].ToString();
                    maxMarks = Convert.ToDecimal(rdr["MaxMarks"]);
                }

                decimal marksObtained = 0;
                int? gradeId = null;

                // 2️⃣ ABSENT CASE
                if (model.IsAbsent)
                {
                    marksObtained = 0;
                    gradeId = null;
                }
                // 3️⃣ GRADE SUBJECT
                else if (evaluationType == "Grade")
                {
                    if (model.GradeId == null)
                        throw new Exception("Grade is required.");

                    SqlCommand gradeCmd = new SqlCommand(@"
                SELECT MidPercentage
                FROM GradeMaster
                WHERE GradeId = @GradeId AND IsActive = 1",
                        con, tran);

                    gradeCmd.Parameters.AddWithValue("@GradeId", model.GradeId);

                    decimal midPercentage =
                        Convert.ToDecimal(gradeCmd.ExecuteScalar());

                    marksObtained =
                        Math.Round((midPercentage / 100) * maxMarks, 2);

                    gradeId = model.GradeId;
                }
                // 4️⃣ MARKS SUBJECT
                else
                {
                    if (model.MarksObtained < 0 || model.MarksObtained > maxMarks)
                        throw new Exception("Invalid marks.");

                    marksObtained = model.MarksObtained!.Value;

                    gradeId = null; // optional auto-grade later
                }

                // 5️⃣ INSERT OR UPDATE
                SqlCommand saveCmd = new SqlCommand(@"
            IF EXISTS (
                SELECT 1 FROM StudentMarks
                WHERE ExamId = @ExamId
                  AND StudentId = @StudentId
                  AND SubjectId = @SubjectId
            )
            BEGIN
                UPDATE StudentMarks
                SET MarksObtained = @MarksObtained,
                    MaxMarks = @MaxMarks,
                    GradeId = @GradeId,
                    IsAbsent = @IsAbsent
                WHERE ExamId = @ExamId
                  AND StudentId = @StudentId
                  AND SubjectId = @SubjectId
            END
            ELSE
            BEGIN
                INSERT INTO StudentMarks
                (ExamId, StudentId, ClassId, SubjectId,
                 MarksObtained, MaxMarks, GradeId, IsAbsent)
                VALUES
                (@ExamId, @StudentId, @ClassId, @SubjectId,
                 @MarksObtained, @MaxMarks, @GradeId, @IsAbsent)
            END",
                    con, tran);

                saveCmd.Parameters.AddWithValue("@ExamId", model.ExamId);
                saveCmd.Parameters.AddWithValue("@StudentId", model.StudentId);
                saveCmd.Parameters.AddWithValue("@ClassId", model.ClassId);
                saveCmd.Parameters.AddWithValue("@SubjectId", model.SubjectId);
                saveCmd.Parameters.AddWithValue("@MarksObtained", marksObtained);
                saveCmd.Parameters.AddWithValue("@MaxMarks", maxMarks);
                saveCmd.Parameters.AddWithValue("@GradeId",
                    gradeId == null ? DBNull.Value : gradeId);
                saveCmd.Parameters.AddWithValue("@IsAbsent", model.IsAbsent);

                saveCmd.ExecuteNonQuery();

                tran.Commit();

                TempData["Success"] = "Marks saved successfully.";
            }
            catch (Exception ex)
            {
                tran.Rollback();
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Index", new
            {
                examId = model.ExamId,
                classId = model.ClassId,
                subjectId = model.SubjectId
            });
        }


        // 🔐 Logged-in TeacherId (from Users table)
        //private int GetLoggedInTeacherId()
        //{
        //    // Example: UserId stored in Claims
        //    int userId = int.Parse(User.FindFirst("UserId")!.Value);

        //    using SqlConnection con = GetConnection();
        //    using SqlCommand cmd = new SqlCommand(
        //        "SELECT TeacherId FROM Teachers WHERE UserId = @UserId",
        //        con);

        //    cmd.Parameters.AddWithValue("@UserId", userId);

        //    con.Open();
        //    return Convert.ToInt32(cmd.ExecuteScalar());
        //}

        // 📅 Current Academic Session
        private int GetCurrentSessionId()
        {
            using SqlConnection con = GetConnection();
            using SqlCommand cmd = new SqlCommand(@"
        SELECT TOP 1 SessionId
        FROM AcademicSessions
        ORDER BY SessionId DESC", con);

            con.Open();
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        
        [Authorize(Roles = "Teacher")]
        [HttpGet]
       public IActionResult TeacherExams()
        {
            int teacherId = GetLoggedInTeacherId();
            int sessionId = GetCurrentSessionId();

            using SqlConnection con = GetConnection();
            con.Open();

            var assignedClassIds = new List<int>();

            using (SqlCommand cmd = new SqlCommand(@"
            SELECT DISTINCT ClassId
            FROM ClassTeacherAssignments
            WHERE TeacherId = @TeacherId
              AND SessionId = @SessionId
              AND IsActive = 1", con))
            {
                cmd.Parameters.AddWithValue("@TeacherId", teacherId);
                cmd.Parameters.AddWithValue("@SessionId", sessionId);

                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    assignedClassIds.Add(Convert.ToInt32(rdr["ClassId"]));
                }
            }

            if (!assignedClassIds.Any())
            {
                TempData["Error"] = "You are not assigned to any class for this session.";
                return RedirectToAction("TeacherDashboard", "Teacher");
            }

            // Step 2: Fetch exams for ALL assigned classes
            var exams = new List<Exam>();

            using (SqlCommand examCmd = new SqlCommand(@"
        SELECT DISTINCT e.ExamId, e.ExamName
        FROM Exams e
        INNER JOIN ExamSubjects es ON e.ExamId = es.ExamId
        WHERE es.ClassId IN (" + string.Join(",", assignedClassIds) + @")
          AND e.SessionId = @SessionId
        ORDER BY e.ExamName", con))
            {
                examCmd.Parameters.AddWithValue("@SessionId", sessionId);

                using var rdr = examCmd.ExecuteReader();
                while (rdr.Read())
                {
                    exams.Add(new Exam
                    {
                        ExamId = Convert.ToInt32(rdr["ExamId"]),
                        ExamName = rdr["ExamName"]!.ToString()
                    });
                }
            }

            return View();
        }


        [Authorize(Roles = "Teacher")]
        [HttpGet]
        public IActionResult StudentList(int examId)
        {
            int teacherId = GetLoggedInTeacherId();
            int sessionId = GetCurrentSessionId();

            using SqlConnection con = GetConnection();
            con.Open();

            // 1️⃣ Get ALL assigned class-section pairs
            var assignments = new List<(int ClassId, int SectionId)>();

            using (SqlCommand cmd = new SqlCommand(@"
        SELECT ClassId, SectionId
        FROM ClassTeacherAssignments
        WHERE TeacherId = @TeacherId
          AND SessionId = @SessionId
          AND IsActive = 1", con))
            {
                cmd.Parameters.AddWithValue("@TeacherId", teacherId);
                cmd.Parameters.AddWithValue("@SessionId", sessionId);

                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    assignments.Add((
                        (int)r["ClassId"],
                        (int)r["SectionId"]
                    ));
                }
            }

            if (!assignments.Any())
            {
                TempData["Error"] = "You are not assigned to any class.";
                return RedirectToAction("TeacherDashboard", "Teacher");
            }

            // 2️⃣ Validate exam belongs to one of teacher's classes
            bool examAllowed;
            using (SqlCommand examCmd = new SqlCommand(@"
        SELECT COUNT(1)
        FROM ExamSubjects es
        INNER JOIN Exams e ON e.ExamId = es.ExamId
        WHERE es.ExamId = @ExamId
          AND e.SessionId = @SessionId
          AND es.ClassId IN (" + string.Join(",", assignments.Select(a => a.ClassId).Distinct()) + ")", con))
            {
                examCmd.Parameters.AddWithValue("@ExamId", examId);
                examCmd.Parameters.AddWithValue("@SessionId", sessionId);

                examAllowed = (int)examCmd.ExecuteScalar() > 0;
            }

            if (!examAllowed)
            {
                TempData["Error"] = "You are not authorized for this exam.";
                return RedirectToAction("TeacherExams");
            }

            // 3️⃣ Load students for ALL assigned sections
            var students = new List<Student>();

            using (SqlCommand studentCmd = new SqlCommand(@"
        SELECT StudentId, AdmissionNumber, Name
        FROM Students
        WHERE IsActive = 1
          AND (" +
                  string.Join(" OR ",
                      assignments.Select(a =>
                          $"(ClassId = {a.ClassId} AND SectionId = {a.SectionId})")) +
                @")
        ORDER BY Name", con))
            {
                using var dr = studentCmd.ExecuteReader();
                while (dr.Read())
                {
                    students.Add(new Student
                    {
                        StudentId = (int)dr["StudentId"],
                        AdmissionNumber = dr["AdmissionNumber"].ToString()!,
                        Name = dr["Name"].ToString()!
                    });
                }
            }

            ViewBag.ExamId = examId;
            return View(students);
        }



    }
}
