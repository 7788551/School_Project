using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SchoolProject.Models;
using System.Data;

namespace SchoolProject.Controllers
{
    //[Authorize(Roles = "Admin")]
    public class ExamController : Controller
    {
        private readonly IConfiguration _configuration;

        public ExamController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private SqlConnection GetConnection()
        {
            return new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
        }

        [HttpGet]
        public IActionResult CreateExam()
        {
            Exam model = new Exam
            {
                  // VERY IMPORTANT
            };

            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateExam(Exam model)
        {
            // 🔒 SAFETY CHECK (JS failure protection)
            if (model.SessionId <= 0)
            {
                ModelState.AddModelError("", "Academic session not loaded. Please refresh the page.");
            }

            // 🔍 Validation check
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                using SqlConnection con = GetConnection();
                using SqlCommand cmd = new SqlCommand(@"
            INSERT INTO Exams
            (SessionId, ExamName, IsActive, CreatedDate)
            VALUES
            (@SessionId, @ExamName, 1, GETDATE());
            SELECT SCOPE_IDENTITY();", con);

                cmd.Parameters.AddWithValue("@SessionId", model.SessionId);
                cmd.Parameters.AddWithValue("@ExamName", model.ExamName);

                con.Open();
                int examId = Convert.ToInt32(cmd.ExecuteScalar());

                // 👉 Next step: Assign subjects to this exam
                return RedirectToAction(
                    "AssignSubjects",
                    "Exam",
                    new { examId }
                );
            }
            catch (Exception ex)
            {
                // 🧯 Show DB error instead of silent failure
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }


        [HttpGet]
        public IActionResult ExamList()
        {
            List<Exam> exams = new();

            using SqlConnection con = GetConnection();
            using SqlCommand cmd = new SqlCommand(@"
        SELECT 
            e.ExamId,
            e.SessionId,
            s.SessionName,
            e.ExamName,
            e.IsActive,
            e.CreatedDate
        FROM Exams e
        INNER JOIN AcademicSessions s 
            ON e.SessionId = s.SessionId
        WHERE e.IsActive = 1
        ORDER BY e.CreatedDate DESC
    ", con);

            con.Open();
            using SqlDataReader dr = cmd.ExecuteReader();

            while (dr.Read())
            {
                exams.Add(new Exam
                {
                    ExamId = Convert.ToInt32(dr["ExamId"]),
                    SessionId = Convert.ToInt32(dr["SessionId"]),
                    ExamName = dr["ExamName"].ToString(),
                    IsActive = Convert.ToBoolean(dr["IsActive"])
                });
            }

            return View(exams);
        }


        [HttpGet]
        public IActionResult AssignSubjects(int examId)
        {
            if (examId <= 0)
            {
                TempData["Error"] = "Please select an exam first.";
                return RedirectToAction("ExamList");
            }

            using SqlConnection con = GetConnection();
            con.Open();

            SqlCommand cmd = new SqlCommand(@"
        SELECT 
            c.ClassId,
            c.ClassName,
            s.SubjectId,
            s.SubjectName
        FROM ClassSubjects cs
        INNER JOIN Classes c ON cs.ClassId = c.ClassId
        INNER JOIN Subjects s ON cs.SubjectId = s.SubjectId
        WHERE cs.IsActive = 1
        ORDER BY c.ClassName, s.SubjectName", con);

            SqlDataReader dr = cmd.ExecuteReader();

            var subjects = new List<dynamic>();
            var classes = new Dictionary<int, string>();

            while (dr.Read())
            {
                int classId = Convert.ToInt32(dr["ClassId"]);
                string className = dr["ClassName"].ToString();

                if (!classes.ContainsKey(classId))
                    classes.Add(classId, className);

                subjects.Add(new
                {
                    ClassId = classId,
                    ClassName = className,
                    SubjectId = Convert.ToInt32(dr["SubjectId"]),
                    SubjectName = dr["SubjectName"].ToString()
                });
            }

            // 🔴 ALL THREE MUST BE SET
            ViewBag.ExamId = examId;
            ViewBag.Subjects = subjects;
            ViewBag.Classes = classes;

            return View();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AssignSubjects(List<ExamSubject> subjects)
        {
            if (subjects == null || subjects.Count == 0)
            {
                TempData["Error"] = "No subjects selected.";
                return RedirectToAction("ExamList");
            }

            // 🔒 Server-side safety: lock to one exam + class
            int examId = subjects[0].ExamId;
            int classId = subjects[0].ClassId;

            subjects = subjects
                .Where(s => s.ExamId == examId && s.ClassId == classId)
                .ToList();

            using SqlConnection con = GetConnection();
            con.Open();

            using SqlTransaction tran = con.BeginTransaction();

            try
            {
                // 🔴 Remove old assignments (Exam + Class)
                using (SqlCommand deleteCmd = new SqlCommand(
                    "DELETE FROM ExamSubjects WHERE ExamId = @ExamId AND ClassId = @ClassId",
                    con, tran))
                {
                    deleteCmd.Parameters.AddWithValue("@ExamId", examId);
                    deleteCmd.Parameters.AddWithValue("@ClassId", classId);
                    deleteCmd.ExecuteNonQuery();
                }

                // ✅ Insert fresh assignments
                foreach (var s in subjects)
                {
                    if (string.IsNullOrWhiteSpace(s.EvaluationType))
                        throw new Exception("Evaluation Type is required for all subjects.");

                    using SqlCommand cmd = new SqlCommand(@"
                INSERT INTO ExamSubjects
                (ExamId, ClassId, SubjectId, EvaluationType, MaxMarks, PassMarks)
                VALUES
                (@ExamId, @ClassId, @SubjectId, @EvaluationType, @MaxMarks, @PassMarks)",
                        con, tran);

                    cmd.Parameters.AddWithValue("@ExamId", s.ExamId);
                    cmd.Parameters.AddWithValue("@ClassId", s.ClassId);
                    cmd.Parameters.AddWithValue("@SubjectId", s.SubjectId);
                    cmd.Parameters.AddWithValue("@EvaluationType", s.EvaluationType);

                    if (s.EvaluationType == "Grade")
                    {
                        cmd.Parameters.AddWithValue("@MaxMarks", DBNull.Value);
                        cmd.Parameters.AddWithValue("@PassMarks", DBNull.Value);
                    }
                    else if (s.EvaluationType == "Marks")
                    {
                        if (s.MaxMarks <= 0)
                            throw new Exception($"Max Marks required for subject ID {s.SubjectId}");

                        if (s.PassMarks <= 0 || s.PassMarks > s.MaxMarks)
                            throw new Exception($"Invalid Pass Marks for subject ID {s.SubjectId}");

                        cmd.Parameters.AddWithValue("@MaxMarks", s.MaxMarks);
                        cmd.Parameters.AddWithValue("@PassMarks", s.PassMarks);
                    }
                    else
                    {
                        throw new Exception("Invalid Evaluation Type selected.");
                    }

                    cmd.ExecuteNonQuery();
                }

                tran.Commit();
                TempData["Success"] = "Subjects configured successfully.";

                // ✅ CORRECT REDIRECT (NO SCHEDULING)
                return RedirectToAction("ExamList", "Exam");
            }
            catch (Exception ex)
            {
                tran.Rollback();
                TempData["Error"] = ex.Message;
                return RedirectToAction("ExamList");
            }
        }

        [HttpGet]
        public IActionResult ViewExamSubjects(int examId)
        {
            using SqlConnection con = GetConnection();
            con.Open();

            SqlCommand cmd = new SqlCommand(@"
        SELECT 
            c.ClassId,
            c.ClassName,
            s.SubjectName,
            es.EvaluationType,
            es.MaxMarks,
            es.PassMarks
        FROM ExamSubjects es
        INNER JOIN Classes c ON es.ClassId = c.ClassId
        INNER JOIN Subjects s ON es.SubjectId = s.SubjectId
        WHERE es.ExamId = @ExamId
        ORDER BY c.ClassName, s.SubjectName", con);

            cmd.Parameters.AddWithValue("@ExamId", examId);

            var subjects = new List<dynamic>();
            var classes = new Dictionary<int, string>();

            using SqlDataReader dr = cmd.ExecuteReader();
            while (dr.Read())
            {
                int classId = Convert.ToInt32(dr["ClassId"]);
                string className = dr["ClassName"].ToString();

                if (!classes.ContainsKey(classId))
                    classes.Add(classId, className);

                subjects.Add(new
                {
                    ClassId = classId,
                    ClassName = className,
                    SubjectName = dr["SubjectName"].ToString(),
                    EvaluationType = dr["EvaluationType"].ToString(),
                    MaxMarks = dr["MaxMarks"] == DBNull.Value ? "-" : dr["MaxMarks"].ToString(),
                    PassMarks = dr["PassMarks"] == DBNull.Value ? "-" : dr["PassMarks"].ToString()
                });
            }

            ViewBag.ExamId = examId;
            ViewBag.Subjects = subjects;
            ViewBag.Classes = classes;

            return View();
        }



    }

}

