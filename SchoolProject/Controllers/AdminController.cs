using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SchoolProject.Models;
using SchoolProject.Services;
using System.Data;

namespace SchoolProject.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly FeeService _feeService;
        private readonly IConfiguration _configuration;

        public AdminController(IConfiguration configuration, FeeService feeService)
        {
            _configuration = configuration;
            _feeService = feeService;
        }


        // ============================
        // DASHBOARD
        // ============================

        [HttpGet]
        [Authorize]
        public IActionResult GetCurrentSession()
        {
            using SqlConnection con = new SqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

            using SqlCommand cmd = new SqlCommand(@"
        SELECT SessionId, SessionName
        FROM AcademicSessions
        WHERE IsCurrent = 1", con);

            con.Open();

            using SqlDataReader dr = cmd.ExecuteReader();

            if (!dr.Read())
            {
                return Json(new
                {
                    sessionId = 0,
                    sessionName = "Error"
                });
            }

            return Json(new
            {
                sessionId = dr.GetInt32(0),
                sessionName = dr.GetString(1)
            });
        }


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
                throw new Exception("Current academic session is not configured.");

            return Convert.ToInt32(result);
        }



        public IActionResult AdminDashboard()
        {
            var session = _feeService.GetCurrentSession();
            int sessionId = session.SessionId;

            int classId = 1; // example: select class from dropdown or admin setting

            _feeService.GenerateMonthlyFees(sessionId, classId);

            return View();
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public IActionResult UsersList()
        {
            var list = new List<dynamic>();

            using SqlConnection con = new SqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

            using SqlCommand cmd = new SqlCommand(@"
        SELECT 
            u.UserId,
            u.Name,
            u.Email,
            u.PhoneNumber,
            r.RoleName,
            u.Password,
            u.IsActive,
            u.CreatedDate
        FROM Users u
        INNER JOIN Roles r ON u.RoleId = r.RoleId
        ORDER BY u.CreatedDate DESC", con);

            con.Open();
            using SqlDataReader dr = cmd.ExecuteReader();

            while (dr.Read())
            {
                list.Add(new
                {
                    UserId = dr.GetInt32(0),
                    Name = dr.GetString(1),
                    Email = dr["Email"] == DBNull.Value ? "" : dr.GetString(2),
                    Phone = dr.GetString(3),
                    Role = dr.GetString(4),
                    Password = dr.GetString(5),   // ⚠️ PLAIN TEXT
                    IsActive = dr.GetBoolean(6),
                    CreatedDate = Convert.ToDateTime(dr["CreatedDate"])
                });
            }

            return View(list);
        }


        [HttpGet]
        public IActionResult AdminProfiledata()
        {
            int userId = Convert.ToInt32(HttpContext.Session.GetInt32("UserId"));

            AdminProfile model = new();
            string cs = _configuration.GetConnectionString("DefaultConnection");

            using SqlConnection con = new(cs);
            using SqlCommand cmd = new(@"
                SELECT 
                    u.UserId, u.Name, u.Email, u.PhoneNumber,
                    a.AdminId, a.Gender, a.DateOfBirth, a.Qualification,
                    a.ExperienceYears, a.Designation, a.JoiningDate,
                    a.AddressLine1, a.AddressLine2, a.City, a.State,
                    a.Pincode, a.AdminImage
                FROM Users u
                INNER JOIN Admin a ON a.UserId = u.UserId
                WHERE u.UserId = @UserId
            ", con);

            cmd.Parameters.AddWithValue("@UserId", userId);
            con.Open();

            using SqlDataReader dr = cmd.ExecuteReader();
            if (dr.Read())
            {
                model.UserId = userId;
                model.AdminId = Convert.ToInt32(dr["AdminId"]);
                model.Name = dr["Name"].ToString()!;
                model.Email = dr["Email"]?.ToString();
                model.PhoneNumber = dr["PhoneNumber"].ToString()!;
                model.Gender = dr["Gender"]?.ToString();
                model.DateOfBirth = dr["DateOfBirth"] as DateTime?;
                model.Qualification = dr["Qualification"]?.ToString();
                model.ExperienceYears = dr["ExperienceYears"] as int?;
                model.Designation = dr["Designation"]?.ToString();
                model.JoiningDate = dr["JoiningDate"] as DateTime?;
                model.AddressLine1 = dr["AddressLine1"]?.ToString();
                model.AddressLine2 = dr["AddressLine2"]?.ToString();
                model.City = dr["City"]?.ToString();
                model.State = dr["State"]?.ToString();
                model.Pincode = dr["Pincode"]?.ToString();
                model.ExistingAdminImage = dr["AdminImage"]?.ToString();
            }

            model.IsEditMode = false; // VIEW MODE
            return View(model);
        }

    

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AdminProfiledata(AdminProfile model)
        {
            int? sessionUserId = HttpContext.Session.GetInt32("UserId");
            if (sessionUserId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            int userId = sessionUserId.Value;

            string fileName = model.ExistingAdminImage;

            if (model.AdminImage != null && model.AdminImage.Length > 0)
            {
                fileName = Guid.NewGuid() + Path.GetExtension(model.AdminImage.FileName);

                string folderPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot/images/Admins"
                );

                // ensure folder exists
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                string fullPath = Path.Combine(folderPath, fileName);

                using FileStream fs = new(fullPath, FileMode.Create);
                model.AdminImage.CopyTo(fs);
            }

            string cs = _configuration.GetConnectionString("DefaultConnection");
            using SqlConnection con = new(cs);
            using SqlCommand cmd = new(@"
        UPDATE Admin
        SET
            Gender = @Gender,
            DateOfBirth = @DateOfBirth,
            Qualification = @Qualification,
            ExperienceYears = @ExperienceYears,
            Designation = @Designation,
            JoiningDate = @JoiningDate,
            AddressLine1 = @AddressLine1,
            AddressLine2 = @AddressLine2,
            City = @City,
            State = @State,
            Pincode = @Pincode,
            AdminImage = @AdminImage,
            UpdatedDate = GETDATE()
        WHERE UserId = @UserId
    ", con);

            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@Gender", (object?)model.Gender ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@DateOfBirth", (object?)model.DateOfBirth ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Qualification", (object?)model.Qualification ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ExperienceYears", (object?)model.ExperienceYears ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Designation", (object?)model.Designation ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@JoiningDate", (object?)model.JoiningDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@AddressLine1", (object?)model.AddressLine1 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@AddressLine2", (object?)model.AddressLine2 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@City", (object?)model.City ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@State", (object?)model.State ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Pincode", (object?)model.Pincode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@AdminImage", (object?)fileName ?? DBNull.Value);

            con.Open();
            cmd.ExecuteNonQuery();

            TempData["Success"] = "Profile updated successfully";
            return RedirectToAction("AdminProfiledata");
        }



        [HttpGet]
        public IActionResult Enquiries()
        {
            List<Enquiry> enquiries = new();

            string cs = _configuration.GetConnectionString("DefaultConnection");

            using SqlConnection con = new(cs);
            using SqlCommand cmd = new(@"
        SELECT 
            EnquiryId,
            FirstName,
            LastName,
            PhoneNumber,
            Email,
            Message,
            CreatedDate,
            IsRead
        FROM Enquiries
        ORDER BY CreatedDate DESC
    ", con);

            con.Open();

            using SqlDataReader dr = cmd.ExecuteReader();
            while (dr.Read())
            {
                enquiries.Add(new Enquiry
                {
                    EnquiryId = Convert.ToInt32(dr["EnquiryId"]),
                    FirstName = dr["FirstName"]?.ToString(),
                    LastName = dr["LastName"]?.ToString(),
                    PhoneNumber = dr["PhoneNumber"]?.ToString(),
                    Email = dr["Email"]?.ToString(),
                    Message = dr["Message"]?.ToString(),
                    CreatedDate = Convert.ToDateTime(dr["CreatedDate"]),
                    IsRead = Convert.ToBoolean(dr["IsRead"])
                });
            }

            return View(enquiries);
        }


        [HttpGet]
        public IActionResult MarkAsRead(int id)
        {
            string cs = _configuration.GetConnectionString("DefaultConnection");

            using SqlConnection con = new(cs);
            using SqlCommand cmd = new(
                "UPDATE Enquiries SET IsRead = 1 WHERE EnquiryId = @Id", con);

            cmd.Parameters.AddWithValue("@Id", id);

            con.Open();
            cmd.ExecuteNonQuery();

            return RedirectToAction(nameof(Enquiries));
        }

        [HttpPost]
        [Authorize] // Admin only (global auth already applies)
        public IActionResult DeleteAllEnquiries()
        {
            string cs = _configuration.GetConnectionString("DefaultConnection");

            using SqlConnection con = new(cs);
            using SqlCommand cmd = new(
                "DELETE FROM Enquiries",
                con
            );

            con.Open();
            cmd.ExecuteNonQuery();

            TempData["Success"] = "All enquiries have been deleted successfully!";
            return RedirectToAction("Enquiries");
        }


        

        [HttpGet]
        public IActionResult GetTeacherTypes()
        {
            var list = new List<TeacherType>();
            string cs = _configuration.GetConnectionString("DefaultConnection");

            using SqlConnection con = new SqlConnection(cs);
            using SqlCommand cmd = new SqlCommand(
                "SELECT TeacherTypeId, TypeName FROM dbo.TeacherTypes ORDER BY TypeName", con);

            con.Open();
            using SqlDataReader dr = cmd.ExecuteReader();

            while (dr.Read())
            {
                list.Add(new TeacherType
                {
                    TeacherTypeId = dr.GetInt32(0),
                    TypeName = dr.GetString(1)
                });
            }

            return Json(list); // 🔥 IMPORTANT
        }

        [HttpGet]
        public IActionResult AddTeacher()
        {
            return View();
        }

       

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddTeacher(AddTeacher dto)
        {
            if (!ModelState.IsValid)
            {
                return View(dto);
            }

            string cs = _configuration.GetConnectionString("DefaultConnection");

            // Default password = DOB (ddMMyyyy)
            string rawPassword = dto.DateOfBirth.ToString("ddMMyyyy");

            // ================================
            // IMAGE UPLOAD (ASP.NET CORE)
            // ================================
            string? imageFileName = null;

            if (dto.TeacherImage != null && dto.TeacherImage.Length > 0)
            {
                // Get original file name (without path)
                string originalFileName = Path.GetFileName(dto.TeacherImage.FileName);

                // Sanitize filename (remove spaces & special chars)
                string safeFileName = string.Concat(
                    originalFileName.Split(Path.GetInvalidFileNameChars())
                ).Replace(" ", "_");

                string uploadFolder = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot",
                    "images",
                    "Teacherprofileimage"
                );

                if (!Directory.Exists(uploadFolder))
                {
                    Directory.CreateDirectory(uploadFolder);
                }

                string filePath = Path.Combine(uploadFolder, safeFileName);

                // 🔁 If file already exists, append timestamp
                if (System.IO.File.Exists(filePath))
                {
                    string name = Path.GetFileNameWithoutExtension(safeFileName);
                    string ext = Path.GetExtension(safeFileName);

                    safeFileName = $"{name}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
                    filePath = Path.Combine(uploadFolder, safeFileName);
                }

                using var stream = new FileStream(filePath, FileMode.Create);
                dto.TeacherImage.CopyTo(stream);

                imageFileName = safeFileName; // Save THIS to DB
            }


            using SqlConnection con = new SqlConnection(cs);
            con.Open();

            try
            {
                // ================================
                // DUPLICATE PHONE CHECK
                // ================================
                using SqlCommand phoneCmd = new SqlCommand(
                    "SELECT COUNT(*) FROM Users WHERE PhoneNumber = @PhoneNumber", con);
                phoneCmd.Parameters.AddWithValue("@PhoneNumber", dto.PhoneNumber);

                if ((int)phoneCmd.ExecuteScalar() > 0)
                {
                    ModelState.AddModelError("PhoneNumber", "Phone number already exists");
                    return View(dto);
                }

                // ================================
                // DUPLICATE EMAIL CHECK
                // ================================
                using SqlCommand emailCmd = new SqlCommand(
                    "SELECT COUNT(*) FROM Users WHERE Email = @Email", con);
                emailCmd.Parameters.AddWithValue("@Email", dto.Email);

                if ((int)emailCmd.ExecuteScalar() > 0)
                {
                    ModelState.AddModelError("Email", "Email already exists");
                    return View(dto);
                }

                // ================================
                // INSERT USER
                // ================================
                string userInsertQuery = @"
INSERT INTO Users
(Name, Email, PhoneNumber, Password, RoleId, ForceChangePassword, IsActive)
OUTPUT INSERTED.UserId
VALUES
(@Name, @Email, @PhoneNumber, @Password,
 (SELECT RoleId FROM Roles WHERE RoleName = 'Teacher'),
 1, 1)";

                int userId;
                using (SqlCommand userCmd = new SqlCommand(userInsertQuery, con))
                {
                    userCmd.Parameters.AddWithValue("@Name", dto.Name);
                    userCmd.Parameters.AddWithValue("@Email", dto.Email);
                    userCmd.Parameters.AddWithValue("@PhoneNumber", dto.PhoneNumber);
                    userCmd.Parameters.AddWithValue("@Password", rawPassword);

                    userId = Convert.ToInt32(userCmd.ExecuteScalar());
                }

                // ================================
                // INSERT TEACHER
                // ================================
                string teacherInsertQuery = @"
INSERT INTO Teachers
(
    UserId, SessionId, TeacherTypeId, Name,
    DateOfBirth, Qualification, ExperienceYears,
    JoiningDate, Designation, Subject,
    PhoneNumber,
    AddressLine1, AddressLine2,
    City, State, Pincode,
    TeacherImage,
    IsActive, CreatedDate
)
VALUES
(
    @UserId, @SessionId, @TeacherTypeId, @Name,
    @DateOfBirth, @Qualification, @ExperienceYears,
    @JoiningDate, @Designation, @Subject,
    @PhoneNumber,
    @AddressLine1, @AddressLine2,
    @City, @State, @Pincode,
    @TeacherImage,
    1, GETDATE()
)";

                using SqlCommand teacherCmd = new SqlCommand(teacherInsertQuery, con);
                teacherCmd.Parameters.AddWithValue("@UserId", userId);
                teacherCmd.Parameters.AddWithValue("@SessionId", dto.SessionId);
                teacherCmd.Parameters.AddWithValue("@TeacherTypeId", dto.TeacherTypeId);
                teacherCmd.Parameters.AddWithValue("@Name", dto.Name);
                teacherCmd.Parameters.AddWithValue("@DateOfBirth", dto.DateOfBirth);

                teacherCmd.Parameters.AddWithValue("@Qualification", (object?)dto.Qualification ?? DBNull.Value);
                teacherCmd.Parameters.AddWithValue("@ExperienceYears", (object?)dto.ExperienceYears ?? DBNull.Value);
                teacherCmd.Parameters.AddWithValue("@JoiningDate", (object?)dto.JoiningDate ?? DBNull.Value);
                teacherCmd.Parameters.AddWithValue("@Designation", (object?)dto.Designation ?? DBNull.Value);
                teacherCmd.Parameters.AddWithValue("@Subject", (object?)dto.Subject ?? DBNull.Value);

                teacherCmd.Parameters.AddWithValue("@PhoneNumber", dto.PhoneNumber);
                teacherCmd.Parameters.AddWithValue("@AddressLine1", (object?)dto.AddressLine1 ?? DBNull.Value);
                teacherCmd.Parameters.AddWithValue("@AddressLine2", (object?)dto.AddressLine2 ?? DBNull.Value);
                teacherCmd.Parameters.AddWithValue("@City", (object?)dto.City ?? DBNull.Value);
                teacherCmd.Parameters.AddWithValue("@State", (object?)dto.State ?? DBNull.Value);
                teacherCmd.Parameters.AddWithValue("@Pincode", (object?)dto.Pincode ?? DBNull.Value);
                teacherCmd.Parameters.AddWithValue("@TeacherImage", (object?)imageFileName ?? DBNull.Value);

                teacherCmd.ExecuteNonQuery();

                TempData["Success"] = "Teacher added successfully. Default password is DOB.";
                return RedirectToAction("Teacher");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(dto);
            }
        }




        [HttpGet]
        public IActionResult Teacher()
        {
            var list = new List<Teacher>();
            string cs = _configuration.GetConnectionString("DefaultConnection");

            using SqlConnection con = new SqlConnection(cs);
            using SqlCommand cmd = new SqlCommand(@"
        SELECT 
            u.UserId,
            u.Name,
            u.Email,
            u.PhoneNumber,
            t.TeacherImage
        FROM dbo.Users u
        INNER JOIN dbo.Teachers t ON u.UserId = t.UserId
        INNER JOIN dbo.Roles r ON u.RoleId = r.RoleId
        WHERE r.RoleName = 'Teacher'
          AND u.IsActive = 1
          AND t.IsActive = 1
        ORDER BY u.Name", con);

            con.Open();
            using SqlDataReader dr = cmd.ExecuteReader();

            while (dr.Read())
            {
                list.Add(new Teacher
                {
                    UserId = dr.GetInt32(0),
                    Name = dr.GetString(1),
                    Email = dr.IsDBNull(2) ? null : dr.GetString(2),
                    PhoneNumber = dr.IsDBNull(3) ? null : dr.GetString(3),
                    TeacherImage = dr.IsDBNull(4) ? null : dr.GetString(4)
                });
            }

            return View(list);
        }


        [HttpGet]
        public IActionResult EditTeacher(int id)
        {
            string cs = _configuration.GetConnectionString("DefaultConnection");

            using SqlConnection con = new SqlConnection(cs);
            using SqlCommand cmd = new SqlCommand(@"
        SELECT u.UserId, u.Name, u.Email, u.PhoneNumber,
               t.SessionId, t.TeacherTypeId, t.Subject, t.Designation,
               t.DateOfBirth, t.Gender, t.JoiningDate,
               t.Qualification, t.ExperienceYears,
               t.AddressLine1, t.AddressLine2, t.City, t.State, t.Pincode,
               t.TeacherImage
        FROM Users u
        INNER JOIN Teachers t ON u.UserId = t.UserId
        WHERE u.UserId = @Id", con);

            cmd.Parameters.AddWithValue("@Id", id);
            con.Open();

            using SqlDataReader dr = cmd.ExecuteReader();
            if (!dr.Read()) return NotFound();

            return View(new EditTeacher
            {
                UserId = id,
                Name = dr.GetString(1),
                Email = dr.IsDBNull(2) ? null : dr.GetString(2),
                PhoneNumber = dr.GetString(3),
                SessionId = dr.GetInt32(4),
                TeacherTypeId = dr.GetInt32(5),
                Subject = dr.IsDBNull(6) ? null : dr.GetString(6),
                Designation = dr.IsDBNull(7) ? null : dr.GetString(7),
                DateOfBirth = dr.GetDateTime(8),
                Gender = dr.IsDBNull(9) ? null : dr.GetString(9),
                JoiningDate = dr.IsDBNull(10) ? null : dr.GetDateTime(10),
                Qualification = dr.IsDBNull(11) ? null : dr.GetString(11),
                ExperienceYears = dr.IsDBNull(12) ? null : dr.GetInt32(12),
                AddressLine1 = dr.IsDBNull(13) ? null : dr.GetString(13),
                AddressLine2 = dr.IsDBNull(14) ? null : dr.GetString(14),
                City = dr.IsDBNull(15) ? null : dr.GetString(15),
                State = dr.IsDBNull(16) ? null : dr.GetString(16),
                Pincode = dr.IsDBNull(17) ? null : dr.GetString(17),

                // ✅ OLD IMAGE
                ExistingTeacherImage = dr.IsDBNull(18) ? null : dr.GetString(18)
            });
        }





        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditTeacher(EditTeacher dto)
        {
            if (dto.UserId <= 0)
            {
                TempData["Error"] = "Invalid teacher record";
                return RedirectToAction("Teacher");
            }

            string cs = _configuration.GetConnectionString("DefaultConnection");

            string? finalImageName = dto.ExistingTeacherImage;

            // ============================
            // IMAGE UPDATE LOGIC
            // ============================
            if (dto.TeacherImage != null && dto.TeacherImage.Length > 0)
            {
                string uploadFolder = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot",
                    "images",
                    "Teacherprofileimage"
                );

                if (!Directory.Exists(uploadFolder))
                    Directory.CreateDirectory(uploadFolder);

                // delete OLD image only when NEW is uploaded
                if (!string.IsNullOrEmpty(dto.ExistingTeacherImage))
                {
                    string oldPath = Path.Combine(uploadFolder, dto.ExistingTeacherImage);
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                // save NEW image (keep original name safely)
                string originalName = Path.GetFileName(dto.TeacherImage.FileName);
                string safeName = originalName.Replace(" ", "_");

                string newPath = Path.Combine(uploadFolder, safeName);

                // avoid overwrite
                if (System.IO.File.Exists(newPath))
                {
                    string name = Path.GetFileNameWithoutExtension(safeName);
                    string ext = Path.GetExtension(safeName);
                    safeName = $"{name}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
                    newPath = Path.Combine(uploadFolder, safeName);
                }

                using var stream = new FileStream(newPath, FileMode.Create);
                dto.TeacherImage.CopyTo(stream);

                finalImageName = safeName;
            }

            using SqlConnection con = new SqlConnection(cs);
            con.Open();
            using SqlTransaction tran = con.BeginTransaction();

            try
            {
                // ============================
                // UPDATE USERS
                // ============================
                using SqlCommand cmd1 = new SqlCommand(@"
            UPDATE Users
            SET Name = @Name,
                Email = @Email,
                PhoneNumber = @Phone
            WHERE UserId = @UserId", con, tran);

                cmd1.Parameters.AddWithValue("@Name", dto.Name);
                cmd1.Parameters.AddWithValue("@Email", (object?)dto.Email ?? DBNull.Value);
                cmd1.Parameters.AddWithValue("@Phone", dto.PhoneNumber);
                cmd1.Parameters.AddWithValue("@UserId", dto.UserId);
                cmd1.ExecuteNonQuery();

                // ============================
                // UPDATE TEACHERS (IMAGE INCLUDED)
                // ============================
                using SqlCommand cmd2 = new SqlCommand(@"
            UPDATE Teachers
            SET TeacherTypeId   = @TeacherTypeId,
                Subject         = @Subject,
                Designation     = @Designation,
                Qualification   = @Qualification,
                ExperienceYears = @ExperienceYears,
                Gender          = @Gender,
                DateOfBirth     = @DateOfBirth,
                JoiningDate     = @JoiningDate,
                AddressLine1    = @AddressLine1,
                AddressLine2    = @AddressLine2,
                City            = @City,
                State           = @State,
                Pincode         = @Pincode,
                TeacherImage    = @TeacherImage
            WHERE UserId = @UserId", con, tran);

                cmd2.Parameters.AddWithValue("@TeacherTypeId", dto.TeacherTypeId);
                cmd2.Parameters.AddWithValue("@Subject", (object?)dto.Subject ?? DBNull.Value);
                cmd2.Parameters.AddWithValue("@Designation", (object?)dto.Designation ?? DBNull.Value);
                cmd2.Parameters.AddWithValue("@Qualification", (object?)dto.Qualification ?? DBNull.Value);
                cmd2.Parameters.AddWithValue("@ExperienceYears", (object?)dto.ExperienceYears ?? DBNull.Value);
                cmd2.Parameters.AddWithValue("@Gender", (object?)dto.Gender ?? DBNull.Value);
                cmd2.Parameters.AddWithValue("@DateOfBirth", dto.DateOfBirth);
                cmd2.Parameters.AddWithValue("@JoiningDate", (object?)dto.JoiningDate ?? DBNull.Value);
                cmd2.Parameters.AddWithValue("@AddressLine1", (object?)dto.AddressLine1 ?? DBNull.Value);
                cmd2.Parameters.AddWithValue("@AddressLine2", (object?)dto.AddressLine2 ?? DBNull.Value);
                cmd2.Parameters.AddWithValue("@City", (object?)dto.City ?? DBNull.Value);
                cmd2.Parameters.AddWithValue("@State", (object?)dto.State ?? DBNull.Value);
                cmd2.Parameters.AddWithValue("@Pincode", (object?)dto.Pincode ?? DBNull.Value);
                cmd2.Parameters.AddWithValue("@TeacherImage", (object?)finalImageName ?? DBNull.Value);
                cmd2.Parameters.AddWithValue("@UserId", dto.UserId);

                cmd2.ExecuteNonQuery();

                tran.Commit();
                TempData["Success1"] = "Teacher updated successfully";
            }
            catch
            {
                tran.Rollback();
                TempData["Error"] = "Update failed";
            }

            return RedirectToAction("Teacher");
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteTeacher(int id)
        {
            string cs = _configuration.GetConnectionString("DefaultConnection");

            using SqlConnection con = new SqlConnection(cs);
            con.Open();

            using SqlTransaction tran = con.BeginTransaction();

            try
            {
                using SqlCommand cmd1 = new SqlCommand(
                    "UPDATE Users SET IsActive = 0 WHERE UserId = @Id",
                    con, tran);
                cmd1.Parameters.AddWithValue("@Id", id);
                cmd1.ExecuteNonQuery();

                using SqlCommand cmd2 = new SqlCommand(
                    "UPDATE Teachers SET IsActive = 0 WHERE UserId = @Id",
                    con, tran);
                cmd2.Parameters.AddWithValue("@Id", id);
                cmd2.ExecuteNonQuery();

                tran.Commit();

                TempData["Success"] = "Teacher deleted successfully";
            }
            catch
            {
                tran.Rollback();
                TempData["Error"] = "Unable to delete teacher";
            }

            return RedirectToAction("Teacher");
        }
            

        [HttpGet]
        public IActionResult AddStudent()
        {
            return View();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddStudent(AddStudent dto)
        {
            string cs = _configuration.GetConnectionString("DefaultConnection");

            int currentSessionId;
            try
            {
                currentSessionId = GetCurrentSessionId();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(dto);
            }

            if (dto.ClassId <= 0 || dto.SectionId <= 0)
            {
                ModelState.AddModelError("", "Please select valid Class and Section.");
                return View(dto);
            }

            string rawPassword = dto.DateOfBirth.ToString("ddMMyyyy");

            // ================================
            // IMAGE UPLOAD (SAME AS TEACHER)
            // ================================
            string? studentImageName = null;

            if (dto.StudentImage != null && dto.StudentImage.Length > 0)
            {
                // Original filename
                string originalFileName = Path.GetFileName(dto.StudentImage.FileName);

                // Sanitize filename
                string safeFileName = string.Concat(
                    originalFileName.Split(Path.GetInvalidFileNameChars())
                ).Replace(" ", "_");

                string uploadFolder = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot",
                    "Content",
                    "images",
                    "Students"
                );

                if (!Directory.Exists(uploadFolder))
                {
                    Directory.CreateDirectory(uploadFolder);
                }

                string filePath = Path.Combine(uploadFolder, safeFileName);

                // Handle duplicate filename
                if (System.IO.File.Exists(filePath))
                {
                    string name = Path.GetFileNameWithoutExtension(safeFileName);
                    string ext = Path.GetExtension(safeFileName);

                    safeFileName = $"{name}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
                    filePath = Path.Combine(uploadFolder, safeFileName);
                }

                using var stream = new FileStream(filePath, FileMode.Create);
                dto.StudentImage.CopyTo(stream);

                studentImageName = safeFileName; // save only filename
            }

            using SqlConnection con = new SqlConnection(cs);
            con.Open();

            SqlTransaction? tx = null;

            try
            {
                tx = con.BeginTransaction();

                // Validate Class–Section
                using SqlCommand csCmd = new SqlCommand(@"
            SELECT 1
            FROM ClassSections
            WHERE SessionId = @SessionId
              AND ClassId = @ClassId
              AND SectionId = @SectionId", con, tx);

                csCmd.Parameters.AddWithValue("@SessionId", currentSessionId);
                csCmd.Parameters.AddWithValue("@ClassId", dto.ClassId);
                csCmd.Parameters.AddWithValue("@SectionId", dto.SectionId);

                if (csCmd.ExecuteScalar() == null)
                    throw new Exception("Selected Class and Section are not valid for the current session.");

                // Duplicate phone
                using SqlCommand checkCmd = new SqlCommand(
                    "SELECT COUNT(*) FROM Users WHERE PhoneNumber = @PhoneNumber",
                    con, tx);

                checkCmd.Parameters.AddWithValue("@PhoneNumber", dto.PhoneNumber);

                if (Convert.ToInt32(checkCmd.ExecuteScalar()) > 0)
                    throw new Exception("Phone number already exists.");

                // Insert User
                int userId;
                using SqlCommand userCmd = new SqlCommand(@"
            INSERT INTO Users
            (Name, PhoneNumber, Password, RoleId, ForceChangePassword, IsActive)
            OUTPUT INSERTED.UserId
            VALUES
            (@Name, @PhoneNumber, @Password, 3, 0, 1)", con, tx);

                userCmd.Parameters.AddWithValue("@Name", dto.Name);
                userCmd.Parameters.AddWithValue("@PhoneNumber", dto.PhoneNumber);
                userCmd.Parameters.AddWithValue("@Password", rawPassword);

                userId = Convert.ToInt32(userCmd.ExecuteScalar());

                // Insert Student
                using SqlCommand studentCmd = new SqlCommand(@"
            INSERT INTO Students
            (UserId, SessionId, ClassId, SectionId,
             AdmissionNumber, RollNumber,
             DateOfBirth, Gender, FatherName, MotherName,
             AddressLine1, AddressLine2, City, State, Pincode,
             StudentImage, IsActive)
            VALUES
            (@UserId, @SessionId, @ClassId, @SectionId,
             @AdmissionNumber, @RollNumber,
             @DateOfBirth, @Gender, @FatherName, @MotherName,
             @AddressLine1, @AddressLine2, @City, @State, @Pincode,
             @StudentImage, 1)", con, tx);

                studentCmd.Parameters.AddWithValue("@UserId", userId);
                studentCmd.Parameters.AddWithValue("@SessionId", currentSessionId);
                studentCmd.Parameters.AddWithValue("@ClassId", dto.ClassId);
                studentCmd.Parameters.AddWithValue("@SectionId", dto.SectionId);
                studentCmd.Parameters.AddWithValue("@AdmissionNumber", dto.AdmissionNumber);
                studentCmd.Parameters.AddWithValue("@RollNumber", (object?)dto.RollNumber ?? DBNull.Value);
                studentCmd.Parameters.AddWithValue("@DateOfBirth", dto.DateOfBirth);
                studentCmd.Parameters.AddWithValue("@Gender", dto.Gender);
                studentCmd.Parameters.AddWithValue("@FatherName", dto.FatherName);
                studentCmd.Parameters.AddWithValue("@MotherName", (object?)dto.MotherName ?? DBNull.Value);
                studentCmd.Parameters.AddWithValue("@AddressLine1", dto.AddressLine1);
                studentCmd.Parameters.AddWithValue("@AddressLine2", (object?)dto.AddressLine2 ?? DBNull.Value);
                studentCmd.Parameters.AddWithValue("@City", (object?)dto.City ?? DBNull.Value);
                studentCmd.Parameters.AddWithValue("@State", (object?)dto.State ?? DBNull.Value);
                studentCmd.Parameters.AddWithValue("@Pincode", (object?)dto.Pincode ?? DBNull.Value);
                studentCmd.Parameters.AddWithValue("@StudentImage", (object?)studentImageName ?? DBNull.Value);

                studentCmd.ExecuteNonQuery();

                tx.Commit();
                tx = null;

                TempData["Success"] = "Student added successfully";
                return RedirectToAction("Student", "Admin");
            }
            catch (Exception ex)
            {
                if (tx != null)
                {
                    try { tx.Rollback(); } catch { }
                }

                ModelState.AddModelError("", ex.Message);
                return View(dto);
            }
        }


        [HttpGet]
        public IActionResult Student()
        {
            List<Student> list = new();
            string cs = _configuration.GetConnectionString("DefaultConnection");

            using SqlConnection con = new SqlConnection(cs);
            using SqlCommand cmd = new SqlCommand(@"
        SELECT 
            u.UserId,
            u.Name,
            u.PhoneNumber,
            s.AdmissionNumber,
            s.RollNumber,
            c.ClassName,
            sec.SectionName,
            s.StudentImage
        FROM Users u
        INNER JOIN Students s ON u.UserId = s.UserId
        INNER JOIN Classes c ON s.ClassId = c.ClassId
        INNER JOIN Sections sec ON s.SectionId = sec.SectionId
        WHERE u.IsActive = 1
          AND s.IsActive = 1
        ORDER BY c.ClassName, sec.SectionName, u.Name
    ", con);

            con.Open();

            using SqlDataReader dr = cmd.ExecuteReader();
            while (dr.Read())
            {
                list.Add(new Student
                {
                    UserId = dr.GetInt32(dr.GetOrdinal("UserId")),
                    Name = dr.GetString(dr.GetOrdinal("Name")),
                    PhoneNumber = dr.GetString(dr.GetOrdinal("PhoneNumber")),

                    AdmissionNumber = dr.GetString(dr.GetOrdinal("AdmissionNumber")),
                    RollNumber = dr.IsDBNull(dr.GetOrdinal("RollNumber"))
                                    ? null
                                    : dr.GetValue(dr.GetOrdinal("RollNumber")).ToString(),

                    ClassName = dr.GetString(dr.GetOrdinal("ClassName")),
                    SectionName = dr.GetString(dr.GetOrdinal("SectionName")),

                    // ✅ IMAGE FETCH (THIS IS THE KEY)
                    StudentImage = dr.IsDBNull(dr.GetOrdinal("StudentImage"))
                                    ? null
                                    : dr.GetString(dr.GetOrdinal("StudentImage"))
                });
            }

            return View(list);
        }


        [HttpGet]
        public IActionResult EditStudent(int id)
        {
            string cs = _configuration.GetConnectionString("DefaultConnection");

            using SqlConnection con = new SqlConnection(cs);
            using SqlCommand cmd = new SqlCommand(@"
        SELECT 
            s.StudentId,              -- 🔥 REQUIRED
            u.UserId, u.Name, u.PhoneNumber,
            s.SessionId, ac.SessionName,
            s.ClassId, s.SectionId,
            s.AdmissionNumber, s.RollNumber,
            s.DateOfBirth, s.Gender,
            s.FatherName, s.MotherName,
            s.AddressLine1, s.AddressLine2,
            s.City, s.State, s.Pincode,
            s.StudentImage
        FROM Users u
        INNER JOIN Students s ON u.UserId = s.UserId
        INNER JOIN AcademicSessions ac ON s.SessionId = ac.SessionId
        WHERE u.UserId = @Id AND u.IsActive = 1 AND s.IsActive = 1
    ", con);

            cmd.Parameters.AddWithValue("@Id", id);
            con.Open();

            using SqlDataReader dr = cmd.ExecuteReader();
            if (!dr.Read()) return NotFound();

            return View(new EditStudent
            {
                StudentId = dr.GetInt32(0),
                UserId = dr.GetInt32(1),

                Name = dr.GetString(2),
                PhoneNumber = dr.GetString(3),

                SessionId = dr.GetInt32(4),
                SessionName = dr.GetString(5),

                ClassId = dr.GetInt32(6),
                SectionId = dr.GetInt32(7),

                AdmissionNumber = dr.GetString(8),
                RollNumber = dr.IsDBNull(9) ? null : dr[9].ToString(),   // ✅ FIX

                DateOfBirth = dr.GetDateTime(10),
                Gender = dr.IsDBNull(11) ? null : dr.GetString(11),

                FatherName = dr.GetString(12),
                MotherName = dr.IsDBNull(13) ? null : dr.GetString(13),

                AddressLine1 = dr.IsDBNull(14) ? null : dr.GetString(14),
                AddressLine2 = dr.IsDBNull(15) ? null : dr.GetString(15),
                City = dr.IsDBNull(16) ? null : dr.GetString(16),
                State = dr.IsDBNull(17) ? null : dr.GetString(17),
                Pincode = dr.IsDBNull(18) ? null : dr.GetString(18),

                ExistingStudentImage = dr.IsDBNull(19) ? null : dr.GetString(19)
            });



        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditStudent(EditStudent dto)
        {
            if (dto.StudentId <= 0 || dto.UserId <= 0)
            {
                TempData["Error"] = "Invalid student record";
                return RedirectToAction("Student");
            }

            string cs = _configuration.GetConnectionString("DefaultConnection");

            string? finalImageName = dto.ExistingStudentImage;

            // ============================
            // IMAGE UPDATE LOGIC (SAME AS TEACHER)
            // ============================
            if (dto.StudentImageFile != null && dto.StudentImageFile.Length > 0)
            {
                string uploadFolder = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot",
                    "images",
                    "StudentImage"
                );

                if (!Directory.Exists(uploadFolder))
                    Directory.CreateDirectory(uploadFolder);

                // delete OLD image only if NEW image uploaded
                if (!string.IsNullOrEmpty(dto.ExistingStudentImage))
                {
                    string oldPath = Path.Combine(uploadFolder, dto.ExistingStudentImage);
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                // keep original name safely (teacher-style)
                string originalName = Path.GetFileName(dto.StudentImageFile.FileName);
                string safeName = originalName.Replace(" ", "_");

                string newPath = Path.Combine(uploadFolder, safeName);

                // avoid overwrite
                if (System.IO.File.Exists(newPath))
                {
                    string name = Path.GetFileNameWithoutExtension(safeName);
                    string ext = Path.GetExtension(safeName);
                    safeName = $"{name}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
                    newPath = Path.Combine(uploadFolder, safeName);
                }

                using var stream = new FileStream(newPath, FileMode.Create);
                dto.StudentImageFile.CopyTo(stream);

                finalImageName = safeName;
            }

            using SqlConnection con = new SqlConnection(cs);
            con.Open();
            using SqlTransaction tran = con.BeginTransaction();

            try
            {
                // ============================
                // UPDATE USERS
                // ============================
                using SqlCommand cmd1 = new SqlCommand(@"
            UPDATE Users
            SET Name = @Name,
                PhoneNumber = @Phone
            WHERE UserId = @UserId
        ", con, tran);

                cmd1.Parameters.AddWithValue("@Name", dto.Name);
                cmd1.Parameters.AddWithValue("@Phone", dto.PhoneNumber);
                cmd1.Parameters.AddWithValue("@UserId", dto.UserId);
                cmd1.ExecuteNonQuery();

                // ============================
                // UPDATE STUDENTS (IMAGE INCLUDED)
                // ============================
                using SqlCommand cmd2 = new SqlCommand(@"
            UPDATE Students
            SET ClassId      = @ClassId,
                SectionId    = @SectionId,
                RollNumber   = @RollNumber,
                DateOfBirth  = @DateOfBirth,
                Gender       = @Gender,
                FatherName   = @FatherName,
                MotherName   = @MotherName,
                AddressLine1 = @AddressLine1,
                AddressLine2 = @AddressLine2,
                City         = @City,
                State        = @State,
                Pincode      = @Pincode,
                StudentImage = @StudentImage
            WHERE StudentId = @StudentId
        ", con, tran);

                // RollNumber INT-safe (teacher-level safety)
                object rollNumberValue;
                if (int.TryParse(dto.RollNumber, out int rn))
                    rollNumberValue = rn;
                else
                    rollNumberValue = DBNull.Value;

                cmd2.Parameters.AddWithValue("@ClassId", dto.ClassId);
                cmd2.Parameters.AddWithValue("@SectionId", dto.SectionId);
                cmd2.Parameters.AddWithValue("@RollNumber", rollNumberValue);
                cmd2.Parameters.AddWithValue("@DateOfBirth", dto.DateOfBirth);
                cmd2.Parameters.AddWithValue("@Gender", (object?)dto.Gender ?? DBNull.Value);
                cmd2.Parameters.AddWithValue("@FatherName", dto.FatherName);
                cmd2.Parameters.AddWithValue("@MotherName", (object?)dto.MotherName ?? DBNull.Value);
                cmd2.Parameters.AddWithValue("@AddressLine1", (object?)dto.AddressLine1 ?? DBNull.Value);
                cmd2.Parameters.AddWithValue("@AddressLine2", (object?)dto.AddressLine2 ?? DBNull.Value);
                cmd2.Parameters.AddWithValue("@City", (object?)dto.City ?? DBNull.Value);
                cmd2.Parameters.AddWithValue("@State", (object?)dto.State ?? DBNull.Value);
                cmd2.Parameters.AddWithValue("@Pincode", (object?)dto.Pincode ?? DBNull.Value);
                cmd2.Parameters.AddWithValue("@StudentImage", (object?)finalImageName ?? DBNull.Value);
                cmd2.Parameters.AddWithValue("@StudentId", dto.StudentId);

                cmd2.ExecuteNonQuery();

                tran.Commit();
                TempData["Success"] = "Student updated successfully";
            }
            catch
            {
                tran.Rollback();
                TempData["Error"] = "Update failed";
            }

            return RedirectToAction("Student");
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteStudent(int id)
        {
            string cs = _configuration.GetConnectionString("DefaultConnection");

            using SqlConnection con = new SqlConnection(cs);
            con.Open();
            using SqlTransaction tran = con.BeginTransaction();

            try
            {
                // 1️⃣ Get student image name
                string? imageName = null;
                using (SqlCommand imgCmd = new SqlCommand(
                    "SELECT StudentImage FROM Students WHERE UserId = @Id", con, tran))
                {
                    imgCmd.Parameters.AddWithValue("@Id", id);
                    imageName = imgCmd.ExecuteScalar()?.ToString();
                }

                // 2️⃣ Soft delete
                new SqlCommand(
                    "UPDATE Users SET IsActive = 0 WHERE UserId = @Id",
                    con, tran)
                { Parameters = { new("@Id", id) } }
                .ExecuteNonQuery();

                new SqlCommand(
                    "UPDATE Students SET IsActive = 0 WHERE UserId = @Id",
                    con, tran)
                { Parameters = { new("@Id", id) } }
                .ExecuteNonQuery();

                tran.Commit();

                // 3️⃣ Delete image AFTER commit
                if (!string.IsNullOrEmpty(imageName))
                {
                    string path = Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "wwwroot", "images", "StudentImage", imageName);

                    if (System.IO.File.Exists(path))
                        System.IO.File.Delete(path);
                }

                TempData["Success"] = "Student deleted successfully";
            }
            catch
            {
                tran.Rollback();
                TempData["Error"] = "Delete failed";
            }

            return RedirectToAction("Student");
        }



        [HttpGet]
        public IActionResult AddAccountant()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddAccountant(AddAccountant dto)
        {
            if (!ModelState.IsValid)
            {
                return View(dto);
            }

            string cs = _configuration.GetConnectionString("DefaultConnection");

            // Default password = DOB (ddMMyyyy)
            string rawPassword = dto.DateOfBirth.ToString("ddMMyyyy");

            // ================================
            // IMAGE UPLOAD
            // ================================
            string? imageFileName = null;

            if (dto.AccountantImage != null && dto.AccountantImage.Length > 0)
            {
                string originalFileName = Path.GetFileName(dto.AccountantImage.FileName);

                string safeFileName = string.Concat(
                    originalFileName.Split(Path.GetInvalidFileNameChars())
                ).Replace(" ", "_");

                string uploadFolder = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot",
                    "images",
                    "Accountantimage"
                );

                if (!Directory.Exists(uploadFolder))
                {
                    Directory.CreateDirectory(uploadFolder);
                }

                string filePath = Path.Combine(uploadFolder, safeFileName);

                // Prevent overwrite
                if (System.IO.File.Exists(filePath))
                {
                    string name = Path.GetFileNameWithoutExtension(safeFileName);
                    string ext = Path.GetExtension(safeFileName);
                    safeFileName = $"{name}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
                    filePath = Path.Combine(uploadFolder, safeFileName);
                }

                using var stream = new FileStream(filePath, FileMode.Create);
                dto.AccountantImage.CopyTo(stream);

                imageFileName = safeFileName; // store filename only
            }

            using SqlConnection con = new SqlConnection(cs);
            con.Open();

            using SqlTransaction transaction = con.BeginTransaction();

            try
            {
                // ================================
                // DUPLICATE PHONE CHECK
                // ================================
                using SqlCommand phoneCmd = new SqlCommand(
                    "SELECT COUNT(*) FROM Users WHERE PhoneNumber = @PhoneNumber",
                    con, transaction);

                phoneCmd.Parameters.AddWithValue("@PhoneNumber", dto.PhoneNumber);

                if ((int)phoneCmd.ExecuteScalar() > 0)
                {
                    transaction.Rollback();
                    ModelState.AddModelError("PhoneNumber", "Phone number already exists");
                    return View(dto);
                }

                // ================================
                // DUPLICATE EMAIL CHECK
                // ================================
                using SqlCommand emailCmd = new SqlCommand(
                    "SELECT COUNT(*) FROM Users WHERE Email = @Email",
                    con, transaction);

                emailCmd.Parameters.AddWithValue("@Email", dto.Email);

                if ((int)emailCmd.ExecuteScalar() > 0)
                {
                    transaction.Rollback();
                    ModelState.AddModelError("Email", "Email already exists");
                    return View(dto);
                }

                // ================================
                // INSERT USER
                // ================================
                string userInsertQuery = @"
INSERT INTO Users
(Name, Email, PhoneNumber, Password, RoleId, ForceChangePassword, IsActive)
OUTPUT INSERTED.UserId
VALUES
(@Name, @Email, @PhoneNumber, @Password,
 (SELECT RoleId FROM Roles WHERE RoleName = 'Accountant'),
 1, 1)";

                int userId;

                using (SqlCommand userCmd = new SqlCommand(userInsertQuery, con, transaction))
                {
                    userCmd.Parameters.AddWithValue("@Name", dto.Name);
                    userCmd.Parameters.AddWithValue("@Email", dto.Email);
                    userCmd.Parameters.AddWithValue("@PhoneNumber", dto.PhoneNumber);
                    userCmd.Parameters.AddWithValue("@Password", rawPassword);

                    userId = Convert.ToInt32(userCmd.ExecuteScalar());
                }

                // ================================
                // INSERT ACCOUNTANT
                // ================================
                string accountantInsertQuery = @"
INSERT INTO Accountants
(
    UserId, SessionId, Name, Gender, DateOfBirth,
    Qualification, ExperienceYears, Designation, JoiningDate,
    AddressLine1, AddressLine2, City, State, Pincode,
    AccountantImage,
    IsActive, CreatedDate
)
VALUES
(
    @UserId, @SessionId, @Name, @Gender, @DateOfBirth,
    @Qualification, @ExperienceYears, @Designation, @JoiningDate,
    @AddressLine1, @AddressLine2, @City, @State, @Pincode,
    @AccountantImage,
    1, GETDATE()
)";

                using SqlCommand accCmd = new SqlCommand(accountantInsertQuery, con, transaction);

                accCmd.Parameters.AddWithValue("@UserId", userId);
                accCmd.Parameters.AddWithValue("@SessionId", dto.SessionId);
                accCmd.Parameters.AddWithValue("@Name", dto.Name);
                accCmd.Parameters.AddWithValue("@Gender", dto.Gender);
                accCmd.Parameters.AddWithValue("@DateOfBirth", dto.DateOfBirth);

                accCmd.Parameters.AddWithValue("@Qualification", (object?)dto.Qualification ?? DBNull.Value);
                accCmd.Parameters.AddWithValue("@ExperienceYears", (object?)dto.ExperienceYears ?? DBNull.Value);
                accCmd.Parameters.AddWithValue("@Designation", (object?)dto.Designation ?? DBNull.Value);
                accCmd.Parameters.AddWithValue("@JoiningDate", (object?)dto.JoiningDate ?? DBNull.Value);

                accCmd.Parameters.AddWithValue("@AddressLine1", (object?)dto.AddressLine1 ?? DBNull.Value);
                accCmd.Parameters.AddWithValue("@AddressLine2", (object?)dto.AddressLine2 ?? DBNull.Value);
                accCmd.Parameters.AddWithValue("@City", (object?)dto.City ?? DBNull.Value);
                accCmd.Parameters.AddWithValue("@State", (object?)dto.State ?? DBNull.Value);
                accCmd.Parameters.AddWithValue("@Pincode", (object?)dto.Pincode ?? DBNull.Value);
                accCmd.Parameters.AddWithValue("@AccountantImage", (object?)imageFileName ?? DBNull.Value);

                accCmd.ExecuteNonQuery();

                transaction.Commit();

                TempData["Success"] = "Accountant added successfully. Default password is DOB.";
                return RedirectToAction("Accountant");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                ModelState.AddModelError("", ex.Message);
                return View(dto);
            }
        }



        [HttpGet]
        public IActionResult Accountant()
        {
            var list = new List<Accountant>();
            string cs = _configuration.GetConnectionString("DefaultConnection");

            using SqlConnection con = new SqlConnection(cs);
            using SqlCommand cmd = new SqlCommand(@"
SELECT 
    u.UserId,
    u.Name,
    u.Email,
    u.PhoneNumber,
    a.AccountantImage
FROM dbo.Users u
INNER JOIN dbo.Accountants a ON u.UserId = a.UserId
INNER JOIN dbo.Roles r ON u.RoleId = r.RoleId
WHERE r.RoleName = 'Accountant'
  AND u.IsActive = 1
  AND a.IsActive = 1
ORDER BY u.Name", con);

            con.Open();
            using SqlDataReader dr = cmd.ExecuteReader();

            while (dr.Read())
            {
                list.Add(new Accountant
                {
                    UserId = dr.GetInt32(0),
                    Name = dr.GetString(1),
                    Email = dr.IsDBNull(2) ? null : dr.GetString(2),
                    PhoneNumber = dr.IsDBNull(3) ? null : dr.GetString(3),
                    AccountantImage = dr.IsDBNull(4) ? null : dr.GetString(4)
                });
            }

            return View(list);
        }

        [HttpGet]
        public IActionResult EditAccountant(int id)
        {
            string cs = _configuration.GetConnectionString("DefaultConnection");

            using SqlConnection con = new SqlConnection(cs);
            using SqlCommand cmd = new SqlCommand(@"
SELECT 
    u.UserId, u.Name, u.Email, u.PhoneNumber,
    a.SessionId, s.SessionName,
    a.Gender, a.DateOfBirth, a.JoiningDate,
    a.Qualification, a.ExperienceYears, a.Designation,
    a.AddressLine1, a.AddressLine2, a.City, a.State, a.Pincode,
    a.AccountantImage
FROM Users u
INNER JOIN Accountants a ON u.UserId = a.UserId
INNER JOIN AcademicSessions s ON a.SessionId = s.SessionId
WHERE u.UserId = @Id
  AND u.IsActive = 1
  AND a.IsActive = 1", con);

            cmd.Parameters.AddWithValue("@Id", id);
            con.Open();

            using SqlDataReader dr = cmd.ExecuteReader();
            if (!dr.Read()) return NotFound();

            return View(new EditAccountant
            {
                UserId = dr.GetInt32(0),
                Name = dr.GetString(1),
                Email = dr.IsDBNull(2) ? null : dr.GetString(2),
                PhoneNumber = dr.GetString(3),

                SessionId = dr.GetInt32(4),
                SessionName = dr.GetString(5),

                Gender = dr.IsDBNull(6) ? null : dr.GetString(6),
                DateOfBirth = dr.GetDateTime(7),
                JoiningDate = dr.IsDBNull(8) ? null : dr.GetDateTime(8),

                Qualification = dr.IsDBNull(9) ? null : dr.GetString(9),
                ExperienceYears = dr.IsDBNull(10) ? null : dr.GetInt32(10),
                Designation = dr.IsDBNull(11) ? null : dr.GetString(11),

                AddressLine1 = dr.IsDBNull(12) ? null : dr.GetString(12),
                AddressLine2 = dr.IsDBNull(13) ? null : dr.GetString(13),
                City = dr.IsDBNull(14) ? null : dr.GetString(14),
                State = dr.IsDBNull(15) ? null : dr.GetString(15),
                Pincode = dr.IsDBNull(16) ? null : dr.GetString(16),

                // ✅ OLD IMAGE
                ExistingAccountantImage = dr.IsDBNull(17) ? null : dr.GetString(17)
            });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditAccountant(EditAccountant dto)
        {
            if (dto.UserId <= 0)
            {
                TempData["Error"] = "Invalid accountant record";
                return RedirectToAction("Accountant");
            }

            string cs = _configuration.GetConnectionString("DefaultConnection");

            string? finalImageName = dto.ExistingAccountantImage;

            // ============================
            // IMAGE UPDATE LOGIC
            // ============================
            if (dto.AccountantImage != null && dto.AccountantImage.Length > 0)
            {
                string uploadFolder = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot",
                    "images",
                    "Accountantimage"
                );

                if (!Directory.Exists(uploadFolder))
                    Directory.CreateDirectory(uploadFolder);

                // delete old image if new uploaded
                if (!string.IsNullOrEmpty(dto.ExistingAccountantImage))
                {
                    string oldPath = Path.Combine(uploadFolder, dto.ExistingAccountantImage);
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                string originalName = Path.GetFileName(dto.AccountantImage.FileName);
                string safeName = originalName.Replace(" ", "_");

                string newPath = Path.Combine(uploadFolder, safeName);

                if (System.IO.File.Exists(newPath))
                {
                    string name = Path.GetFileNameWithoutExtension(safeName);
                    string ext = Path.GetExtension(safeName);
                    safeName = $"{name}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
                    newPath = Path.Combine(uploadFolder, safeName);
                }

                using var stream = new FileStream(newPath, FileMode.Create);
                dto.AccountantImage.CopyTo(stream);

                finalImageName = safeName;
            }

            using SqlConnection con = new SqlConnection(cs);
            con.Open();
            using SqlTransaction tran = con.BeginTransaction();

            try
            {
                // ===== UPDATE USERS =====
                using SqlCommand cmd1 = new SqlCommand(@"
UPDATE Users
SET Name = @Name,
    Email = @Email,
    PhoneNumber = @Phone
WHERE UserId = @UserId", con, tran);

                cmd1.Parameters.AddWithValue("@Name", dto.Name);
                cmd1.Parameters.AddWithValue("@Email", (object?)dto.Email ?? DBNull.Value);
                cmd1.Parameters.AddWithValue("@Phone", dto.PhoneNumber);
                cmd1.Parameters.AddWithValue("@UserId", dto.UserId);
                cmd1.ExecuteNonQuery();

                // ===== UPDATE ACCOUNTANTS (IMAGE INCLUDED) =====
                using SqlCommand cmd2 = new SqlCommand(@"
UPDATE Accountants
SET Gender = @Gender,
    DateOfBirth = @DateOfBirth,
    JoiningDate = @JoiningDate,
    Qualification = @Qualification,
    ExperienceYears = @ExperienceYears,
    Designation = @Designation,
    AddressLine1 = @AddressLine1,
    AddressLine2 = @AddressLine2,
    City = @City,
    State = @State,
    Pincode = @Pincode,
    AccountantImage = @AccountantImage
WHERE UserId = @UserId", con, tran);

                cmd2.Parameters.AddWithValue("@Gender", (object?)dto.Gender ?? DBNull.Value);
                cmd2.Parameters.AddWithValue("@DateOfBirth", dto.DateOfBirth);
                cmd2.Parameters.AddWithValue("@JoiningDate", (object?)dto.JoiningDate ?? DBNull.Value);
                cmd2.Parameters.AddWithValue("@Qualification", (object?)dto.Qualification ?? DBNull.Value);
                cmd2.Parameters.AddWithValue("@ExperienceYears", (object?)dto.ExperienceYears ?? DBNull.Value);
                cmd2.Parameters.AddWithValue("@Designation", (object?)dto.Designation ?? DBNull.Value);
                cmd2.Parameters.AddWithValue("@AddressLine1", (object?)dto.AddressLine1 ?? DBNull.Value);
                cmd2.Parameters.AddWithValue("@AddressLine2", (object?)dto.AddressLine2 ?? DBNull.Value);
                cmd2.Parameters.AddWithValue("@City", (object?)dto.City ?? DBNull.Value);
                cmd2.Parameters.AddWithValue("@State", (object?)dto.State ?? DBNull.Value);
                cmd2.Parameters.AddWithValue("@Pincode", (object?)dto.Pincode ?? DBNull.Value);
                cmd2.Parameters.AddWithValue("@AccountantImage", (object?)finalImageName ?? DBNull.Value);
                cmd2.Parameters.AddWithValue("@UserId", dto.UserId);

                cmd2.ExecuteNonQuery();

                tran.Commit();
                TempData["Success"] = "Accountant updated successfully";
                return RedirectToAction("Accountant");
            }
            catch
            {
                tran.Rollback();
                TempData["Error"] = "Update failed";
                return View(dto);
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteAccountant(int id)
        {
            string cs = _configuration.GetConnectionString("DefaultConnection");

            using SqlConnection con = new SqlConnection(cs);
            con.Open();
            using SqlTransaction tran = con.BeginTransaction();

            try
            {
                new SqlCommand(
                    "UPDATE Users SET IsActive = 0 WHERE UserId = @Id",
                    con, tran)
                { Parameters = { new SqlParameter("@Id", id) } }
                .ExecuteNonQuery();

                new SqlCommand(
                    "UPDATE Accountants SET IsActive = 0 WHERE UserId = @Id",
                    con, tran)
                { Parameters = { new SqlParameter("@Id", id) } }
                .ExecuteNonQuery();

                tran.Commit();
                TempData["Success"] = "Accountant deleted successfully";
            }
            catch
            {
                tran.Rollback();
                TempData["Error"] = "Delete failed";
            }

            return RedirectToAction("Accountant");
        }


        // ============================
        // CREATE NEXT SESSION
        // ============================

        [HttpPost]
        public IActionResult CreateNextSession()
        {
            using SqlConnection con =
                new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));

            using SqlCommand cmd =
                new SqlCommand("CreateNextAcademicSession", con);

            cmd.CommandType = CommandType.StoredProcedure;

            con.Open();
            cmd.ExecuteNonQuery();

            return RedirectToAction("Index"); // reload sessions list
        }





        [HttpGet]
        public IActionResult GetClassesBySession(int sessionId)
        {
            var list = new List<object>();
            string cs = _configuration.GetConnectionString("DefaultConnection");

            using SqlConnection con = new SqlConnection(cs);
            using SqlCommand cmd = new SqlCommand(@"
        SELECT ClassId, ClassName
        FROM (
            SELECT DISTINCT
                c.ClassId,
                c.ClassName,
                CASE
                    WHEN c.ClassName = 'Nursery' THEN 1
                    WHEN c.ClassName = 'LKG' THEN 2
                    WHEN c.ClassName = 'UKG' THEN 3
                    WHEN ISNUMERIC(c.ClassName) = 1
                        THEN 100 + CAST(c.ClassName AS INT)
                    ELSE 1000
                END AS SortOrder
            FROM ClassSections cs
            INNER JOIN Classes c ON cs.ClassId = c.ClassId
            WHERE cs.SessionId = @SessionId
        ) x
        ORDER BY x.SortOrder;", con);

            cmd.Parameters.AddWithValue("@SessionId", sessionId);

            con.Open();
            using SqlDataReader dr = cmd.ExecuteReader();

            while (dr.Read())
            {
                list.Add(new
                {
                    classId = dr.GetInt32(0),
                    className = dr.GetString(1)
                });
            }

            return Json(list);
        }



        [HttpGet]
        public IActionResult GetSectionsByClass(int sessionId, int classId)
        {
            var list = new List<object>();
            string cs = _configuration.GetConnectionString("DefaultConnection");

            using SqlConnection con = new SqlConnection(cs);
            using SqlCommand cmd = new SqlCommand(@"
        SELECT s.SectionId, s.SectionName
        FROM ClassSections cs
        INNER JOIN Sections s ON cs.SectionId = s.SectionId
        WHERE cs.SessionId = @SessionId
          AND cs.ClassId = @ClassId
        ORDER BY s.SectionName", con);

            cmd.Parameters.AddWithValue("@SessionId", sessionId);
            cmd.Parameters.AddWithValue("@ClassId", classId);

            con.Open();
            using SqlDataReader dr = cmd.ExecuteReader();

            while (dr.Read())
            {
                list.Add(new
                {
                    sectionId = dr.GetInt32(0),
                    sectionName = dr.GetString(1)
                });
            }

            return Json(list);
        }



        [HttpGet]
        public IActionResult GetTeachersByCurrentSession()
        {
            var list = new List<object>();
            string cs = _configuration.GetConnectionString("DefaultConnection");

            using SqlConnection con = new SqlConnection(cs);
            con.Open();

            int sessionId = GetCurrentSessionId();

            using SqlCommand cmd = new SqlCommand(@"
        SELECT t.TeacherId, t.Name
        FROM Teachers t
        WHERE t.SessionId = @SessionId
          AND t.IsActive = 1
        ORDER BY t.Name", con);

            cmd.Parameters.AddWithValue("@SessionId", sessionId);

            using SqlDataReader dr = cmd.ExecuteReader();
            while (dr.Read())
            {
                list.Add(new
                {
                    teacherId = dr.GetInt32(0),
                    teacherName = dr.GetString(1)
                });
            }

            return Json(list);
        }







        [HttpGet]
        public IActionResult AssignClassTeacher()
        {
            return View();
        }


        [HttpPost]
        public JsonResult AssignClassTeacher([FromBody] ClassTeacherAssignment model)
        {
            string cs = _configuration.GetConnectionString("DefaultConnection");

            using SqlConnection con = new SqlConnection(cs);
            con.Open();

            using SqlTransaction tran = con.BeginTransaction();

            try
            {
                // 1️⃣ Get CURRENT academic session
                int currentSessionId;
                using (SqlCommand cmd = new SqlCommand(@"
            SELECT SessionId
            FROM AcademicSessions
            WHERE IsCurrent = 1", con, tran))
                {
                    object result = cmd.ExecuteScalar();
                    if (result == null)
                        throw new Exception("No active academic session found.");

                    currentSessionId = Convert.ToInt32(result);
                }

                // 2️⃣ Validate teacher exists IN CURRENT SESSION
                using (SqlCommand validateTeacher = new SqlCommand(@"
            SELECT 1
            FROM Teachers
            WHERE TeacherId = @TeacherId
              AND SessionId = @SessionId
              AND IsActive = 1", con, tran))
                {
                    validateTeacher.Parameters.AddWithValue("@TeacherId", model.TeacherId);
                    validateTeacher.Parameters.AddWithValue("@SessionId", currentSessionId);

                    if (validateTeacher.ExecuteScalar() == null)
                    {
                        tran.Rollback();
                        return Json(new
                        {
                            success = false,
                            message = "Selected teacher does not belong to the current academic session."
                        });
                    }
                }

                // 3️⃣ Deactivate existing ACTIVE assignment (soft replace)
                using (SqlCommand deactivate = new SqlCommand(@"
            UPDATE ClassTeacherAssignments
            SET IsActive = 0
            WHERE SessionId = @SessionId
              AND ClassId = @ClassId
              AND SectionId = @SectionId
              AND IsActive = 1", con, tran))
                {
                    deactivate.Parameters.AddWithValue("@SessionId", currentSessionId);
                    deactivate.Parameters.AddWithValue("@ClassId", model.ClassId);
                    deactivate.Parameters.AddWithValue("@SectionId", model.SectionId);
                    deactivate.ExecuteNonQuery();
                }

                // 4️⃣ Insert NEW active assignment
                using (SqlCommand insert = new SqlCommand(@"
            INSERT INTO ClassTeacherAssignments
                (SessionId, TeacherId, ClassId, SectionId, IsActive, CreatedDate)
            VALUES
                (@SessionId, @TeacherId, @ClassId, @SectionId, 1, GETDATE())", con, tran))
                {
                    insert.Parameters.AddWithValue("@SessionId", currentSessionId);
                    insert.Parameters.AddWithValue("@TeacherId", model.TeacherId);
                    insert.Parameters.AddWithValue("@ClassId", model.ClassId);
                    insert.Parameters.AddWithValue("@SectionId", model.SectionId);
                    insert.ExecuteNonQuery();
                }

                tran.Commit();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                tran.Rollback();
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult AssignedClassTeacher()
        {
            var list = new List<AssignedClassTeacher>();
            string cs = _configuration.GetConnectionString("DefaultConnection");

            using SqlConnection con = new SqlConnection(cs);
            using SqlCommand cmd = new SqlCommand(@"
        SELECT
            a.AssignmentId,
            s.SessionName,
            c.ClassName,
            sec.SectionName,
            u.Name AS TeacherName,
            a.IsActive,
            a.CreatedDate
        FROM ClassTeacherAssignments a
        INNER JOIN AcademicSessions s
            ON s.SessionId = a.SessionId
        INNER JOIN Classes c
            ON c.ClassId = a.ClassId
        INNER JOIN Sections sec
            ON sec.SectionId = a.SectionId
        INNER JOIN Teachers t
            ON t.TeacherId = a.TeacherId
        INNER JOIN Users u
            ON u.UserId = t.UserId
        WHERE s.IsCurrent = 1
          AND a.IsActive = 1
        ORDER BY c.ClassName, sec.SectionName;
    ", con);

            con.Open();
            using SqlDataReader dr = cmd.ExecuteReader();

            while (dr.Read())
            {
                list.Add(new AssignedClassTeacher
                {
                    AssignmentId = Convert.ToInt32(dr["AssignmentId"]),
                    SessionName = dr["SessionName"].ToString()!,
                    ClassName = dr["ClassName"].ToString()!,
                    SectionName = dr["SectionName"].ToString()!,
                    TeacherName = dr["TeacherName"].ToString()!,
                    IsActive = Convert.ToBoolean(dr["IsActive"]),
                    CreatedDate = Convert.ToDateTime(dr["CreatedDate"])
                });
            }

            return View(list);
        }



        [HttpGet]
        public IActionResult FeeHeads()
        {
            List<FeeSchedules> list = new();

            using SqlConnection con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            using SqlCommand cmd = new SqlCommand(@"
        SELECT 
            fh.FeeHeadId,
            fh.FeeHeadName,
            fh.IsActive,
            fh.CreatedDate,
            fs.Frequency,
            fs.MonthNumber
        FROM FeeHeads fh
        LEFT JOIN FeeSchedules fs ON fh.FeeHeadId = fs.FeeHeadId
        ORDER BY fh.FeeHeadName, fs.MonthNumber", con);

            con.Open();
            using SqlDataReader dr = cmd.ExecuteReader();

            while (dr.Read())
            {
                int feeHeadId = Convert.ToInt32(dr["FeeHeadId"]);

                var existing = list.FirstOrDefault(x => x.FeeHeadId == feeHeadId);

                if (existing == null)
                {
                    existing = new FeeSchedules
                    {
                        FeeHeadId = feeHeadId,
                        FeeHeadName = dr["FeeHeadName"].ToString(),
                        IsActive = Convert.ToBoolean(dr["IsActive"]),
                        CreatedDate = Convert.ToDateTime(dr["CreatedDate"]),
                        Schedules = new List<string>()
                    };
                    list.Add(existing);
                }

                if (dr["Frequency"] != DBNull.Value)
                {
                    string frequency = dr["Frequency"].ToString();
                    string monthText = "";

                    if (frequency == "Custom" && dr["MonthNumber"] != DBNull.Value)
                    {
                        int month = Convert.ToInt32(dr["MonthNumber"]);
                        monthText = new DateTime(2000, month, 1).ToString("MMMM");
                        existing.Schedules.Add($"{monthText}");
                    }
                    else
                    {
                        existing.Schedules.Add(frequency);
                    }
                }
            }

            // JSON for AJAX
            if (Request.Headers["Accept"].ToString().Contains("application/json"))
            {
                return Json(list);
            }

            return View(list);
        }




        [HttpGet]
        public IActionResult AddFeeHead()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddFeeHead(string feeHeadName)
        {
            if (string.IsNullOrWhiteSpace(feeHeadName))
            {
                TempData["Error"] = "Fee Head name cannot be empty.";
                return RedirectToAction("AddFeeHead");
            }

            feeHeadName = feeHeadName.Trim();

            using SqlConnection con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            con.Open();

            // 🔍 Check if Fee Head already exists
            using (SqlCommand checkCmd = new SqlCommand(
                "SELECT COUNT(*) FROM FeeHeads WHERE FeeHeadName = @FeeHeadName", con))
            {
                checkCmd.Parameters.AddWithValue("@FeeHeadName", feeHeadName);
                int exists = Convert.ToInt32(checkCmd.ExecuteScalar());

                if (exists > 0)
                {
                    TempData["Error"] = "This Fee Head already exists.";
                    return RedirectToAction("AddFeeHead");
                }
            }

            // ✅ Insert only if not exists
            using (SqlCommand cmd = new SqlCommand(
                "INSERT INTO FeeHeads (FeeHeadName) VALUES (@FeeHeadName)", con))
            {
                cmd.Parameters.AddWithValue("@FeeHeadName", feeHeadName);
                cmd.ExecuteNonQuery();
            }

            TempData["Success"] = "Fee Head added successfully.";
            return RedirectToAction("FeeHeads");
        }

        [HttpGet]
        public IActionResult EditFeeHead(int id)
        {
            FeeSchedules model = null;

            using SqlConnection con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            using SqlCommand cmd = new SqlCommand(@"
        SELECT 
            fh.FeeHeadId,
            fh.FeeHeadName,
            fh.IsActive,
            fs.Frequency,
            fs.MonthNumber
        FROM FeeHeads fh
        LEFT JOIN FeeSchedules fs ON fh.FeeHeadId = fs.FeeHeadId
        WHERE fh.FeeHeadId = @Id
        ORDER BY fs.MonthNumber", con);

            cmd.Parameters.AddWithValue("@Id", id);

            con.Open();
            using SqlDataReader dr = cmd.ExecuteReader();

            while (dr.Read())
            {
                if (model == null)
                {
                    model = new FeeSchedules
                    {
                        FeeHeadId = Convert.ToInt32(dr["FeeHeadId"]),
                        FeeHeadName = dr["FeeHeadName"].ToString(),
                        IsActive = Convert.ToBoolean(dr["IsActive"]),
                        Schedules = new List<string>()
                    };
                }

                if (dr["Frequency"] != DBNull.Value)
                {
                    if (dr["Frequency"].ToString() == "Custom" && dr["MonthNumber"] != DBNull.Value)
                    {
                        int month = Convert.ToInt32(dr["MonthNumber"]);
                        model.Schedules.Add(month.ToString());
                    }
                    else
                    {
                        model.Schedules.Add(dr["Frequency"].ToString());
                    }
                }
            }

            return View(model);
        }



        [HttpPost]
        public IActionResult EditFeeHead(int feeHeadId, string feeHeadName, string frequency, int[] monthNumbers)
        {
            using SqlConnection con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            con.Open();

            // 1️⃣ Update FeeHead name
            using (SqlCommand cmd = new SqlCommand(
                "UPDATE FeeHeads SET FeeHeadName = @Name WHERE FeeHeadId = @Id", con))
            {
                cmd.Parameters.AddWithValue("@Name", feeHeadName);
                cmd.Parameters.AddWithValue("@Id", feeHeadId);
                cmd.ExecuteNonQuery();
            }

            // 2️⃣ Remove old schedules
            using (SqlCommand cmd = new SqlCommand(
                "DELETE FROM FeeSchedules WHERE FeeHeadId = @Id", con))
            {
                cmd.Parameters.AddWithValue("@Id", feeHeadId);
                cmd.ExecuteNonQuery();
            }

            // 3️⃣ Insert new schedules
            if (frequency == "Custom")
            {
                foreach (var month in monthNumbers)
                {
                    using SqlCommand cmd = new SqlCommand(@"
                INSERT INTO FeeSchedules (FeeHeadId, Frequency, MonthNumber)
                VALUES (@FeeHeadId, 'Custom', @MonthNumber)", con);

                    cmd.Parameters.AddWithValue("@FeeHeadId", feeHeadId);
                    cmd.Parameters.AddWithValue("@MonthNumber", month);
                    cmd.ExecuteNonQuery();
                }
            }
            else
            {
                using SqlCommand cmd = new SqlCommand(@"
            INSERT INTO FeeSchedules (FeeHeadId, Frequency, MonthNumber)
            VALUES (@FeeHeadId, @Frequency, NULL)", con);

                cmd.Parameters.AddWithValue("@FeeHeadId", feeHeadId);
                cmd.Parameters.AddWithValue("@Frequency", frequency);
                cmd.ExecuteNonQuery();
            }

            TempData["Success"] = "Fee Head updated successfully.";
            return RedirectToAction("FeeHeads");
        }


        [HttpGet]
        public IActionResult DeleteFeeHead(int id)
        {
            string cs = _configuration.GetConnectionString("DefaultConnection");

            using SqlConnection con = new SqlConnection(cs);
            con.Open();

            // 1️⃣ Delete from FeeSchedules
            using (SqlCommand cmd1 = new SqlCommand(@"
        DELETE FROM FeeSchedules
        WHERE FeeHeadId = @Id
    ", con))
            {
                cmd1.Parameters.AddWithValue("@Id", id);
                cmd1.ExecuteNonQuery();
            }

            // 2️⃣ Delete from StudentFeeOverrides
            using (SqlCommand cmd2 = new SqlCommand(@"
        DELETE FROM StudentFeeOverrides
        WHERE FeeHeadId = @Id
    ", con))
            {
                cmd2.Parameters.AddWithValue("@Id", id);
                cmd2.ExecuteNonQuery();
            }

            // 3️⃣ Delete from ClassFeeStructure
            using (SqlCommand cmd3 = new SqlCommand(@"
        DELETE FROM ClassFeeStructure
        WHERE FeeHeadId = @Id
    ", con))
            {
                cmd3.Parameters.AddWithValue("@Id", id);
                cmd3.ExecuteNonQuery();
            }

            // 4️⃣ Finally delete the FeeHead itself
            using (SqlCommand cmd4 = new SqlCommand(@"
        DELETE FROM FeeHeads
        WHERE FeeHeadId = @Id
    ", con))
            {
                cmd4.Parameters.AddWithValue("@Id", id);
                cmd4.ExecuteNonQuery();
            }

            TempData["Success"] = "Fee Head deleted successfully.";
            return RedirectToAction("FeeHeads");
        }


        public IActionResult ToggleFeeHead(int id)
        {
            using SqlConnection con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            using SqlCommand cmd = new SqlCommand(
                "UPDATE FeeHeads SET IsActive = CASE WHEN IsActive = 1 THEN 0 ELSE 1 END WHERE FeeHeadId = @Id", con);

            cmd.Parameters.AddWithValue("@Id", id);

            con.Open();
            cmd.ExecuteNonQuery();

            return RedirectToAction("FeeHeads");
        }


        [HttpGet]
        public IActionResult AddFeeSchedule()
        {
            return View();
        }



        [HttpPost]
        public IActionResult AddFeeSchedule(int feeHeadId, string frequency, int[] monthNumbers)
        {
            using SqlConnection con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            con.Open();

            // ================================
            // 🔁 CUSTOM (Multiple Months)
            // ================================
            if (frequency == "Custom")
            {
                foreach (var month in monthNumbers)
                {
                    // 🔍 Prevent duplicate month entries
                    using SqlCommand checkCmd = new SqlCommand(@"
                SELECT COUNT(*) FROM FeeSchedules
                WHERE FeeHeadId = @FeeHeadId
                  AND Frequency = 'Custom'
                  AND MonthNumber = @MonthNumber", con);

                    checkCmd.Parameters.AddWithValue("@FeeHeadId", feeHeadId);
                    checkCmd.Parameters.AddWithValue("@MonthNumber", month);

                    int exists = Convert.ToInt32(checkCmd.ExecuteScalar());

                    if (exists == 0)
                    {
                        using SqlCommand insertCmd = new SqlCommand(@"
                    INSERT INTO FeeSchedules (FeeHeadId, Frequency, MonthNumber)
                    VALUES (@FeeHeadId, @Frequency, @MonthNumber)", con);

                        insertCmd.Parameters.AddWithValue("@FeeHeadId", feeHeadId);
                        insertCmd.Parameters.AddWithValue("@Frequency", frequency);
                        insertCmd.Parameters.AddWithValue("@MonthNumber", month);

                        insertCmd.ExecuteNonQuery();
                    }
                }
            }
            else
            {
                // ================================
                // 📅 MONTHLY / YEARLY
                // ================================

                // 🔍 Prevent duplicate schedule
                using SqlCommand checkCmd = new SqlCommand(@"
            SELECT COUNT(*) FROM FeeSchedules
            WHERE FeeHeadId = @FeeHeadId
              AND Frequency = @Frequency", con);

                checkCmd.Parameters.AddWithValue("@FeeHeadId", feeHeadId);
                checkCmd.Parameters.AddWithValue("@Frequency", frequency);

                int exists = Convert.ToInt32(checkCmd.ExecuteScalar());

                if (exists == 0)
                {
                    using SqlCommand insertCmd = new SqlCommand(@"
                INSERT INTO FeeSchedules (FeeHeadId, Frequency, MonthNumber)
                VALUES (@FeeHeadId, @Frequency, NULL)", con);

                    insertCmd.Parameters.AddWithValue("@FeeHeadId", feeHeadId);
                    insertCmd.Parameters.AddWithValue("@Frequency", frequency);

                    insertCmd.ExecuteNonQuery();
                }
            }

            TempData["Success"] = "Fee schedule saved successfully.";
            return RedirectToAction("FeeHeads");
        }

        //    [HttpGet]
        //    public IActionResult ClassFeeStructure(int? classId)
        //    {
        //        List<ClassFeeStructure> list = new();

        //        string cs = _configuration.GetConnectionString("DefaultConnection");

        //        using SqlConnection con = new SqlConnection(cs);
        //        using SqlCommand cmd = new SqlCommand(@"
        //    SELECT 
        //        cfs.StructureId,
        //        cfs.SessionId,
        //        s.SessionName,
        //        cfs.ClassId,
        //        c.ClassName,
        //        cfs.FeeHeadId,
        //        fh.FeeHeadName,
        //        cfs.Amount,
        //        cfs.IsActive,
        //        cfs.CreatedDate
        //    FROM ClassFeeStructure cfs
        //    INNER JOIN AcademicSessions s ON cfs.SessionId = s.SessionId
        //    INNER JOIN Classes c ON cfs.ClassId = c.ClassId
        //    INNER JOIN FeeHeads fh ON cfs.FeeHeadId = fh.FeeHeadId
        //    WHERE (@ClassId IS NULL OR cfs.ClassId = @ClassId)
        //    ORDER BY c.ClassName, fh.FeeHeadName
        //", con);

        //        cmd.Parameters.AddWithValue("@ClassId", (object?)classId ?? DBNull.Value);

        //        con.Open();
        //        using SqlDataReader dr = cmd.ExecuteReader();

        //        while (dr.Read())
        //        {
        //            list.Add(new ClassFeeStructure
        //            {
        //                StructureId = Convert.ToInt32(dr["StructureId"]),
        //                SessionId = Convert.ToInt32(dr["SessionId"]),
        //                SessionName = dr["SessionName"].ToString(),

        //                ClassId = Convert.ToInt32(dr["ClassId"]),
        //                ClassName = dr["ClassName"].ToString(),

        //                FeeHeadId = Convert.ToInt32(dr["FeeHeadId"]),
        //                FeeHeadName = dr["FeeHeadName"].ToString(),

        //                Amount = Convert.ToDecimal(dr["Amount"]),
        //                IsActive = Convert.ToBoolean(dr["IsActive"]),
        //                CreatedDate = Convert.ToDateTime(dr["CreatedDate"])
        //            });
        //        }

        //        return View(list);
        //    }



        [HttpGet]
        public IActionResult ClassFeeStructure(int? classId)
        {
            List<ClassFeeStructure> list = new();

            string cs = _configuration.GetConnectionString("DefaultConnection");

            // 🔹 Get Current Academic Session
            var session = _feeService.GetCurrentSession();

            using SqlConnection con = new SqlConnection(cs);
            using SqlCommand cmd = new SqlCommand(@"
        SELECT 
            cfs.StructureId,
            cfs.SessionId,
            s.SessionName,
            cfs.ClassId,
            c.ClassName,
            cfs.FeeHeadId,
            fh.FeeHeadName,
            cfs.Amount,
            cfs.IsActive,
            cfs.CreatedDate
        FROM ClassFeeStructure cfs
        INNER JOIN AcademicSessions s ON cfs.SessionId = s.SessionId
        INNER JOIN Classes c ON cfs.ClassId = c.ClassId
        INNER JOIN FeeHeads fh ON cfs.FeeHeadId = fh.FeeHeadId
        WHERE cfs.SessionId = @SessionId
          AND (@ClassId IS NULL OR cfs.ClassId = @ClassId)
        ORDER BY c.ClassName, fh.FeeHeadName
    ", con);

            cmd.Parameters.AddWithValue("@SessionId", session.SessionId);
            cmd.Parameters.AddWithValue("@ClassId", (object?)classId ?? DBNull.Value);

            con.Open();
            using SqlDataReader dr = cmd.ExecuteReader();

            while (dr.Read())
            {
                list.Add(new ClassFeeStructure
                {
                    StructureId = Convert.ToInt32(dr["StructureId"]),
                    SessionId = Convert.ToInt32(dr["SessionId"]),
                    SessionName = dr["SessionName"].ToString(),

                    ClassId = Convert.ToInt32(dr["ClassId"]),
                    ClassName = dr["ClassName"].ToString(),

                    FeeHeadId = Convert.ToInt32(dr["FeeHeadId"]),
                    FeeHeadName = dr["FeeHeadName"].ToString(),

                    Amount = Convert.ToDecimal(dr["Amount"]),
                    IsActive = Convert.ToBoolean(dr["IsActive"]),
                    CreatedDate = Convert.ToDateTime(dr["CreatedDate"])
                });
            }

            return View(list);
        }


        [HttpGet]
        public IActionResult EditClassFee(int id)
        {
            ClassFeeStructure model = null;

            string cs = _configuration.GetConnectionString("DefaultConnection");

            using SqlConnection con = new SqlConnection(cs);
            using SqlCommand cmd = new SqlCommand(@"
        SELECT 
            cfs.StructureId,
            cfs.SessionId,
            s.SessionName,
            cfs.ClassId,
            c.ClassName,
            cfs.FeeHeadId,
            fh.FeeHeadName,
            cfs.Amount,
            cfs.IsActive
        FROM ClassFeeStructure cfs
        INNER JOIN AcademicSessions s ON cfs.SessionId = s.SessionId
        INNER JOIN Classes c ON cfs.ClassId = c.ClassId
        INNER JOIN FeeHeads fh ON cfs.FeeHeadId = fh.FeeHeadId
        WHERE cfs.StructureId = @Id
    ", con);

            cmd.Parameters.AddWithValue("@Id", id);

            con.Open();
            using SqlDataReader dr = cmd.ExecuteReader();

            if (dr.Read())
            {
                model = new ClassFeeStructure
                {
                    StructureId = Convert.ToInt32(dr["StructureId"]),
                    SessionId = Convert.ToInt32(dr["SessionId"]),
                    SessionName = dr["SessionName"].ToString(),

                    ClassId = Convert.ToInt32(dr["ClassId"]),
                    ClassName = dr["ClassName"].ToString(),

                    FeeHeadId = Convert.ToInt32(dr["FeeHeadId"]),
                    FeeHeadName = dr["FeeHeadName"].ToString(),

                    Amount = Convert.ToDecimal(dr["Amount"]),
                    IsActive = Convert.ToBoolean(dr["IsActive"])
                };
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditClassFee(int structureId, decimal amount, bool isActive)
        {
            string cs = _configuration.GetConnectionString("DefaultConnection");

            using SqlConnection con = new SqlConnection(cs);
            using SqlCommand cmd = new SqlCommand(@"
        UPDATE ClassFeeStructure
        SET Amount = @Amount,
            IsActive = @IsActive
        WHERE StructureId = @StructureId
    ", con);

            cmd.Parameters.AddWithValue("@StructureId", structureId);
            cmd.Parameters.AddWithValue("@Amount", amount);
            cmd.Parameters.AddWithValue("@IsActive", isActive);

            con.Open();
            cmd.ExecuteNonQuery();

            TempData["Success"] = "Class fee updated successfully.";
            return RedirectToAction("ClassFeeStructure");
        }


        [HttpGet]
        public IActionResult DeleteClassFee(int id)
        {
            string cs = _configuration.GetConnectionString("DefaultConnection");

            using SqlConnection con = new SqlConnection(cs);
            using SqlCommand cmd = new SqlCommand(
                "DELETE FROM ClassFeeStructure WHERE StructureId = @Id", con);

            cmd.Parameters.AddWithValue("@Id", id);

            con.Open();
            cmd.ExecuteNonQuery();

            TempData["Success"] = "Class fee deleted successfully.";
            return RedirectToAction("ClassFeeStructure");
        }

        [HttpGet]
        public IActionResult ToggleClassFeeStatus(int id)
        {
            string cs = _configuration.GetConnectionString("DefaultConnection");
            int classId = 0;

            using SqlConnection con = new SqlConnection(cs);
            con.Open();

            // 1️⃣ First get ClassId for this StructureId
            using (SqlCommand getCmd = new SqlCommand(
                "SELECT ClassId FROM ClassFeeStructure WHERE StructureId = @Id", con))
            {
                getCmd.Parameters.AddWithValue("@Id", id);
                object result = getCmd.ExecuteScalar();

                if (result != null)
                {
                    classId = Convert.ToInt32(result);
                }
            }

            // 2️⃣ Toggle Active / Inactive
            using (SqlCommand cmd = new SqlCommand(@"
        UPDATE ClassFeeStructure
        SET IsActive = CASE WHEN IsActive = 1 THEN 0 ELSE 1 END
        WHERE StructureId = @Id
    ", con))
            {
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.ExecuteNonQuery();
            }

            TempData["Success"] = "Fee status updated successfully.";

            // 3️⃣ Redirect back to SAME class
            return RedirectToAction("ClassFeeStructure", new { classId = classId });
        }




        [HttpGet]
        public IActionResult AssignClassFee()
        { 
            return View();
        }

        [HttpPost]
        public IActionResult AssignClassFee(int sessionId, int classId, int feeHeadId, decimal amount)
        {
            using SqlConnection con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            using SqlCommand cmd = new SqlCommand(@"
                INSERT INTO ClassFeeStructure (SessionId, ClassId, FeeHeadId, Amount)
                VALUES (@SessionId, @ClassId, @FeeHeadId, @Amount)", con);

            cmd.Parameters.AddWithValue("@SessionId", sessionId);
            cmd.Parameters.AddWithValue("@ClassId", classId);
            cmd.Parameters.AddWithValue("@FeeHeadId", feeHeadId);
            cmd.Parameters.AddWithValue("@Amount", amount);

            con.Open();
            cmd.ExecuteNonQuery();

            return RedirectToAction("ClassFeeStructure");
        }

        public IActionResult DeactivateClassFee(int structureId)
        {
            using SqlConnection con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            using SqlCommand cmd = new SqlCommand(
                "UPDATE ClassFeeStructure SET IsActive = 0 WHERE StructureId = @Id", con);

            cmd.Parameters.AddWithValue("@Id", structureId);

            con.Open();
            cmd.ExecuteNonQuery();

            return RedirectToAction("ClassFeeStructure");
        }






        [HttpGet]
        public IActionResult StudentFeeDetails(int studentId)
        {
            string cs = _configuration.GetConnectionString("DefaultConnection");

            // 🔹 Always use CURRENT SESSION
            var session = _feeService.GetCurrentSession();
            int sessionId = session.SessionId;

            // ==================================================
            // ✅ VERY IMPORTANT (MISSING STEP – NOW FIXED)
            // ==================================================
            // ➜ Ensure Transport Fee exists before reading ledger
            _feeService.GenerateMonthlyTransportFee(sessionId);

            using SqlConnection con = new SqlConnection(cs);
            using SqlCommand cmd = new SqlCommand(@"
        -- =====================================
        -- 1️⃣ STUDENT BASIC INFO
        -- =====================================
        SELECT 
            s.StudentId,
            u.Name AS StudentName,
            s.AdmissionNumber,
            c.ClassName,
            sec.SectionName
        INTO #Student
        FROM Students s
        INNER JOIN Users u ON s.UserId = u.UserId
        INNER JOIN Classes c ON s.ClassId = c.ClassId
        INNER JOIN Sections sec ON s.SectionId = sec.SectionId
        WHERE s.StudentId = @StudentId;

        -- =====================================
        -- 2️⃣ MONTH-WISE LEDGER (JAN → DEC)
        -- =====================================
        SELECT 
            smf.FeeMonth,
            fh.FeeHeadName,
            smf.DueAmount,
            smf.PaidAmount,
            (smf.DueAmount - smf.PaidAmount) AS Balance
        FROM StudentMonthlyFee smf
        INNER JOIN FeeHeads fh ON smf.FeeHeadId = fh.FeeHeadId
        WHERE smf.StudentId = @StudentId
          AND smf.SessionId = @SessionId
        ORDER BY 
            CASE smf.FeeMonth
                WHEN 'January' THEN 1
                WHEN 'February' THEN 2
                WHEN 'March' THEN 3
                WHEN 'April' THEN 4
                WHEN 'May' THEN 5
                WHEN 'June' THEN 6
                WHEN 'July' THEN 7
                WHEN 'August' THEN 8
                WHEN 'September' THEN 9
                WHEN 'October' THEN 10
                WHEN 'November' THEN 11
                WHEN 'December' THEN 12
            END;

        -- =====================================
        -- 3️⃣ TOTAL OUTSTANDING (DYNAMIC)
        -- =====================================
        SELECT 
            ISNULL(SUM(DueAmount - PaidAmount), 0) AS TotalOutstanding
        FROM StudentMonthlyFee
        WHERE StudentId = @StudentId
          AND SessionId = @SessionId;

        -- =====================================
        -- 4️⃣ STUDENT INFO (FROM TEMP TABLE)
        -- =====================================
        SELECT 
            StudentName,
            AdmissionNumber,
            ClassName,
            SectionName
        FROM #Student;
    ", con);

            cmd.Parameters.AddWithValue("@StudentId", studentId);
            cmd.Parameters.AddWithValue("@SessionId", sessionId);

            con.Open();
            SqlDataReader dr = cmd.ExecuteReader();

            // ===============================
            // 🔹 MONTHLY LEDGER
            // ===============================
            List<dynamic> monthlyLedger = new();
            decimal totalOutstanding = 0;

            while (dr.Read())
            {
                monthlyLedger.Add(new
                {
                    FeeMonth = dr["FeeMonth"].ToString(),
                    FeeHeadName = dr["FeeHeadName"].ToString(),
                    DueAmount = Convert.ToDecimal(dr["DueAmount"]),
                    PaidAmount = Convert.ToDecimal(dr["PaidAmount"]),
                    Balance = Convert.ToDecimal(dr["Balance"])
                });
            }

            // ===============================
            // 🔹 TOTAL OUTSTANDING
            // ===============================
            if (dr.NextResult() && dr.Read())
            {
                totalOutstanding = Convert.ToDecimal(dr["TotalOutstanding"]);
            }

            // ===============================
            // 🔹 STUDENT INFO
            // ===============================
            string studentName = "";
            string admissionNumber = "";
            string className = "";
            string sectionName = "";

            if (dr.NextResult() && dr.Read())
            {
                studentName = dr["StudentName"].ToString();
                admissionNumber = dr["AdmissionNumber"].ToString();
                className = dr["ClassName"].ToString();
                sectionName = dr["SectionName"].ToString();
            }

            dr.Close();
            con.Close();

            // ===============================
            // 🔹 VIEW DATA
            // ===============================
            ViewBag.MonthlyLedger = monthlyLedger;
            ViewBag.TotalOutstanding = totalOutstanding;

            ViewBag.StudentName = studentName;
            ViewBag.AdmissionNumber = admissionNumber;
            ViewBag.ClassName = className;
            ViewBag.SectionName = sectionName;
            ViewBag.StudentId = studentId;
            ViewBag.SessionId = sessionId;

            return View();
        }




        [HttpPost]
        public IActionResult SearchStudentByAdmission(string admissionNumber, int sessionId)
        {
            if (string.IsNullOrEmpty(admissionNumber))
            {
                TempData["Error"] = "Please enter an admission number.";
                return RedirectToAction("StudentFeeDetails");
            }

            string cs = _configuration.GetConnectionString("DefaultConnection");

            int studentId = 0;

            using (SqlConnection con = new SqlConnection(cs))
            {
                using SqlCommand cmd = new SqlCommand(@"
            SELECT StudentId 
            FROM Students 
            WHERE AdmissionNumber = @AdmissionNumber
        ", con);

                cmd.Parameters.AddWithValue("@AdmissionNumber", admissionNumber);

                con.Open();
                object result = cmd.ExecuteScalar();

                if (result == null)
                {
                    TempData["Error"] = "No student found with this Admission Number.";
                    return RedirectToAction("StudentFeeDetails");
                }

                studentId = Convert.ToInt32(result);
            }

            // 🔁 Redirect to your existing fee details page
            return RedirectToAction("StudentFeeDetails", new { studentId = studentId, sessionId = sessionId });
        }

   


        [HttpGet]
        public IActionResult TransportSlabs()
        {
            string cs = _configuration.GetConnectionString("DefaultConnection");

            List<dynamic> list = new();

            using SqlConnection con = new SqlConnection(cs);
            using SqlCommand cmd = new SqlCommand(@"
        SELECT 
            SlabId,
            FromKM,
            ToKM,
            Amount,
            IsActive
        FROM TransportSlabs
        ORDER BY FromKM
    ", con);

            con.Open();
            SqlDataReader dr = cmd.ExecuteReader();

            while (dr.Read())
            {
                list.Add(new
                {
                    SlabId = dr["SlabId"],
                    FromKM = dr["FromKM"],
                    ToKM = dr["ToKM"],
                    Amount = dr["Amount"],
                    IsActive = dr["IsActive"]
                });
            }

            con.Close();

            ViewBag.TransportSlabs = list;
            return View();
        }



        [HttpGet]
        public IActionResult AddTransportSlab()
        {
            return View();
        }

        [HttpPost]
        public IActionResult AddTransportSlab(decimal fromKM, decimal toKM, decimal amount)
        {
            using SqlConnection con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            using SqlCommand cmd = new SqlCommand(@"
                INSERT INTO TransportSlabs (FromKM, ToKM, Amount)
                VALUES (@FromKM, @ToKM, @Amount)", con);

            cmd.Parameters.AddWithValue("@FromKM", fromKM);
            cmd.Parameters.AddWithValue("@ToKM", toKM);
            cmd.Parameters.AddWithValue("@Amount", amount);

            con.Open();
            cmd.ExecuteNonQuery();

            return RedirectToAction("TransportSlabs");
        }

        public IActionResult DeactivateTransportSlab(int slabId)
        {
            using SqlConnection con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            using SqlCommand cmd = new SqlCommand(
                "UPDATE TransportSlabs SET IsActive = 0 WHERE SlabId = @Id", con);

            cmd.Parameters.AddWithValue("@Id", slabId);

            con.Open();
            cmd.ExecuteNonQuery();

            return RedirectToAction("TransportSlabs");
        }

        [HttpGet]
        public IActionResult EditTransportSlab(int id)
        {
            string cs = _configuration.GetConnectionString("DefaultConnection");

            using SqlConnection con = new SqlConnection(cs);
            using SqlCommand cmd = new SqlCommand(@"
        SELECT SlabId, FromKM, ToKM, Amount
        FROM TransportSlabs
        WHERE SlabId = @SlabId
    ", con);

            cmd.Parameters.AddWithValue("@SlabId", id);

            con.Open();
            SqlDataReader dr = cmd.ExecuteReader();

            if (dr.Read())
            {
                ViewBag.SlabId = dr["SlabId"];
                ViewBag.FromKM = dr["FromKM"];
                ViewBag.ToKM = dr["ToKM"];
                ViewBag.Amount = dr["Amount"];
            }
            else
            {
                return RedirectToAction("TransportSlabs");
            }

            con.Close();
            return View();
        }

        [HttpPost]
        public IActionResult EditTransportSlab(int slabId, decimal fromKM, decimal toKM, decimal amount)
        {
            string cs = _configuration.GetConnectionString("DefaultConnection");

            using SqlConnection con = new SqlConnection(cs);
            using SqlCommand cmd = new SqlCommand(@"
        UPDATE TransportSlabs
        SET FromKM = @FromKM,
            ToKM = @ToKM,
            Amount = @Amount
        WHERE SlabId = @SlabId
    ", con);

            cmd.Parameters.AddWithValue("@SlabId", slabId);
            cmd.Parameters.AddWithValue("@FromKM", fromKM);
            cmd.Parameters.AddWithValue("@ToKM", toKM);
            cmd.Parameters.AddWithValue("@Amount", amount);

            con.Open();
            cmd.ExecuteNonQuery();

            return RedirectToAction("TransportSlabs");
        }


        [HttpGet]
        public IActionResult ToggleTransportSlabStatus(int id)
        {
            string cs = _configuration.GetConnectionString("DefaultConnection");

            using SqlConnection con = new SqlConnection(cs);
            using SqlCommand cmd = new SqlCommand(@"
        UPDATE TransportSlabs
        SET IsActive = CASE WHEN IsActive = 1 THEN 0 ELSE 1 END
        WHERE SlabId = @SlabId
    ", con);

            cmd.Parameters.AddWithValue("@SlabId", id);

            con.Open();
            cmd.ExecuteNonQuery();

            return RedirectToAction("TransportSlabs");
        }


        [HttpGet]
        public IActionResult DeleteTransportSlab(int id)
        {
            string cs = _configuration.GetConnectionString("DefaultConnection");

            using SqlConnection con = new SqlConnection(cs);
            using SqlCommand cmd = new SqlCommand(@"
        DELETE FROM TransportSlabs
        WHERE SlabId = @SlabId
    ", con);

            cmd.Parameters.AddWithValue("@SlabId", id);

            con.Open();
            cmd.ExecuteNonQuery();

            return RedirectToAction("TransportSlabs");
        }
        [HttpGet]
        public IActionResult StudentTransport(string admissionSearch = "", string assignedSearch = "")
        {
            string cs = _configuration.GetConnectionString("DefaultConnection");
            using SqlConnection con = new SqlConnection(cs);

            // =======================
            // Load searched student(s)
            // =======================
            SqlDataAdapter daStudents = new SqlDataAdapter(@"
        SELECT 
            s.StudentId,
            u.Name AS StudentName,
            s.AdmissionNumber
        FROM Students s
        INNER JOIN Users u ON s.UserId = u.UserId
        WHERE s.IsActive = 1
          AND (@Admission = '' OR s.AdmissionNumber LIKE '%' + @Admission + '%')
    ", con);

            daStudents.SelectCommand.Parameters.AddWithValue("@Admission", admissionSearch ?? "");

            DataTable dtStudents = new DataTable();
            daStudents.Fill(dtStudents);
            ViewBag.Students = dtStudents;
            ViewBag.AdmissionSearch = admissionSearch;

            // 👉 Auto-select student if exactly one result
            if (dtStudents.Rows.Count == 1)
            {
                string studentId = dtStudents.Rows[0]["StudentId"].ToString();
                ViewBag.SelectedStudentId = studentId;

                // 👉 Check if this student already has transport
                using SqlCommand cmdCheck = new SqlCommand(@"
            SELECT COUNT(*) 
            FROM StudentTransport 
            WHERE StudentId = @StudentId
        ", con);

                cmdCheck.Parameters.AddWithValue("@StudentId", studentId);

                con.Open();
                int exists = (int)cmdCheck.ExecuteScalar();
                con.Close();

                ViewBag.HasTransport = exists > 0;   // true = Update, false = Assign
            }
            else
            {
                ViewBag.SelectedStudentId = "";
                ViewBag.HasTransport = false;
            }

            // =======================
            // Load slabs
            // =======================
            SqlDataAdapter daSlabs = new SqlDataAdapter(@"
        SELECT SlabId, FromKM, ToKM, Amount
        FROM TransportSlabs
        WHERE IsActive = 1
    ", con);

            DataTable dtSlabs = new DataTable();
            daSlabs.Fill(dtSlabs);
            ViewBag.Slabs = dtSlabs;

            // =======================
            // Load assigned transport
            // =======================
            SqlDataAdapter daAssigned = new SqlDataAdapter(@"
        SELECT 
            st.StudentId,
            u.Name AS StudentName,
            s.AdmissionNumber,
            st.DistanceKM,
            ts.FromKM,
            ts.ToKM,
            ts.Amount AS MonthlyAmount,
            st.IsActive,
            st.SlabId
        FROM StudentTransport st
        INNER JOIN Students s ON st.StudentId = s.StudentId
        INNER JOIN Users u ON s.UserId = u.UserId
        INNER JOIN TransportSlabs ts ON st.SlabId = ts.SlabId
        WHERE (@AssignedSearch = '' OR s.AdmissionNumber LIKE '%' + @AssignedSearch + '%')
    ", con);

            daAssigned.SelectCommand.Parameters.AddWithValue("@AssignedSearch", assignedSearch ?? "");
            DataTable dt = new DataTable();
            daAssigned.Fill(dt);

            ViewBag.AssignedSearch = assignedSearch;
            return View(dt);
        }




        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AssignStudentTransport(int studentId, decimal distanceKM, int slabId)
        {
            string cs = _configuration.GetConnectionString("DefaultConnection");

            using SqlConnection con = new SqlConnection(cs);
            using SqlCommand cmd = new SqlCommand(@"
        -- Validate slab
        IF NOT EXISTS (SELECT 1 FROM TransportSlabs WHERE SlabId = @SlabId AND IsActive = 1)
        BEGIN
            RAISERROR('Selected transport slab is invalid or inactive.', 16, 1)
            RETURN
        END

        -- If record exists → Update & Activate
        IF EXISTS (SELECT 1 FROM StudentTransport WHERE StudentId = @StudentId)
        BEGIN
            UPDATE StudentTransport
            SET DistanceKM = @DistanceKM,
                SlabId = @SlabId,
                IsActive = 1
            WHERE StudentId = @StudentId
        END
        ELSE
        BEGIN
            -- First time opt
            INSERT INTO StudentTransport (StudentId, DistanceKM, SlabId, IsActive)
            VALUES (@StudentId, @DistanceKM, @SlabId, 1)
        END
    ", con);

            cmd.Parameters.AddWithValue("@StudentId", studentId);
            cmd.Parameters.AddWithValue("@DistanceKM", distanceKM);
            cmd.Parameters.AddWithValue("@SlabId", slabId);

            con.Open();
            cmd.ExecuteNonQuery();

            TempData["Success"] = "Transport assigned / activated successfully.";
            return RedirectToAction("StudentTransport");
        }


        [HttpPost]
        public IActionResult DiscontinueStudentTransport(int studentId)
        {
            string cs = _configuration.GetConnectionString("DefaultConnection");

            using SqlConnection con = new SqlConnection(cs);
            using SqlCommand cmd = new SqlCommand(@"
        UPDATE StudentTransport
        SET IsActive = 0
        WHERE StudentId = @StudentId
    ", con);

            cmd.Parameters.AddWithValue("@StudentId", studentId);

            con.Open();
            cmd.ExecuteNonQuery();

            TempData["Success"] = "Transport service discontinued for student.";
            return RedirectToAction("StudentTransport");
        }



        public IActionResult Subjects()
        {
            List<Subject> subjects = new List<Subject>();

            string cs = _configuration.GetConnectionString("DefaultConnection");

            using (SqlConnection con = new SqlConnection(cs))
            {
                string query = @"SELECT SubjectId, SubjectName,  IsActive
                         FROM Subjects
                         WHERE IsActive = 1";

                SqlCommand cmd = new SqlCommand(query, con);
                con.Open();

                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    subjects.Add(new Subject
                    {
                        SubjectId = Convert.ToInt32(reader["SubjectId"]),
                        SubjectName = reader["SubjectName"].ToString(),
                      
                        IsActive = Convert.ToBoolean(reader["IsActive"])
                    });
                }
            }

            return View(subjects);
        }

        [HttpGet]
        public IActionResult CreateSubject()
        {
            return View();
        }

        [HttpPost]
        public IActionResult CreateSubject(Subject subject)
        {
            if (string.IsNullOrWhiteSpace(subject.SubjectName))
            {
                ModelState.AddModelError("", "Subject name is required");
                return View(subject);
            }

            string cs = _configuration.GetConnectionString("DefaultConnection");

            using (SqlConnection con = new SqlConnection(cs))
            {
                string query = @"INSERT INTO Subjects (SubjectName, IsActive)
                         VALUES (@SubjectName, 1)";

                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@SubjectName", subject.SubjectName.Trim());
              

                con.Open();
                cmd.ExecuteNonQuery();
            }

            return RedirectToAction("Subjects");
        }


        [HttpGet]
        public IActionResult OptionalSubjects()
        {
            string cs = _configuration.GetConnectionString("DefaultConnection");
            int sessionId = _feeService.GetCurrentSession().SessionId;

            List<OptionalSubject> list = new List<OptionalSubject>();

            using (SqlConnection con = new SqlConnection(cs))
            {
                string query = @"
        SELECT 
            os.OptionalSubjectId,
            os.SubjectId,
            os.OptionalSubjectName,
            os.CreatedDate
        FROM OptionalSubjects os
        WHERE os.SessionId = @SessionId
          AND os.IsActive = 1
        ORDER BY os.OptionalSubjectName";

                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@SessionId", sessionId);

                con.Open();
                SqlDataReader r = cmd.ExecuteReader();

                while (r.Read())
                {
                    list.Add(new OptionalSubject
                    {
                        OptionalSubjectId = Convert.ToInt32(r["OptionalSubjectId"]),
                        SubjectId = Convert.ToInt32(r["SubjectId"]),
                        OptionalSubjectName = r["OptionalSubjectName"].ToString(),
                        CreatedDate = Convert.ToDateTime(r["CreatedDate"])
                    });
                }
            }

            return View(list);
        }

        private void LoadSubjectsForOptionalSubject()
        {
            string cs = _configuration.GetConnectionString("DefaultConnection");
            ViewBag.Subjects = new List<dynamic>();

            using (SqlConnection con = new SqlConnection(cs))
            {
                string query = @"
        SELECT SubjectId, SubjectName
        FROM Subjects
        WHERE IsActive = 1";

                SqlCommand cmd = new SqlCommand(query, con);
                con.Open();

                SqlDataReader r = cmd.ExecuteReader();
                while (r.Read())
                {
                    ViewBag.Subjects.Add(new
                    {
                        SubjectId = r["SubjectId"],
                        SubjectName = r["SubjectName"]
                    });
                }
            }
        }


        [HttpGet]
        public IActionResult CreateOptionalSubject()
        {
            // Load master subjects for dropdown
            LoadSubjectsForOptionalSubject();

            return View();
        }


        [HttpPost]
        public IActionResult CreateOptionalSubject(OptionalSubject model)
        {
            // 1️⃣ Validation (same pattern as CreateSubject)
            if (model.SubjectId <= 0)
            {
                ModelState.AddModelError("", "Subject is required");
                LoadSubjectsForOptionalSubject();   // required for dropdown
                return View(model);
            }

            string cs = _configuration.GetConnectionString("DefaultConnection");
            int sessionId = _feeService.GetCurrentSession().SessionId;

            using (SqlConnection con = new SqlConnection(cs))
            {
                string query = @"
        IF NOT EXISTS (
            SELECT 1 
            FROM OptionalSubjects 
            WHERE SessionId = @SessionId 
              AND SubjectId = @SubjectId
              AND IsActive = 1
        )
        BEGIN
            INSERT INTO OptionalSubjects
            (SessionId, SubjectId, OptionalSubjectName, IsActive)
            SELECT 
                @SessionId,
                s.SubjectId,
                s.SubjectName,
                1
            FROM Subjects s
            WHERE s.SubjectId = @SubjectId
        END";

                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@SessionId", sessionId);
                cmd.Parameters.AddWithValue("@SubjectId", model.SubjectId);

                con.Open();
                cmd.ExecuteNonQuery();
            }

            return RedirectToAction("OptionalSubjects");
        }

        [HttpGet]
        public IActionResult AssignOptionalSubject()
        {
            return View();
        }

        [HttpPost]
        public IActionResult AssignOptionalSubject(string admissionNumber)
        {
            if (string.IsNullOrWhiteSpace(admissionNumber))
            {
                ViewBag.Error = "Admission Number is required";
                return View();
            }

            string cs = _configuration.GetConnectionString("DefaultConnection");
            int sessionId = _feeService.GetCurrentSession().SessionId;

            using (SqlConnection con = new SqlConnection(cs))
            {
                con.Open();

                // 1️⃣ Get Student details (FIXED FINAL)
                string studentQuery = @"
        SELECT 
            s.StudentId,
            u.Name AS StudentName,
            s.AdmissionNumber,
            c.ClassName,
            sec.SectionName
        FROM Students s
        INNER JOIN Users u ON s.UserId = u.UserId
        INNER JOIN Classes c ON s.ClassId = c.ClassId
        INNER JOIN Sections sec ON s.SectionId = sec.SectionId
        WHERE s.AdmissionNumber = @AdmissionNumber
          AND s.SessionId = @SessionId
          AND s.IsActive = 1";

                SqlCommand studentCmd = new SqlCommand(studentQuery, con);
                studentCmd.Parameters.AddWithValue("@AdmissionNumber", admissionNumber);
                studentCmd.Parameters.AddWithValue("@SessionId", sessionId);

                SqlDataReader reader = studentCmd.ExecuteReader();

                if (!reader.Read())
                {
                    reader.Close();
                    ViewBag.Error = "Student not found for current session";
                    return View();
                }

                int studentId = Convert.ToInt32(reader["StudentId"]);

                ViewBag.StudentId = studentId;
                ViewBag.StudentName = reader["StudentName"].ToString();
                ViewBag.AdmissionNumber = reader["AdmissionNumber"].ToString();
                ViewBag.ClassName = reader["ClassName"].ToString();
                ViewBag.SectionName = reader["SectionName"].ToString();

                reader.Close();

                // 2️⃣ Load Optional Subjects
                string optionalSubjectQuery = @"
        SELECT OptionalSubjectId, OptionalSubjectName
        FROM OptionalSubjects
        WHERE SessionId = @SessionId
          AND IsActive = 1";

                SqlCommand optionalCmd = new SqlCommand(optionalSubjectQuery, con);
                optionalCmd.Parameters.AddWithValue("@SessionId", sessionId);

                DataTable optionalSubjects = new DataTable();
                SqlDataAdapter da = new SqlDataAdapter(optionalCmd);
                da.Fill(optionalSubjects);

                ViewBag.OptionalSubjects = optionalSubjects;

                // 3️⃣ Load already assigned optional subjects
                string assignedQuery = @"
        SELECT OptionalSubjectId
        FROM StudentOptionalSubjects
        WHERE StudentId = @StudentId
          AND SessionId = @SessionId
          AND IsActive = 1";

                SqlCommand assignedCmd = new SqlCommand(assignedQuery, con);
                assignedCmd.Parameters.AddWithValue("@StudentId", studentId);
                assignedCmd.Parameters.AddWithValue("@SessionId", sessionId);

                List<int> assignedSubjectIds = new List<int>();
                SqlDataReader assignedReader = assignedCmd.ExecuteReader();

                while (assignedReader.Read())
                {
                    assignedSubjectIds.Add(Convert.ToInt32(assignedReader["OptionalSubjectId"]));
                }

                assignedReader.Close();

                ViewBag.AssignedSubjectIds = assignedSubjectIds;
            }

            return View();
        }

        [HttpPost]
        public IActionResult SaveStudentOptionalSubjects(
    int studentId,
    List<int> selectedOptionalSubjectIds)
        {
            string cs = _configuration.GetConnectionString("DefaultConnection");
            int sessionId = _feeService.GetCurrentSession().SessionId;

            using (SqlConnection con = new SqlConnection(cs))
            {
                con.Open();
                SqlTransaction tran = con.BeginTransaction();

                try
                {
                    // 1️⃣ Deactivate existing
                    string deactivateQuery = @"
            UPDATE StudentOptionalSubjects
            SET IsActive = 0
            WHERE StudentId = @StudentId
              AND SessionId = @SessionId
              AND IsActive = 1";

                    SqlCommand deactivateCmd = new SqlCommand(deactivateQuery, con, tran);
                    deactivateCmd.Parameters.AddWithValue("@StudentId", studentId);
                    deactivateCmd.Parameters.AddWithValue("@SessionId", sessionId);
                    deactivateCmd.ExecuteNonQuery();

                    // 2️⃣ Insert new
                    if (selectedOptionalSubjectIds != null)
                    {
                        foreach (int optionalSubjectId in selectedOptionalSubjectIds)
                        {
                            string insertQuery = @"
                    INSERT INTO StudentOptionalSubjects
                    (SessionId, StudentId, OptionalSubjectId, IsActive, CreatedDate)
                    VALUES
                    (@SessionId, @StudentId, @OptionalSubjectId, 1, GETDATE())";

                            SqlCommand insertCmd = new SqlCommand(insertQuery, con, tran);
                            insertCmd.Parameters.AddWithValue("@SessionId", sessionId);
                            insertCmd.Parameters.AddWithValue("@StudentId", studentId);
                            insertCmd.Parameters.AddWithValue("@OptionalSubjectId", optionalSubjectId);
                            insertCmd.ExecuteNonQuery();
                        }
                    }

                    tran.Commit();
                    TempData["Success"] = "Optional subjects saved successfully";
                }
                catch
                {
                    tran.Rollback();
                    TempData["Error"] = "Error while saving optional subjects";
                }
            }

            return RedirectToAction("OptionalSubjectStudentLis");
        }


        [HttpGet]
        public IActionResult OptionalSubjectStudentList()
        {
            string cs = _configuration.GetConnectionString("DefaultConnection");
            int sessionId = _feeService.GetCurrentSession().SessionId;

            DataTable dt = new DataTable();

            using (SqlConnection con = new SqlConnection(cs))
            {
                string query = @"
        SELECT
            sos.StudentOptionalSubjectId,
            u.Name AS StudentName,
            s.AdmissionNumber,
            c.ClassName,
            sec.SectionName,
            os.OptionalSubjectName,
            sos.CreatedDate
        FROM StudentOptionalSubjects sos
        INNER JOIN Students s 
            ON sos.StudentId = s.StudentId
        INNER JOIN Users u 
            ON s.UserId = u.UserId
        INNER JOIN Classes c 
            ON s.ClassId = c.ClassId
        INNER JOIN Sections sec 
            ON s.SectionId = sec.SectionId
        INNER JOIN OptionalSubjects os 
            ON sos.OptionalSubjectId = os.OptionalSubjectId
        WHERE sos.SessionId = @SessionId
          AND sos.IsActive = 1
          AND s.IsActive = 1
          AND os.IsActive = 1
        ORDER BY os.OptionalSubjectName, c.ClassName, sec.SectionName, u.Name";

                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@SessionId", sessionId);

                SqlDataAdapter da = new SqlDataAdapter(cmd);
                da.Fill(dt);
            }

            ViewBag.OptionalSubjectStudentList = dt;
            return View();
        }


        [HttpGet]
        public IActionResult EditStudentOptionalSubject(int id)
        {
            string cs = _configuration.GetConnectionString("DefaultConnection");
            int sessionId = _feeService.GetCurrentSession().SessionId;

            DataTable optionalSubjects = new DataTable();

            using (SqlConnection con = new SqlConnection(cs))
            {
                con.Open();

                // 1️⃣ Get existing mapping
                string query = @"
        SELECT 
            sos.StudentOptionalSubjectId,
            sos.StudentId,
            sos.OptionalSubjectId,
            u.Name AS StudentName,
            s.AdmissionNumber
        FROM StudentOptionalSubjects sos
        INNER JOIN Students s ON sos.StudentId = s.StudentId
        INNER JOIN Users u ON s.UserId = u.UserId
        WHERE sos.StudentOptionalSubjectId = @Id
          AND sos.SessionId = @SessionId
          AND sos.IsActive = 1";

                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@SessionId", sessionId);

                SqlDataReader reader = cmd.ExecuteReader();

                if (!reader.Read())
                {
                    reader.Close();
                    return RedirectToAction("OptionalSubjectStudentList");
                }

                ViewBag.StudentOptionalSubjectId = id;
                ViewBag.StudentId = reader["StudentId"];
                ViewBag.StudentName = reader["StudentName"];
                ViewBag.AdmissionNumber = reader["AdmissionNumber"];
                ViewBag.SelectedOptionalSubjectId = reader["OptionalSubjectId"];

                reader.Close();

                // 2️⃣ Load optional subjects
                string optQuery = @"
        SELECT OptionalSubjectId, OptionalSubjectName
        FROM OptionalSubjects
        WHERE SessionId = @SessionId
          AND IsActive = 1";

                SqlCommand optCmd = new SqlCommand(optQuery, con);
                optCmd.Parameters.AddWithValue("@SessionId", sessionId);

                SqlDataAdapter da = new SqlDataAdapter(optCmd);
                da.Fill(optionalSubjects);
            }

            ViewBag.OptionalSubjects = optionalSubjects;
            return View();
        }


        [HttpPost]
        public IActionResult EditStudentOptionalSubject(
    int studentOptionalSubjectId,
    int studentId,
    int optionalSubjectId)
        {
            string cs = _configuration.GetConnectionString("DefaultConnection");
            int sessionId = _feeService.GetCurrentSession().SessionId;

            using (SqlConnection con = new SqlConnection(cs))
            {
                con.Open();
                SqlTransaction tran = con.BeginTransaction();

                try
                {
                    // Deactivate old
                    string deactivateQuery = @"
            UPDATE StudentOptionalSubjects
            SET IsActive = 0
            WHERE StudentOptionalSubjectId = @Id";

                    SqlCommand deactivateCmd = new SqlCommand(deactivateQuery, con, tran);
                    deactivateCmd.Parameters.AddWithValue("@Id", studentOptionalSubjectId);
                    deactivateCmd.ExecuteNonQuery();

                    // Insert new
                    string insertQuery = @"
            INSERT INTO StudentOptionalSubjects
            (SessionId, StudentId, OptionalSubjectId, IsActive, CreatedDate)
            VALUES
            (@SessionId, @StudentId, @OptionalSubjectId, 1, GETDATE())";

                    SqlCommand insertCmd = new SqlCommand(insertQuery, con, tran);
                    insertCmd.Parameters.AddWithValue("@SessionId", sessionId);
                    insertCmd.Parameters.AddWithValue("@StudentId", studentId);
                    insertCmd.Parameters.AddWithValue("@OptionalSubjectId", optionalSubjectId);
                    insertCmd.ExecuteNonQuery();

                    tran.Commit();
                }
                catch
                {
                    tran.Rollback();
                }
            }

            return RedirectToAction("OptionalSubjectStudentList");
        }

        [HttpGet]
        public IActionResult DeleteStudentOptionalSubject(int id)
        {
            string cs = _configuration.GetConnectionString("DefaultConnection");

            using (SqlConnection con = new SqlConnection(cs))
            {
                con.Open();

                string query = @"
        UPDATE StudentOptionalSubjects
        SET IsActive = 0
        WHERE StudentOptionalSubjectId = @Id";

                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.ExecuteNonQuery();
            }

            return RedirectToAction("OptionalSubjectStudentList");
        }


        [HttpGet]
        public IActionResult AssignSubjectsToClass()
        {
            string cs = _configuration.GetConnectionString("DefaultConnection");

            using (SqlConnection con = new SqlConnection(cs))
            {
                con.Open();

                // Load Classes
                string classQuery = @"
SELECT ClassId, ClassName
FROM Classes
ORDER BY
    CASE
        WHEN ClassName = 'Nursery' THEN 1
        WHEN ClassName = 'LKG' THEN 2
        WHEN ClassName = 'UKG' THEN 3
        WHEN ISNUMERIC(ClassName) = 1 THEN 100 + CAST(ClassName AS INT)
        ELSE 1000
    END";

                SqlDataAdapter classDa = new SqlDataAdapter(classQuery, con);
                DataTable classes = new DataTable();
                classDa.Fill(classes);
                ViewBag.Classes = classes;

                // Load Subjects
                string subjectQuery = "SELECT SubjectId, SubjectName FROM Subjects";

                SqlDataAdapter subjectDa = new SqlDataAdapter(subjectQuery, con);
                DataTable subjects = new DataTable();
                subjectDa.Fill(subjects);
                ViewBag.Subjects = subjects;
            }

            return View();
        }

        [HttpPost]
        public IActionResult AssignSubjectsToClass(int classId)
        {
            string cs = _configuration.GetConnectionString("DefaultConnection");
            int sessionId = _feeService.GetCurrentSession().SessionId;

            using (SqlConnection con = new SqlConnection(cs))
            {
                con.Open();
                    
                // Reload Classes
                SqlDataAdapter classDa =
                    new SqlDataAdapter("SELECT ClassId, ClassName FROM Classes", con);
                DataTable classes = new DataTable();
                classDa.Fill(classes);
                ViewBag.Classes = classes;

                // Reload Subjects
                SqlDataAdapter subjectDa =
                    new SqlDataAdapter("SELECT SubjectId, SubjectName FROM Subjects", con);
                DataTable subjects = new DataTable();
                subjectDa.Fill(subjects);
                ViewBag.Subjects = subjects;


                // Load assigned subjects for this class
                string assignedQuery = @"
        SELECT SubjectId
        FROM ClassSubjects
        WHERE ClassId = @ClassId
          AND SessionId = @SessionId
          AND IsActive = 1";

                SqlCommand cmd = new SqlCommand(assignedQuery, con);
                cmd.Parameters.AddWithValue("@ClassId", classId);
                cmd.Parameters.AddWithValue("@SessionId", sessionId);

                SqlDataReader reader = cmd.ExecuteReader();
                List<int> assignedSubjectIds = new List<int>();

                while (reader.Read())
                {
                    assignedSubjectIds.Add(Convert.ToInt32(reader["SubjectId"]));
                }
                reader.Close();

                ViewBag.SelectedClassId = classId;
                ViewBag.AssignedSubjectIds = assignedSubjectIds;
            }

            return View();
        }

        [HttpPost]
        public IActionResult SaveClassSubjects(int classId, List<int> subjectIds)
        {
            string cs = _configuration.GetConnectionString("DefaultConnection");
            int sessionId = _feeService.GetCurrentSession().SessionId;

            using (SqlConnection con = new SqlConnection(cs))
            {
                con.Open();
                SqlTransaction tran = con.BeginTransaction();

                try
                {
                    // Deactivate old assignments
                    string deactivateQuery = @"
            UPDATE ClassSubjects
            SET IsActive = 0
            WHERE ClassId = @ClassId
              AND SessionId = @SessionId";

                    SqlCommand deactivateCmd = new SqlCommand(deactivateQuery, con, tran);
                    deactivateCmd.Parameters.AddWithValue("@ClassId", classId);
                    deactivateCmd.Parameters.AddWithValue("@SessionId", sessionId);
                    deactivateCmd.ExecuteNonQuery();

                    // Insert new assignments
                    if (subjectIds != null)
                    {
                        foreach (int subjectId in subjectIds)
                        {
                            string insertQuery = @"
                    INSERT INTO ClassSubjects
                    (SessionId, ClassId, SubjectId, IsActive, CreatedDate)
                    VALUES
                    (@SessionId, @ClassId, @SubjectId, 1, GETDATE())";

                            SqlCommand insertCmd = new SqlCommand(insertQuery, con, tran);
                            insertCmd.Parameters.AddWithValue("@SessionId", sessionId);
                            insertCmd.Parameters.AddWithValue("@ClassId", classId);
                            insertCmd.Parameters.AddWithValue("@SubjectId", subjectId);
                            insertCmd.ExecuteNonQuery();
                        }
                    }

                    tran.Commit();
                }
                catch
                {
                    tran.Rollback();
                }
            }

            return RedirectToAction("ViewClassSubjects");
        }


        [HttpGet]
        public IActionResult ViewClassSubjects(int? classId)
        {
            string cs = _configuration.GetConnectionString("DefaultConnection");
            int sessionId = _feeService.GetCurrentSession().SessionId;

            DataTable dt = new DataTable();
            DataTable classDt = new DataTable();

            using (SqlConnection con = new SqlConnection(cs))
            {
                con.Open();

                // 🔹 Load Classes for dropdown
                //string classQuery = "SELECT ClassId, ClassName FROM Classes ORDER BY ClassName";
                string classQuery = @"
SELECT ClassId, ClassName
FROM Classes
ORDER BY
    CASE
        WHEN ClassName = 'Nursery' THEN 1
        WHEN ClassName = 'LKG' THEN 2
        WHEN ClassName = 'UKG' THEN 3
        WHEN ISNUMERIC(ClassName) = 1 THEN 100 + CAST(ClassName AS INT)
        ELSE 1000
    END";


                SqlDataAdapter classDa = new SqlDataAdapter(classQuery, con);
                classDa.Fill(classDt);

                // 🔹 Load subjects (filtered if class selected)
                string query = @"
        SELECT
            c.ClassName,
            s.SubjectName
        FROM ClassSubjects cs
        INNER JOIN Classes c ON cs.ClassId = c.ClassId
        INNER JOIN Subjects s ON cs.SubjectId = s.SubjectId
        WHERE cs.SessionId = @SessionId
          AND cs.IsActive = 1
          AND (@ClassId IS NULL OR cs.ClassId = @ClassId)
        ORDER BY c.ClassName, s.SubjectName";

                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@SessionId", sessionId);
                cmd.Parameters.AddWithValue("@ClassId", (object?)classId ?? DBNull.Value);

                SqlDataAdapter da = new SqlDataAdapter(cmd);
                da.Fill(dt);
            }

            ViewBag.ClassSubjects = dt;
            ViewBag.Classes = classDt;
            ViewBag.SelectedClassId = classId;

            return View();
        }


        [HttpGet]
        public IActionResult StudentSubjectList()
        {
            return View();
        }


        [HttpPost]
        public IActionResult StudentSubjectList(string admissionNumber)
        {
            if (string.IsNullOrWhiteSpace(admissionNumber))
            {
                ViewBag.Error = "Admission Number is required";
                return View();
            }

            string cs = _configuration.GetConnectionString("DefaultConnection");
            int sessionId = _feeService.GetCurrentSession().SessionId;

            DataTable dt = new DataTable();

            using (SqlConnection con = new SqlConnection(cs))
            {
                con.Open();

                // 🔹 Get student details
                string studentQuery = @"
 SELECT 
     s.StudentId,
     s.ClassId,
     u.Name AS StudentName,
     s.AdmissionNumber,
     c.ClassName
 FROM Students s
 INNER JOIN Users u ON s.UserId = u.UserId
 INNER JOIN Classes c ON s.ClassId = c.ClassId
 WHERE s.AdmissionNumber = @AdmissionNumber
   AND s.SessionId = @SessionId";

                SqlCommand studentCmd = new SqlCommand(studentQuery, con);
                studentCmd.Parameters.AddWithValue("@AdmissionNumber", admissionNumber);
                studentCmd.Parameters.AddWithValue("@SessionId", sessionId);

                SqlDataReader reader = studentCmd.ExecuteReader();

                if (!reader.Read())
                {
                    reader.Close();
                    ViewBag.Error = "Student not found";
                    return View();
                }

                int studentId = Convert.ToInt32(reader["StudentId"]);
                int classId = Convert.ToInt32(reader["ClassId"]);

                ViewBag.StudentName = reader["StudentName"];
                ViewBag.AdmissionNumber = reader["AdmissionNumber"];
                ViewBag.ClassName = reader["ClassName"];

                reader.Close();

                // 🔹 Load ALL subjects (mandatory + optional)
                string subjectQuery = @"
 SELECT DISTINCT SubjectName, SubjectType
 FROM
 (
     -- Mandatory subjects
     SELECT s.SubjectName, 'Mandatory' AS SubjectType
     FROM ClassSubjects cs
     INNER JOIN Subjects s ON cs.SubjectId = s.SubjectId
     WHERE cs.ClassId = @ClassId
       AND cs.SessionId = @SessionId
       AND cs.IsActive = 1

     UNION ALL

     -- Optional subjects
     SELECT os.OptionalSubjectName AS SubjectName, 'Optional' AS SubjectType
     FROM StudentOptionalSubjects sos
     INNER JOIN OptionalSubjects os 
         ON sos.OptionalSubjectId = os.OptionalSubjectId
     WHERE sos.StudentId = @StudentId
       AND sos.SessionId = @SessionId
       AND sos.IsActive = 1
 ) x
 ORDER BY SubjectType, SubjectName";

                SqlCommand subjectCmd = new SqlCommand(subjectQuery, con);
                subjectCmd.Parameters.AddWithValue("@ClassId", classId);
                subjectCmd.Parameters.AddWithValue("@StudentId", studentId);
                subjectCmd.Parameters.AddWithValue("@SessionId", sessionId);

                SqlDataAdapter da = new SqlDataAdapter(subjectCmd);
                da.Fill(dt);
            }

            ViewBag.Subjects = dt;
            return View();
        }

    }


}
