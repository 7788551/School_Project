using Microsoft.Data.SqlClient;

namespace SchoolProject.Helpers
{
    public class TeacherContextHelper
    {
        private readonly IConfiguration _configuration;

        public TeacherContextHelper(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public int GetTeacherIdFromUserId(int userId)
        {
            using SqlConnection con = new SqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

            using SqlCommand cmd = new SqlCommand(@"
                SELECT TeacherId
                FROM Teachers
                WHERE UserId = @UserId", con);

            cmd.Parameters.AddWithValue("@UserId", userId);

            con.Open();
            object result = cmd.ExecuteScalar();

            if (result == null || result == DBNull.Value)
                throw new Exception("Teacher not found for this user.");

            return Convert.ToInt32(result);
        }
    }
}
