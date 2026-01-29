using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SchoolProject.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace SchoolProject.Controllers
{
    [Authorize]
    public class MentorController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;

        public MentorController(IConfiguration configuration, IWebHostEnvironment env)
        {
            _configuration = configuration;
            _env = env;
        }

        private SqlConnection GetConnection()
        {
            return new SqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));
        }

        // ---------------- LIST ----------------
        public IActionResult Mentor()
        {
            List<Mentor> list = new();

            using SqlConnection con = GetConnection();
            SqlCommand cmd = new(
                "SELECT * FROM Mentors ORDER BY DisplayOrder ASC", con);

            con.Open();
            SqlDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                list.Add(new Mentor
                {
                    MentorId = Convert.ToInt32(reader["MentorId"]),
                    Name = reader["Name"].ToString(),
                    Designation = reader["Designation"].ToString(),
                    ImagePath = reader["ImagePath"].ToString(),
                    DisplayOrder = Convert.ToInt32(reader["DisplayOrder"])
                });
            }

            return View(list);
        }

        // ---------------- CREATE (GET) ----------------
        [HttpGet]
        public IActionResult AddMentor()
        {
            using SqlConnection con = GetConnection();
            SqlCommand cmd = new("SELECT COUNT(*) FROM Mentors", con);

            con.Open();
            int count = (int)cmd.ExecuteScalar();

            if (count >= 2)
            {
                TempData["ErrorMessage"] =
                    "Only 2 mentors are allowed. Please delete one to add another.";
                return RedirectToAction("Mentor");
            }

            return View();
        }

        // ---------------- CREATE (POST) ----------------
        [HttpPost]
        public IActionResult AddMentor(Mentor model, IFormFile ImageFile)
        {
            // HARD SAFETY CHECK
            using (SqlConnection con = GetConnection())
            {
                SqlCommand countCmd = new("SELECT COUNT(*) FROM Mentors", con);
                con.Open();
                int count = (int)countCmd.ExecuteScalar();
                con.Close();

                if (count >= 2)
                {
                    ModelState.AddModelError("", "Maximum 2 mentors allowed.");
                    return View(model);
                }
            }

            try
            {
                if (ImageFile == null || ImageFile.Length == 0)
                    return View(model);

                string folder = Path.Combine(_env.WebRootPath, "uploads/mentors");
                Directory.CreateDirectory(folder);

                string fileName = Guid.NewGuid() + Path.GetExtension(ImageFile.FileName);
                string filePath = Path.Combine(folder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    ImageFile.CopyTo(stream);
                }

                using SqlConnection con = GetConnection();
                SqlCommand cmd = new(@"
                    INSERT INTO Mentors (Name, Designation, ImagePath, DisplayOrder)
                    VALUES (@Name, @Designation, @ImagePath, @DisplayOrder)", con);

                cmd.Parameters.AddWithValue("@Name", model.Name);
                cmd.Parameters.AddWithValue("@Designation", model.Designation);
                cmd.Parameters.AddWithValue("@ImagePath", fileName);
                cmd.Parameters.AddWithValue("@DisplayOrder", model.DisplayOrder);

                con.Open();
                cmd.ExecuteNonQuery();

                TempData["SuccessMessage"] = "Mentor added successfully.";
                return RedirectToAction("Mentor");
            }
            catch (Exception ex)
            {
                ViewBag.Error = ex.Message;
                return View(model);
            }
        }

        // ---------------- DELETE ----------------
        [HttpPost]
        public IActionResult Delete(int id)
        {
            try
            {
                string oldImage = null;

                using SqlConnection con = GetConnection();

                SqlCommand getCmd =
                    new("SELECT ImagePath FROM Mentors WHERE MentorId=@id", con);
                getCmd.Parameters.AddWithValue("@id", id);

                con.Open();
                oldImage = getCmd.ExecuteScalar()?.ToString();
                con.Close();

                SqlCommand delCmd =
                    new("DELETE FROM Mentors WHERE MentorId=@id", con);
                delCmd.Parameters.AddWithValue("@id", id);

                con.Open();
                delCmd.ExecuteNonQuery();

                if (!string.IsNullOrEmpty(oldImage))
                {
                    string path = Path.Combine(
                        _env.WebRootPath, "uploads/mentors", oldImage);

                    if (System.IO.File.Exists(path))
                        System.IO.File.Delete(path);
                }

                return RedirectToAction("Mentor");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Mentor");
            }
        }

        // ---------------- EDIT (GET) ----------------
        [HttpGet]
        public IActionResult EditMentor(int id)
        {
            using SqlConnection con = GetConnection();
            using SqlCommand cmd = new(@"
                SELECT MentorId, Name, Designation, ImagePath, DisplayOrder
                FROM Mentors
                WHERE MentorId = @Id", con);

            cmd.Parameters.AddWithValue("@Id", id);
            con.Open();

            using SqlDataReader dr = cmd.ExecuteReader();
            if (!dr.Read()) return NotFound();

            return View(new Mentor
            {
                MentorId = dr.GetInt32(0),
                Name = dr.GetString(1),
                Designation = dr.GetString(2),
                ImagePath = dr.IsDBNull(3) ? null : dr.GetString(3),
                DisplayOrder = dr.GetInt32(4)
            });
        }

        // ---------------- EDIT (POST) ----------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditMentor(Mentor model, IFormFile ImageFile)
        {
            string finalImageName = model.ImagePath;

            if (ImageFile != null && ImageFile.Length > 0)
            {
                string uploadFolder =
                    Path.Combine(_env.WebRootPath, "uploads", "mentors");

                Directory.CreateDirectory(uploadFolder);

                if (!string.IsNullOrEmpty(model.ImagePath))
                {
                    string oldPath =
                        Path.Combine(uploadFolder, model.ImagePath);

                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                string safeName =
                    Path.GetFileName(ImageFile.FileName).Replace(" ", "_");

                string newPath = Path.Combine(uploadFolder, safeName);

                if (System.IO.File.Exists(newPath))
                {
                    string name = Path.GetFileNameWithoutExtension(safeName);
                    string ext = Path.GetExtension(safeName);
                    safeName = $"{name}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
                    newPath = Path.Combine(uploadFolder, safeName);
                }

                using var stream = new FileStream(newPath, FileMode.Create);
                ImageFile.CopyTo(stream);

                finalImageName = safeName;
            }

            using SqlConnection con = GetConnection();
            con.Open();
            using SqlTransaction tran = con.BeginTransaction();

            try
            {
                using SqlCommand cmd = new(@"
                    UPDATE Mentors
                    SET Name=@Name,
                        Designation=@Designation,
                        ImagePath=@ImagePath,
                        DisplayOrder=@DisplayOrder
                    WHERE MentorId=@Id", con, tran);

                cmd.Parameters.AddWithValue("@Id", model.MentorId);
                cmd.Parameters.AddWithValue("@Name", model.Name);
                cmd.Parameters.AddWithValue("@Designation", model.Designation);
                cmd.Parameters.AddWithValue("@DisplayOrder", model.DisplayOrder);
                cmd.Parameters.AddWithValue("@ImagePath",
                    (object?)finalImageName ?? DBNull.Value);

                cmd.ExecuteNonQuery();
                tran.Commit();

                TempData["SuccessMessage"] = "Mentor updated successfully.";
            }
            catch
            {
                tran.Rollback();
                TempData["Error"] = "Mentor update failed.";
                return View(model);
            }

            return RedirectToAction("Mentor");
        }
    }
}
