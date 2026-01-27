namespace SchoolProject.Services
{
    public class FeeService
    {
        private readonly string _cs;

        public FeeService(IConfiguration configuration)
        {
            _cs = configuration.GetConnectionString("DefaultConnection");
        }

        public FeeSummaryVM GetStudentFeeSummary(int studentId, int sessionId, int classId)
        {
            decimal totalDue = 0;
            decimal totalPaid = 0;
            decimal transportFee = 0;

            using SqlConnection con = new SqlConnection(_cs);

            // Class Fees
            string feeQuery = @"
                SELECT Amount FROM ClassFeeStructure
                WHERE SessionId = @SessionId AND ClassId = @ClassId AND IsActive = 1

                UNION ALL

                SELECT Amount FROM StudentFeeOverrides
                WHERE SessionId = @SessionId AND StudentId = @StudentId AND IsActive = 1";

            using (SqlCommand cmd = new SqlCommand(feeQuery, con))
            {
                cmd.Parameters.AddWithValue("@SessionId", sessionId);
                cmd.Parameters.AddWithValue("@ClassId", classId);
                cmd.Parameters.AddWithValue("@StudentId", studentId);

                con.Open();
                using SqlDataReader dr = cmd.ExecuteReader();
                while (dr.Read())
                {
                    totalDue += Convert.ToDecimal(dr["Amount"]);
                }
                con.Close();
            }

            // Transport Fee
            using (SqlCommand cmd = new SqlCommand(@"
                SELECT ts.Amount
                FROM StudentTransport st
                INNER JOIN TransportSlabs ts ON st.SlabId = ts.SlabId
                WHERE st.StudentId = @StudentId AND st.IsActive = 1", con))
            {
                cmd.Parameters.AddWithValue("@StudentId", studentId);
                con.Open();
                object? transport = cmd.ExecuteScalar();
                if (transport != null)
                    transportFee = Convert.ToDecimal(transport);
                con.Close();
            }

            // Already Paid
            using (SqlCommand cmd = new SqlCommand(@"
                SELECT ISNULL(SUM(PaidAmount),0)
                FROM FeeReceipts
                WHERE StudentId = @StudentId
                  AND SessionId = @SessionId
                  AND IsCancelled = 0", con))
            {
                cmd.Parameters.AddWithValue("@StudentId", studentId);
                cmd.Parameters.AddWithValue("@SessionId", sessionId);

                con.Open();
                totalPaid = Convert.ToDecimal(cmd.ExecuteScalar());
                con.Close();
            }

            totalDue += transportFee;

            return new FeeSummaryVM
            {
                TotalDue = totalDue,
                Paid = totalPaid,
                Balance = totalDue - totalPaid
            };
        }
    }
}
