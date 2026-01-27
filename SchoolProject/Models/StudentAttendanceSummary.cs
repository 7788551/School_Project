namespace SchoolProject.Models
{
    public class StudentAttendanceSummary
    {
        public int TotalDays { get; set; }
        public int PresentDays { get; set; }
        public int AbsentDays { get; set; }

        public double AttendancePercentage
        {
            get
            {
                if (TotalDays == 0) return 0;
                return Math.Round((double)PresentDays / TotalDays * 100, 2);
            }
        }
    }
}
        