namespace SchoolProject.Models
{
    public class Teacher
    {
        public int UserId { get; set; }

        public string Name { get; set; } = null!;
        public string? Email { get; set; }

        // ✔ Phone numbers should be string
        public string PhoneNumber { get; set; } = null!;

        // ✔ Image file name stored in DB
        public string? TeacherImage { get; set; }

        // (Optional – useful later)
        public string? Designation { get; set; }
        public string? Subject { get; set; }
        public string? Gender { get; set; }
    }
}
