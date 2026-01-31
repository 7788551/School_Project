using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SchoolProject.Models;
using SchoolProject.Services;

namespace SchoolProject.Controllers
{
    [Authorize(Roles = "Accountant")]
    public class AccountantController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly FeeService _feeService;

        public AccountantController(IConfiguration configuration, FeeService feeService)
        {
            _configuration = configuration;
            _feeService = feeService;
        }

        [HttpGet]
        [Authorize(Roles = "Accountant")]
        public IActionResult AccountantProfileData()
        {
            int userId = Convert.ToInt32(HttpContext.Session.GetInt32("UserId"));

            AccountantProfile model = new();
            string cs = _configuration.GetConnectionString("DefaultConnection");

            using SqlConnection con = new(cs);
            using SqlCommand cmd = new(@"
        SELECT
            u.UserId,
            u.Name,
            u.Email,
            u.PhoneNumber,

            a.AccountantId,
            a.Gender,
            a.DateOfBirth,
            a.Qualification,
            a.ExperienceYears,
            a.Designation,
            a.JoiningDate,
            a.AddressLine1,
            a.AddressLine2,
            a.City,
            a.State,
            a.Pincode,
            a.AccountantImage
        FROM Users u
        LEFT JOIN Accountants a ON a.UserId = u.UserId
        WHERE u.UserId = @UserId
    ", con);

            cmd.Parameters.AddWithValue("@UserId", userId);
            con.Open();

            using SqlDataReader dr = cmd.ExecuteReader();
            if (dr.Read())
            {
                model.UserId = userId;
                model.AccountantId = dr["AccountantId"] == DBNull.Value
                    ? 0
                    : Convert.ToInt32(dr["AccountantId"]);

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

                model.ExistingAccountantImage = dr["AccountantImage"]?.ToString();
            }

            // 🔴 THIS IS THE IMPORTANT PART (IMAGE FIX)
            if (!string.IsNullOrEmpty(model.ExistingAccountantImage))
            {
                HttpContext.Session.SetString(
                    "AccountantImage",
                    model.ExistingAccountantImage
                );
            }
            else
            {
                HttpContext.Session.SetString(
                    "AccountantImage",
                    "default.png"
                );
            }

            model.IsEditMode = false;
            return View(model);
        }


        [HttpPost]
        [Authorize(Roles = "Accountant")]
        [ValidateAntiForgeryToken]
        public IActionResult AccountantProfileData(AccountantProfile model)
        {
            int? sessionUserId = HttpContext.Session.GetInt32("UserId");
            if (sessionUserId == null)
                return RedirectToAction("Login", "Account");

            int userId = sessionUserId.Value;
            int sessionId = Convert.ToInt32(HttpContext.Session.GetInt32("SessionId"));

            string fileName = model.ExistingAccountantImage;

            // IMAGE UPLOAD
            if (model.AccountantImage != null && model.AccountantImage.Length > 0)
            {
                fileName = Guid.NewGuid() + Path.GetExtension(model.AccountantImage.FileName);

                string folderPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot/images/Accountants"
                );

                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                string fullPath = Path.Combine(folderPath, fileName);
                using FileStream fs = new(fullPath, FileMode.Create);
                model.AccountantImage.CopyTo(fs);
            }

            using SqlConnection con = new(_configuration.GetConnectionString("DefaultConnection"));
            using SqlCommand cmd = new(@"
        IF EXISTS (SELECT 1 FROM Accountants WHERE UserId = @UserId)
        BEGIN
            UPDATE Accountants
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
                AccountantImage = @AccountantImage,
                UpdatedDate = GETDATE()
            WHERE UserId = @UserId
        END
        ELSE
        BEGIN
            INSERT INTO Accountants
            (
                UserId, SessionId,
                Gender, DateOfBirth, Qualification,
                ExperienceYears, Designation, JoiningDate,
                AddressLine1, AddressLine2, City, State, Pincode,
                AccountantImage, IsActive, CreatedDate
            )
            VALUES
            (
                @UserId, @SessionId,
                @Gender, @DateOfBirth, @Qualification,
                @ExperienceYears, @Designation, @JoiningDate,
                @AddressLine1, @AddressLine2, @City, @State, @Pincode,
                @AccountantImage, 1, GETDATE()
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
            cmd.Parameters.AddWithValue("@JoiningDate", (object?)model.JoiningDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@AddressLine1", (object?)model.AddressLine1 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@AddressLine2", (object?)model.AddressLine2 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@City", (object?)model.City ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@State", (object?)model.State ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Pincode", (object?)model.Pincode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@AccountantImage", (object?)fileName ?? DBNull.Value);

            con.Open();
            cmd.ExecuteNonQuery();

            HttpContext.Session.SetString("AccountantImage", fileName ?? "");

            TempData["Success"] = "Profile updated successfully";
            return RedirectToAction("AccountantDashboard");
        }


        // ======================================================
        // 📊 DASHBOARD
        // ======================================================
        public IActionResult AccountantDashboard()
        {
            return View();
        }

        // ======================================================
        // 🔍 SEARCH STUDENT
        // ======================================================
        [HttpPost]
        public IActionResult SearchStudentByAdmission(string admissionNumber)
        {
            if (string.IsNullOrWhiteSpace(admissionNumber))
            {
                TempData["Error"] = "Please enter Admission Number.";
                return RedirectToAction("CollectFee");
            }

            var session = _feeService.GetCurrentSession();
            Student student = _feeService.GetStudentByAdmission(admissionNumber, session.SessionId);

            if (student == null)
            {
                TempData["Error"] = "Student not found.";
                return RedirectToAction("CollectFee");
            }

            return RedirectToAction("CollectFee", new
            {
                studentId = student.StudentId,
                sessionId = session.SessionId,
                classId = student.ClassId
            });
        }

        // ======================================================
        // 💰 COLLECT FEE (GET)
        // ======================================================
        [HttpGet]
        public IActionResult CollectFee(int studentId, int sessionId, int classId)
        {
            // 🔹 Always resolve CURRENT session from backend
            var session = _feeService.GetCurrentSession();
            sessionId = session.SessionId;

            // ==================================================
            // ✅ STEP 1: GENERATE MONTHLY FEES (VERY IMPORTANT)
            // ==================================================
            // ➜ This ensures Tuition / Exam / etc. exist
            _feeService.GenerateMonthlyFees(sessionId, classId);

            // ➜ This ensures Transport Fee is generated monthly
            _feeService.GenerateMonthlyTransportFee(sessionId);

            // ==================================================
            // 🔹 STEP 2: FETCH STUDENT
            // ==================================================
            Student student = _feeService.GetStudentById(studentId, sessionId);

            // ==================================================
            // 🔹 STEP 3: FETCH MONTH-WISE LEDGER (ALL MONTHS)
            // ==================================================
            List<StudentMonthlyFee> ledger =
                _feeService.GetStudentMonthlyLedger(studentId, sessionId);

            // ==================================================
            // 🔹 STEP 4: CALCULATE OUTSTANDING FROM LEDGER
            // ==================================================
            decimal outstanding = ledger.Sum(x => x.Balance);
            outstanding = Math.Round(outstanding, 2, MidpointRounding.AwayFromZero);

            // ==================================================
            // 🔹 STEP 5: BUILD VIEW MODEL
            // ==================================================
            StudentFeeCollect model = new StudentFeeCollect
            {
                StudentId = studentId,
                SessionId = session.SessionId,
                SessionName = session.SessionName,
                ClassId = classId,

                StudentName = student.Name,
                AdmissionNumber = student.AdmissionNumber,
                ClassName = student.ClassName,
                SectionName = student.SectionName,

                TotalDue = outstanding,
                NetFee = outstanding,
                Outstanding = outstanding,

                MonthlyLedger = ledger
            };

            return View(model);
        }


        // ======================================================
        // 💳 COLLECT FEE (POST)
        // ======================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CollectFee(
    int studentId,
    int sessionId,
    int classId,
    decimal paidAmount,
    decimal concessionAmount,
    string paymentMode,
    string? paymentRef
)
        {
            paidAmount = Math.Round(paidAmount, 2);
            concessionAmount = Math.Round(concessionAmount, 2);

            if (paidAmount <= 0 && concessionAmount <= 0)
            {
                TempData["Error"] = "Paid amount or concession must be greater than zero.";
                return RedirectToAction("CollectFee", new { studentId, sessionId, classId });
            }

            // 🔹 Always resolve CURRENT session
            var session = _feeService.GetCurrentSession();
            sessionId = session.SessionId;

            // ==================================================
            // ✅ ENSURE MONTHLY FEES EXIST (CLASS + TRANSPORT)
            // ==================================================
            _feeService.GenerateMonthlyFees(sessionId, classId);
            _feeService.GenerateMonthlyTransportFee(sessionId);

            int userId = Convert.ToInt32(User.FindFirst("UserId")!.Value);
            string receiptNumber = GenerateReceiptNumber();
            int receiptYear = DateTime.Now.Year;
            int receiptId;

            using SqlConnection con =
                new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            con.Open();
            SqlTransaction tran = con.BeginTransaction();

            try
            {
                // ===============================
                // 1️⃣ TOTAL OUTSTANDING (ALL MONTHS)
                // ===============================
                decimal outstanding;

                using (SqlCommand balCmd = new SqlCommand(@"
            SELECT ISNULL(SUM(DueAmount - PaidAmount),0)
            FROM StudentMonthlyFee
            WHERE StudentId = @StudentId
              AND SessionId = @SessionId
              AND (DueAmount - PaidAmount) > 0
        ", con, tran))
                {
                    balCmd.Parameters.AddWithValue("@StudentId", studentId);
                    balCmd.Parameters.AddWithValue("@SessionId", sessionId);
                    outstanding = Convert.ToDecimal(balCmd.ExecuteScalar());
                }

                if (paidAmount + concessionAmount > outstanding)
                {
                    TempData["Error"] = "Paid + concession cannot exceed outstanding.";
                    return RedirectToAction("CollectFee", new { studentId, sessionId, classId });
                }

                // ===============================
                // 2️⃣ INSERT RECEIPT MASTER
                // ===============================
                using (SqlCommand cmd = new SqlCommand(@"
            INSERT INTO FeeReceipts
            (
                ReceiptNumber, ReceiptYear, SessionId, StudentId,
                TotalDue, ConcessionAmount, PaidAmount,
                BalanceAmount, AdvanceAmount,
                PaymentMode, PaymentReference, ReceivedBy
            )
            OUTPUT INSERTED.ReceiptId
            VALUES
            (
                @ReceiptNumber, @ReceiptYear, @SessionId, @StudentId,
                @TotalDue, @Concession, @PaidAmount,
                0, 0,
                @PaymentMode, @PaymentReference, @ReceivedBy
            )
        ", con, tran))
                {
                    cmd.Parameters.AddWithValue("@ReceiptNumber", receiptNumber);
                    cmd.Parameters.AddWithValue("@ReceiptYear", receiptYear);
                    cmd.Parameters.AddWithValue("@SessionId", sessionId);
                    cmd.Parameters.AddWithValue("@StudentId", studentId);
                    cmd.Parameters.AddWithValue("@TotalDue", outstanding);
                    cmd.Parameters.AddWithValue("@Concession", concessionAmount);
                    cmd.Parameters.AddWithValue("@PaidAmount", paidAmount);
                    cmd.Parameters.AddWithValue("@PaymentMode", paymentMode);
                    cmd.Parameters.AddWithValue("@PaymentReference",
                        (object?)paymentRef ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ReceivedBy", userId);

                    receiptId = (int)cmd.ExecuteScalar();
                }

                // ===============================
                // 3️⃣ CORE ALLOCATION (MONTH-WISE)
                // ===============================
                _feeService.AllocateWithConcession(
                    con,
                    tran,
                    receiptId,
                    studentId,
                    sessionId,
                    concessionAmount,
                    paidAmount
                );

                // ===============================
                // 4️⃣ UPDATE RECEIPT BALANCE
                // ===============================
                decimal receiptBalance;

                using (SqlCommand balCmd = new SqlCommand(@"
            SELECT ISNULL(SUM(BalanceAmount),0)
            FROM FeeReceiptDetails
            WHERE ReceiptId = @ReceiptId
        ", con, tran))
                {
                    balCmd.Parameters.AddWithValue("@ReceiptId", receiptId);
                    receiptBalance = Convert.ToDecimal(balCmd.ExecuteScalar());
                }

                using (SqlCommand upd = new SqlCommand(@"
            UPDATE FeeReceipts
            SET BalanceAmount = @Balance,
                AdvanceAmount = 0
            WHERE ReceiptId = @ReceiptId
        ", con, tran))
                {
                    upd.Parameters.AddWithValue("@Balance", receiptBalance);
                    upd.Parameters.AddWithValue("@ReceiptId", receiptId);
                    upd.ExecuteNonQuery();
                }

                tran.Commit();
            }
            catch (Exception ex)
            {
                tran.Rollback();
                TempData["Error"] = "Payment failed. " + ex.Message;
                return RedirectToAction("CollectFee", new { studentId, sessionId, classId });
            }

            return RedirectToAction("Receipt", new { id = receiptId });
        }


        // ======================================================
        // 🧾 RECEIPT VIEW
        // ======================================================
        [HttpGet]
        public IActionResult Receipt(int id)
        {
            string cs = _configuration.GetConnectionString("DefaultConnection");
            using SqlConnection con = new SqlConnection(cs);

            var session = _feeService.GetCurrentSession();
            ViewBag.SessionName = session.SessionName;

            con.Open();

            // ==================================================
            // 🔹 RECEIPT MASTER (WITH FATHER NAME)
            // ==================================================
            using SqlCommand cmd = new SqlCommand(@"
        SELECT 
            fr.ReceiptNumber,
            fr.ReceiptDate,
            fr.PaymentMode,
            fr.PaymentReference,
            fr.TotalDue,
            fr.ConcessionAmount,
            fr.PaidAmount,
            fr.BalanceAmount,

            u.Name AS StudentName,
            s.FatherName,                -- ✅ ADDED
            s.AdmissionNumber,
            c.ClassName,
            sec.SectionName
        FROM FeeReceipts fr
        INNER JOIN Students s ON fr.StudentId = s.StudentId
        INNER JOIN Users u ON s.UserId = u.UserId
        INNER JOIN Classes c ON s.ClassId = c.ClassId
        INNER JOIN Sections sec ON s.SectionId = sec.SectionId
        WHERE fr.ReceiptId = @Id
    ", con);

            cmd.Parameters.AddWithValue("@Id", id);

            using SqlDataReader dr = cmd.ExecuteReader();
            if (!dr.Read())
            {
                TempData["Error"] = "Receipt not found.";
                return RedirectToAction("CollectFee");
            }

            ViewBag.ReceiptNumber = dr["ReceiptNumber"];
            ViewBag.StudentName = dr["StudentName"];
            ViewBag.FatherName = dr["FatherName"];     // ✅ ADDED
            ViewBag.AdmissionNumber = dr["AdmissionNumber"];
            ViewBag.ClassName = dr["ClassName"];
            ViewBag.SectionName = dr["SectionName"];
            ViewBag.TotalDue = dr["TotalDue"];
            ViewBag.ConcessionAmount = dr["ConcessionAmount"];
            ViewBag.PaidAmount = dr["PaidAmount"];
            ViewBag.BalanceAmount = dr["BalanceAmount"];
            ViewBag.PaymentMode = dr["PaymentMode"];
            ViewBag.PaymentReference = dr["PaymentReference"];
            ViewBag.ReceiptDate = dr["ReceiptDate"];

            dr.Close();

            // ==================================================
            // 🔹 RECEIPT DETAILS (MONTH-WISE, FULL DATA)
            // ==================================================
            using SqlCommand cmd2 = new SqlCommand(@"
        SELECT 
            fh.FeeHeadName,
            frd.FeeMonth,
            frd.DueAmount,
            frd.ConcessionAmount,
            frd.PaidAmount,
            frd.BalanceAmount
        FROM FeeReceiptDetails frd
        INNER JOIN FeeHeads fh ON frd.FeeHeadId = fh.FeeHeadId
        WHERE frd.ReceiptId = @Id
        ORDER BY 
            CASE frd.FeeMonth
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
            END
    ", con);

            cmd2.Parameters.AddWithValue("@Id", id);

            List<dynamic> details = new();

            using SqlDataReader dr2 = cmd2.ExecuteReader();
            while (dr2.Read())
            {
                details.Add(new
                {
                    FeeHeadName = dr2["FeeHeadName"].ToString(),
                    FeeMonth = dr2["FeeMonth"].ToString(),
                    DueAmount = Convert.ToDecimal(dr2["DueAmount"]),
                    ConcessionAmount = Convert.ToDecimal(dr2["ConcessionAmount"]),
                    PaidAmount = Convert.ToDecimal(dr2["PaidAmount"]),
                    BalanceAmount = Convert.ToDecimal(dr2["BalanceAmount"])
                });
            }

            ViewBag.FeeDetails = details;

            return View();
        }

        // ======================================================
        // 🧾 RECEIPT NUMBER
        // ======================================================
        private string GenerateReceiptNumber()
        {
            int year = DateTime.Now.Year;

            using SqlConnection con =
                new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            con.Open();

            using SqlCommand cmd = new SqlCommand(@"
                SELECT ISNULL(MAX(
                    CAST(SUBSTRING(ReceiptNumber,
                        LEN('REC/' + CAST(@Year AS VARCHAR) + '/') + 1, 6) AS INT)
                ),0) + 1
                FROM FeeReceipts WITH (TABLOCKX)
                WHERE ReceiptYear = @Year
            ", con);

            cmd.Parameters.AddWithValue("@Year", year);
            int next = Convert.ToInt32(cmd.ExecuteScalar());

            return $"REC/{year}/{next:D6}";
        }
       [HttpGet]
public IActionResult MonthlyFeeReport(
    int? classId,
    int? sectionId,
    string admissionNumber,
    string status = "All"
)
{
    // 🔹 Always resolve CURRENT session
    var session = _feeService.GetCurrentSession();

    // 🔥 FIX 1: Treat 0 as NULL (VERY IMPORTANT)
    if (classId == 0) classId = null;
    if (sectionId == 0) sectionId = null;

    // 🔹 Normalize inputs
    admissionNumber = string.IsNullOrWhiteSpace(admissionNumber)
        ? null
        : admissionNumber.Trim();

    status = string.IsNullOrWhiteSpace(status) ? "All" : status;

    // 🔹 Fetch report data
    var data = _feeService.GetMonthlyFeeStatus(
        session.SessionId,
        classId,
        sectionId,
        admissionNumber,
        status
    );

    // 🔹 Pass filter state back to view
    ViewBag.SessionId = session.SessionId;
    ViewBag.SessionName = session.SessionName;
    ViewBag.SelectedClassId = classId;
    ViewBag.SelectedSectionId = sectionId;
    ViewBag.AdmissionNumber = admissionNumber;
    ViewBag.SelectedStatus = status;

    return View(data);
}



    }
}
