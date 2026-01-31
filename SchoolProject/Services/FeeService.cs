using Microsoft.Data.SqlClient;
using SchoolProject.Models;

namespace SchoolProject.Services
{
    public class FeeService
    {
        private readonly string _cs;

        public FeeService(IConfiguration configuration)
        {
            _cs = configuration.GetConnectionString("DefaultConnection");
        }

        // ======================================================
        // 🔹 CURRENT SESSION
        // ======================================================
        public (int SessionId, string SessionName) GetCurrentSession()
        {
            using SqlConnection con = new SqlConnection(_cs);
            using SqlCommand cmd = new SqlCommand(@"
        SELECT SessionId, SessionName
        FROM AcademicSessions
        WHERE IsCurrent = 1", con);

            con.Open();
            using SqlDataReader dr = cmd.ExecuteReader();

            if (!dr.Read())
                throw new Exception("No current academic session set.");

            return (dr.GetInt32(0), dr.GetString(1));
        }

        // ======================================================
        // 🔹 STUDENT BY ADMISSION NUMBER
        // ======================================================
        public Student GetStudentByAdmission(string admissionNumber, int sessionId)
        {
            using SqlConnection con = new SqlConnection(_cs);
            using SqlCommand cmd = new SqlCommand(@"
                SELECT 
                    s.StudentId,
                    s.UserId,
                    u.Name,
                    u.PhoneNumber,
                    s.AdmissionNumber,
                    s.RollNumber,
                    s.ClassId,
                    s.SectionId,
                    c.ClassName,
                    sec.SectionName
                FROM Students s
                INNER JOIN Users u ON s.UserId = u.UserId
                INNER JOIN Classes c ON s.ClassId = c.ClassId
                INNER JOIN Sections sec ON s.SectionId = sec.SectionId
                WHERE s.AdmissionNumber = @AdmissionNumber
                  AND s.SessionId = @SessionId", con);

            cmd.Parameters.AddWithValue("@AdmissionNumber", admissionNumber);
            cmd.Parameters.AddWithValue("@SessionId", sessionId);

            con.Open();
            using SqlDataReader dr = cmd.ExecuteReader();
            if (!dr.Read()) return null;

            return new Student
            {
                StudentId = Convert.ToInt32(dr["StudentId"]),
                UserId = Convert.ToInt32(dr["UserId"]),
                Name = dr["Name"].ToString(),
                PhoneNumber = dr["PhoneNumber"].ToString(),
                AdmissionNumber = dr["AdmissionNumber"].ToString(),
                RollNumber = dr["RollNumber"] == DBNull.Value ? null : dr["RollNumber"].ToString(),
                ClassId = Convert.ToInt32(dr["ClassId"]),
                SectionId = Convert.ToInt32(dr["SectionId"]),
                ClassName = dr["ClassName"].ToString(),
                SectionName = dr["SectionName"].ToString()
            };
        }

        // ======================================================
        // 🔹 STUDENT BY ID (FIX FOR YOUR ERROR)
        // ======================================================
        public Student GetStudentById(int studentId, int sessionId)
        {
            Student student = new Student();

            using SqlConnection con = new SqlConnection(_cs);
            using SqlCommand cmd = new SqlCommand(@"
                SELECT 
                    s.StudentId,
                    u.UserId,
                    u.Name,
                    u.PhoneNumber,
                    s.AdmissionNumber,
                    s.RollNumber,
                    c.ClassName,
                    sec.SectionName
                FROM Students s
                INNER JOIN Users u ON s.UserId = u.UserId
                INNER JOIN Classes c ON s.ClassId = c.ClassId
                INNER JOIN Sections sec ON s.SectionId = sec.SectionId
                WHERE s.StudentId = @StudentId
                  AND s.SessionId = @SessionId", con);

            cmd.Parameters.AddWithValue("@StudentId", studentId);
            cmd.Parameters.AddWithValue("@SessionId", sessionId);

            con.Open();
            using SqlDataReader dr = cmd.ExecuteReader();
            if (dr.Read())
            {
                student.StudentId = studentId;
                student.UserId = Convert.ToInt32(dr["UserId"]);
                student.Name = dr["Name"].ToString();
                student.PhoneNumber = dr["PhoneNumber"].ToString();
                student.AdmissionNumber = dr["AdmissionNumber"].ToString();
                student.RollNumber = dr["RollNumber"] == DBNull.Value ? null : dr["RollNumber"].ToString();
                student.ClassName = dr["ClassName"].ToString();
                student.SectionName = dr["SectionName"].ToString();
            }

            return student;
        }

        // ======================================================
        // 🔹 MONTHLY FEE GENERATION
        // ======================================================
        public void GenerateMonthlyFees(int sessionId, int classId)
        {
            using SqlConnection con = new SqlConnection(_cs);
            con.Open();

            // 🔹 Month name as string (April, May, etc.)
            string currentMonth = DateTime.Now.ToString("MMMM");

            using SqlCommand cmd = new SqlCommand(@"
        INSERT INTO StudentMonthlyFee
        (StudentId, SessionId, ClassId, FeeHeadId, FeeMonth, DueAmount, PaidAmount)
        SELECT 
            s.StudentId,
            @SessionId,
            s.ClassId,
            cfs.FeeHeadId,
            @FeeMonth,
            cfs.Amount,
            0
        FROM Students s
        INNER JOIN ClassFeeStructure cfs 
            ON s.ClassId = cfs.ClassId
        WHERE s.ClassId = @ClassId
          AND cfs.SessionId = @SessionId
          AND cfs.IsActive = 1

          -- 🔹 Month applicability check (CRITICAL)
          AND (
                cfs.ApplicableMonths IS NULL
                OR cfs.ApplicableMonths = 'ALL'
                OR CHARINDEX(@FeeMonth, cfs.ApplicableMonths) > 0
          )

          -- 🔹 Prevent duplicate monthly rows
          AND NOT EXISTS (
                SELECT 1
                FROM StudentMonthlyFee smf
                WHERE smf.StudentId = s.StudentId
                  AND smf.FeeHeadId = cfs.FeeHeadId
                  AND smf.SessionId = @SessionId
                  AND smf.FeeMonth = @FeeMonth
          )
    ", con);

            cmd.Parameters.AddWithValue("@SessionId", sessionId);
            cmd.Parameters.AddWithValue("@ClassId", classId);
            cmd.Parameters.AddWithValue("@FeeMonth", currentMonth);

            cmd.ExecuteNonQuery();
        }


        public void GenerateMonthlyTransportFee(int sessionId)
        {
            using SqlConnection con = new SqlConnection(_cs);
            con.Open();

            string currentMonth = DateTime.Now.ToString("MMMM"); // January, February...

            using SqlCommand cmd = new SqlCommand(@"
        INSERT INTO StudentMonthlyFee
        (
            StudentId,
            SessionId,
            ClassId,
            FeeHeadId,
            FeeMonth,
            DueAmount,
            PaidAmount,
            CreatedDate
        )
        SELECT
            st.StudentId,
            @SessionId,
            s.ClassId,
            fh.FeeHeadId,
            @FeeMonth,
            ts.Amount,      -- ✅ FIXED (REAL COLUMN)
            0,
            GETDATE()
        FROM StudentTransport st
        INNER JOIN Students s 
            ON s.StudentId = st.StudentId
           AND s.SessionId = @SessionId
        INNER JOIN TransportSlabs ts 
            ON ts.SlabId = st.SlabId
        INNER JOIN FeeHeads fh 
            ON fh.FeeHeadName = 'Transport Fee'
        WHERE st.IsActive = 1
          AND NOT EXISTS
          (
              SELECT 1
              FROM StudentMonthlyFee smf
              WHERE smf.StudentId = st.StudentId
                AND smf.SessionId = @SessionId
                AND smf.FeeMonth = @FeeMonth
                AND smf.FeeHeadId = fh.FeeHeadId
          );
    ", con);

            cmd.Parameters.AddWithValue("@SessionId", sessionId);
            cmd.Parameters.AddWithValue("@FeeMonth", currentMonth);

            cmd.ExecuteNonQuery();
        }


        // ======================================================
        // 🔥 CORE PAYMENT + CONCESSION ALLOCATION (YOUR LOGIC)
        // ======================================================
        public void AllocateWithConcession(
     SqlConnection con,
     SqlTransaction tran,
     int receiptId,
     int studentId,
     int sessionId,
     decimal concessionAmount,
     decimal paidAmount
 )
        {
            // ==================================================
            // 1️⃣ FETCH MONTH-WISE UNPAID FEES (OLD → NEW)
            // ==================================================
            using SqlCommand fetchCmd = new SqlCommand(@"
        SELECT 
            MonthlyFeeId,
            FeeHeadId,
            FeeMonth,
            DueAmount,
            PaidAmount
        FROM StudentMonthlyFee
        WHERE StudentId = @StudentId
          AND SessionId = @SessionId
          AND (DueAmount - PaidAmount) > 0
        ORDER BY 
            CASE FeeMonth
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
    ", con, tran);

            fetchCmd.Parameters.AddWithValue("@StudentId", studentId);
            fetchCmd.Parameters.AddWithValue("@SessionId", sessionId);

            var rows = new List<(int Id, int FeeHeadId, string Month, decimal Due, decimal Paid)>();

            using (SqlDataReader dr = fetchCmd.ExecuteReader())
            {
                while (dr.Read())
                {
                    rows.Add((
                        Convert.ToInt32(dr["MonthlyFeeId"]),
                        Convert.ToInt32(dr["FeeHeadId"]),
                        dr["FeeMonth"].ToString()!,
                        Convert.ToDecimal(dr["DueAmount"]),
                        Convert.ToDecimal(dr["PaidAmount"])
                    ));
                }
            }

            decimal remainingConcession = concessionAmount;
            decimal remainingPayment = paidAmount;

            // ==================================================
            // 2️⃣ APPLY CONCESSION & PAYMENT (SAFE LOGIC)
            // ==================================================
            foreach (var row in rows)
            {
                if (remainingConcession <= 0 && remainingPayment <= 0)
                    break;

                decimal outstandingBefore = row.Due - row.Paid;
                if (outstandingBefore <= 0)
                    continue;

                decimal conApplied = 0;
                decimal payApplied = 0;

                // ---- APPLY CONCESSION FIRST (LOGICAL ONLY) ----
                if (remainingConcession > 0)
                {
                    conApplied = Math.Min(outstandingBefore, remainingConcession);
                    remainingConcession -= conApplied;
                    outstandingBefore -= conApplied;
                }

                // ---- APPLY PAYMENT ----
                if (remainingPayment > 0 && outstandingBefore > 0)
                {
                    payApplied = Math.Min(outstandingBefore, remainingPayment);
                    remainingPayment -= payApplied;
                    outstandingBefore -= payApplied;
                }

                decimal totalCleared = conApplied + payApplied;
                decimal balanceAfter = outstandingBefore;

                // ==================================================
                // 3️⃣ INSERT RECEIPT DETAILS (✅ FIXED)
                // ==================================================
                using SqlCommand rdCmd = new SqlCommand(@"
            INSERT INTO FeeReceiptDetails
            (
                ReceiptId,
                FeeHeadId,
                FeeMonth,
                DueAmount,
                ConcessionAmount,
                PaidAmount,
                BalanceAmount
            )
            VALUES
            (
                @RId,
                @FH,
                @Month,
                @Due,
                @Con,
                @Paid,
                @Bal
            )
        ", con, tran);

                rdCmd.Parameters.AddWithValue("@RId", receiptId);
                rdCmd.Parameters.AddWithValue("@FH", row.FeeHeadId);
                rdCmd.Parameters.AddWithValue("@Month", row.Month);
                rdCmd.Parameters.AddWithValue("@Due", row.Due - row.Paid);   // ✅ OUTSTANDING BEFORE
                rdCmd.Parameters.AddWithValue("@Con", conApplied);
                rdCmd.Parameters.AddWithValue("@Paid", payApplied);
                rdCmd.Parameters.AddWithValue("@Bal", balanceAfter);

                rdCmd.ExecuteNonQuery();

                // ==================================================
                // 4️⃣ UPDATE LEDGER (PAID ONLY)
                // ==================================================
                using SqlCommand updCmd = new SqlCommand(@"
            UPDATE StudentMonthlyFee
            SET PaidAmount = PaidAmount + @Amt
            WHERE MonthlyFeeId = @Id
        ", con, tran);

                updCmd.Parameters.AddWithValue("@Amt", totalCleared);
                updCmd.Parameters.AddWithValue("@Id", row.Id);
                updCmd.ExecuteNonQuery();
            }
        }




        // ======================================================
        // 🔹 STUDENT MONTHLY LEDGER
        // ======================================================
        public List<StudentMonthlyFee> GetStudentMonthlyLedger(int studentId, int sessionId)
        {
            List<StudentMonthlyFee> list = new();

            using SqlConnection con = new SqlConnection(_cs);
            using SqlCommand cmd = new SqlCommand(@"
                SELECT 
                    smf.MonthlyFeeId,
                    smf.StudentId,
                    smf.SessionId,
                    smf.ClassId,
                    smf.FeeHeadId,
                    fh.FeeHeadName,
                    smf.FeeMonth,
                    smf.DueAmount,
                    smf.PaidAmount,
                    smf.CreatedDate
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
                    END
            ", con);

            cmd.Parameters.AddWithValue("@StudentId", studentId);
            cmd.Parameters.AddWithValue("@SessionId", sessionId);

            con.Open();
            using SqlDataReader dr = cmd.ExecuteReader();
            while (dr.Read())
            {
                list.Add(new StudentMonthlyFee
                {
                    MonthlyFeeId = Convert.ToInt32(dr["MonthlyFeeId"]),
                    StudentId = Convert.ToInt32(dr["StudentId"]),
                    SessionId = Convert.ToInt32(dr["SessionId"]),
                    ClassId = Convert.ToInt32(dr["ClassId"]),
                    FeeHeadId = Convert.ToInt32(dr["FeeHeadId"]),
                    FeeHeadName = dr["FeeHeadName"].ToString(),
                    FeeMonth = dr["FeeMonth"].ToString(),
                    DueAmount = Convert.ToDecimal(dr["DueAmount"]),
                    PaidAmount = Convert.ToDecimal(dr["PaidAmount"]),
                    CreatedDate = Convert.ToDateTime(dr["CreatedDate"])
                });
            }

            return list;
        }

//        public List<MonthlyFeeStatus> GetMonthlyFeeStatus(
//     int sessionId,
//     int? classId,
//     int? sectionId,
//     string admissionNumber,
//     string status
// )
//        {
//            List<MonthlyFeeStatus> list = new();

//            using SqlConnection con = new SqlConnection(_cs);
//            using SqlCommand cmd = new SqlCommand(@"
//SELECT
//    s.StudentId,
//    s.AdmissionNumber,
//    u.Name AS StudentName,
//    c.ClassName,
//    sec.SectionName,
//    smf.FeeMonth,

//    SUM(smf.DueAmount) AS TotalDue,
//    SUM(smf.PaidAmount) AS TotalPaid,
//    SUM(smf.DueAmount - smf.PaidAmount) AS Balance,

//    CASE
//        WHEN SUM(smf.DueAmount - smf.PaidAmount) = 0
//            THEN 'Paid'
//        ELSE 'Pending'
//    END AS FeeStatus,

//    MAX(fr.ReceiptId) AS ReceiptId

//FROM StudentMonthlyFee smf
//INNER JOIN Students s ON smf.StudentId = s.StudentId
//INNER JOIN Users u ON s.UserId = u.UserId
//INNER JOIN Classes c ON smf.ClassId = c.ClassId
//INNER JOIN Sections sec ON s.SectionId = sec.SectionId

//LEFT JOIN FeeReceiptDetails frd
//    ON frd.FeeMonth = smf.FeeMonth

//LEFT JOIN FeeReceipts fr
//    ON fr.ReceiptId = frd.ReceiptId
//   AND fr.StudentId = smf.StudentId
//   AND fr.SessionId = smf.SessionId

//WHERE smf.SessionId = @SessionId
//  AND (@ClassId IS NULL OR smf.ClassId = @ClassId)
//  AND (@SectionId IS NULL OR s.SectionId = @SectionId)
//  AND (@AdmissionNumber IS NULL OR s.AdmissionNumber = @AdmissionNumber)

//GROUP BY
//    s.StudentId,
//    s.AdmissionNumber,
//    u.Name,
//    c.ClassName,
//    sec.SectionName,
//    smf.FeeMonth

//HAVING
//    @Status = 'All'
//    OR (
//        @Status = 'Paid'
//        AND SUM(smf.DueAmount - smf.PaidAmount) = 0
//    )
//    OR (
//        @Status = 'Pending'
//        AND SUM(smf.DueAmount - smf.PaidAmount) > 0
//    )

//ORDER BY
//    c.ClassName,
//    sec.SectionName,
//    s.AdmissionNumber,
//    CASE smf.FeeMonth
//        WHEN 'January' THEN 1
//        WHEN 'February' THEN 2
//        WHEN 'March' THEN 3
//        WHEN 'April' THEN 4
//        WHEN 'May' THEN 5
//        WHEN 'June' THEN 6
//        WHEN 'July' THEN 7
//        WHEN 'August' THEN 8
//        WHEN 'September' THEN 9
//        WHEN 'October' THEN 10
//        WHEN 'November' THEN 11
//        WHEN 'December' THEN 12
//    END;
//", con);

//            cmd.Parameters.AddWithValue("@SessionId", sessionId);
//            cmd.Parameters.AddWithValue("@ClassId", (object?)classId ?? DBNull.Value);
//            cmd.Parameters.AddWithValue("@SectionId", (object?)sectionId ?? DBNull.Value);
//            cmd.Parameters.AddWithValue("@AdmissionNumber",
//                string.IsNullOrWhiteSpace(admissionNumber) ? DBNull.Value : admissionNumber);
//            cmd.Parameters.AddWithValue("@Status", status);

//            con.Open();

//            using SqlDataReader dr = cmd.ExecuteReader();
//            while (dr.Read())
//            {
//                list.Add(new MonthlyFeeStatus
//                {
//                    StudentId = Convert.ToInt32(dr["StudentId"]),
//                    AdmissionNumber = dr["AdmissionNumber"].ToString(),
//                    StudentName = dr["StudentName"].ToString(),
//                    ClassName = dr["ClassName"].ToString(),
//                    SectionName = dr["SectionName"].ToString(),
//                    FeeMonth = dr["FeeMonth"].ToString(),
//                    TotalDue = Convert.ToDecimal(dr["TotalDue"]),
//                    TotalPaid = Convert.ToDecimal(dr["TotalPaid"]),
//                    Balance = Convert.ToDecimal(dr["Balance"]),
//                    FeeStatus = dr["FeeStatus"].ToString(),
//                    ReceiptId = dr["ReceiptId"] == DBNull.Value
//                        ? null
//                        : Convert.ToInt32(dr["ReceiptId"])
//                });
//            }

//            return list;
//        }

        public List<MonthlyFeeStatus> GetMonthlyFeeStatus(
            int sessionId,
            int? classId,
            int? sectionId,
            string admissionNumber,
            string status
        )
        {
            List<MonthlyFeeStatus> list = new();

            using SqlConnection con = new SqlConnection(_cs);
            using SqlCommand cmd = new SqlCommand(@"
SELECT
    s.StudentId,
    s.AdmissionNumber,
    u.Name AS StudentName,
    c.ClassName,
    sec.SectionName,
    smf.FeeMonth,

    SUM(smf.DueAmount) AS TotalDue,
    SUM(smf.PaidAmount) AS TotalPaid,
    SUM(smf.DueAmount - smf.PaidAmount) AS Balance,

    CASE
        WHEN SUM(smf.DueAmount - smf.PaidAmount) = 0
            THEN 'Paid'
        ELSE 'Pending'
    END AS FeeStatus,

    MAX(fr.ReceiptId) AS ReceiptId

FROM StudentMonthlyFee smf
INNER JOIN Students s ON smf.StudentId = s.StudentId
INNER JOIN Users u ON s.UserId = u.UserId
INNER JOIN Classes c ON smf.ClassId = c.ClassId
INNER JOIN Sections sec ON s.SectionId = sec.SectionId

LEFT JOIN FeeReceiptDetails frd
    ON frd.FeeMonth = smf.FeeMonth

LEFT JOIN FeeReceipts fr
    ON fr.ReceiptId = frd.ReceiptId
   AND fr.StudentId = smf.StudentId
   AND fr.SessionId = smf.SessionId

WHERE smf.SessionId = @SessionId
  AND (@ClassId IS NULL OR smf.ClassId = @ClassId)
  AND (@SectionId IS NULL OR s.SectionId = @SectionId)
  AND (@AdmissionNumber IS NULL OR s.AdmissionNumber = @AdmissionNumber)

GROUP BY
    s.StudentId,
    s.AdmissionNumber,
    u.Name,
    c.ClassName,
    sec.SectionName,
    smf.FeeMonth

HAVING
    @Status = 'All'
    OR (
        @Status = 'Paid'
        AND SUM(smf.DueAmount - smf.PaidAmount) = 0
    )
    OR (
        @Status = 'Pending'
        AND SUM(smf.DueAmount - smf.PaidAmount) > 0
    )

ORDER BY
    c.ClassName,
    sec.SectionName,
    s.AdmissionNumber,
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
", con);

            cmd.Parameters.AddWithValue("@SessionId", sessionId);
            cmd.Parameters.AddWithValue("@ClassId", (object?)classId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SectionId", (object?)sectionId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@AdmissionNumber",
                string.IsNullOrWhiteSpace(admissionNumber) ? DBNull.Value : admissionNumber);
            cmd.Parameters.AddWithValue("@Status", status);

            con.Open();

            using SqlDataReader dr = cmd.ExecuteReader();
            while (dr.Read())
            {
                list.Add(new MonthlyFeeStatus
                {
                    StudentId = Convert.ToInt32(dr["StudentId"]),
                    AdmissionNumber = dr["AdmissionNumber"].ToString(),
                    StudentName = dr["StudentName"].ToString(),
                    ClassName = dr["ClassName"].ToString(),
                    SectionName = dr["SectionName"].ToString(),
                    FeeMonth = dr["FeeMonth"].ToString(),
                    TotalDue = Convert.ToDecimal(dr["TotalDue"]),
                    TotalPaid = Convert.ToDecimal(dr["TotalPaid"]),
                    Balance = Convert.ToDecimal(dr["Balance"]),
                    FeeStatus = dr["FeeStatus"].ToString(),
                    ReceiptId = dr["ReceiptId"] == DBNull.Value
                        ? null
                        : Convert.ToInt32(dr["ReceiptId"])
                });
            }

            return list;
        }



    }
}
