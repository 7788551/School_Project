namespace SchoolProject.Models
{
    public class Student
    {
        // ===== Identity / Keys =====
        public int StudentId { get; set; }
        public int UserId { get; set; }

        public int SessionId { get; set; }
        public int ClassId { get; set; }
        public int SectionId { get; set; }

        // ===== Student Info =====
        public string Name { get; set; } = null!;
        public string PhoneNumber { get; set; } = null!;
        public string AdmissionNumber { get; set; } = null!;
        public string? RollNumber { get; set; }

        // ✅ ADD THESE (VERY IMPORTANT)
        public string? FatherName { get; set; }
        public string? MotherName { get; set; }

        // ===== Display Helpers (Joined data) =====
        public string ClassName { get; set; } = null!;
        public string SectionName { get; set; } = null!;

        // ===== Image =====
        public string? StudentImage { get; set; }
    }
}
