using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.SqlClient;
using SchoolProject.Services;

namespace SchoolProject.BackgroundServices
{
    public class MonthlyFeeBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public MonthlyFeeBackgroundService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Run once when app starts
            await GenerateFeesIfRequired();

            // Then check once every 24 hours
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
                await GenerateFeesIfRequired();
            }
        }

        private async Task GenerateFeesIfRequired()
        {
            using var scope = _scopeFactory.CreateScope();
            var feeService = scope.ServiceProvider.GetRequiredService<FeeService>();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            string cs = configuration.GetConnectionString("DefaultConnection");

            var session = feeService.GetCurrentSession();
            if (session.SessionId == 0)
                return;

            string currentMonth = DateTime.Now.ToString("MMMM"); // January, February, etc.

            using SqlConnection con = new SqlConnection(cs);
            con.Open();

            // 1️⃣ Check if fees already generated for this month
            using SqlCommand checkCmd = new SqlCommand(@"
                SELECT COUNT(1)
                FROM StudentMonthlyFee
                WHERE SessionId = @SessionId
                  AND FeeMonth = @FeeMonth
            ", con);

            checkCmd.Parameters.AddWithValue("@SessionId", session.SessionId);
            checkCmd.Parameters.AddWithValue("@FeeMonth", currentMonth);

            int alreadyGenerated = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
            if (alreadyGenerated > 0)
                return; // ✅ Already done

            // 2️⃣ Generate fees for ALL classes
            using SqlCommand classCmd = new SqlCommand(@"
                SELECT DISTINCT ClassId
                FROM Students
                WHERE SessionId = @SessionId
            ", con);

            classCmd.Parameters.AddWithValue("@SessionId", session.SessionId);

            using SqlDataReader dr = await classCmd.ExecuteReaderAsync();
            List<int> classIds = new();

            while (await dr.ReadAsync())
            {
                classIds.Add(dr.GetInt32(0));
            }

            dr.Close();

            foreach (int classId in classIds)
            {
                feeService.GenerateMonthlyFees(session.SessionId, classId);
            }
        }
    }
}
