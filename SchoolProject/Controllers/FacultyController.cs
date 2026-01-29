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
    public class FacultyController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;

        public FacultyController(IConfiguration configuration, IWebHostEnvironment env)
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
        public IActionResult Faculty()
        {
            List<Faculty> list = new();

            using SqlConnection con = GetConnection();
            SqlCommand cmd = new(
                "SELECT * FROM Faculty ORDER BY CreatedDate DESC", con);

            con.Open();
            SqlDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                list.Add(new Faculty
                {
                    FacultyId = Convert.ToInt32(reader["FacultyId"]),
                    Name = reader["Name"].ToString(),
                    Designation = reader["Designation"].ToString(),
                    ImagePath = reader["ImagePath"].ToString(),
                    CreatedDate = Convert.ToDateTime(reader["CreatedDate"])
                });
            }

            return View(list);
        }

        // ---------------- CREATE (GET) ----------------
        [HttpGet]
        public IActionResult AddFaculty()
        {
            return View();
        }

        // ---------------- CREATE (POST) ----------------
        [HttpPost]
        public IActionResult AddFaculty(Faculty model, IFormFile ImageFile)
        {
            try
            {
                if (ImageFile == null || ImageFile.Length == 0)
                    return View(model);

                string folder = Path.Combine(_env.WebRootPath, "uploads/faculty");
                Directory.CreateDirectory(folder);

                string fileName = Guid.NewGuid() + Path.GetExtension(ImageFile.FileName);
                string filePath = Path.Combine(folder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    ImageFile.CopyTo(stream);
                }

                using SqlConnection con = GetConnection();
                SqlCommand cmd = new(@"
                    INSERT INTO Faculty (Name, Designation, ImagePath)
                    VALUES (@Name, @Designation, @ImagePath)", con);

                cmd.Parameters.AddWithValue("@Name", model.Name);
                cmd.Parameters.AddWithValue("@Designation", model.Designation);
                cmd.Parameters.AddWithValue("@ImagePath", fileName);

                con.Open();
                cmd.ExecuteNonQuery();

                TempData["SuccessMessage"] = "Faculty added successfully.";
                return RedirectToAction("Faculty");
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

                SqlCommand getCmd = new(
                    "SELECT ImagePath FROM Faculty WHERE FacultyId=@id", con);
                getCmd.Parameters.AddWithValue("@id", id);

                con.Open();
                oldImage = getCmd.ExecuteScalar()?.ToString();
                con.Close();

                SqlCommand delCmd = new(
                    "DELETE FROM Faculty WHERE FacultyId=@id", con);
                delCmd.Parameters.AddWithValue("@id", id);

                con.Open();
                delCmd.ExecuteNonQuery();

                if (!string.IsNullOrEmpty(oldImage))
                {
                    string path = Path.Combine(_env.WebRootPath, "uploads/faculty", oldImage);
                    if (System.IO.File.Exists(path))
                        System.IO.File.Delete(path);
                }

                return RedirectToAction("Faculty");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Faculty");
            }
        }

        // ---------------- EDIT (GET) ----------------
        [HttpGet]
        public IActionResult EditFaculty(int id)
        {
            using SqlConnection con = GetConnection();
            using SqlCommand cmd = new SqlCommand(@"
                SELECT FacultyId, Name, Designation, ImagePath
                FROM Faculty
                WHERE FacultyId = @Id
            ", con);

            cmd.Parameters.AddWithValue("@Id", id);
            con.Open();

            using SqlDataReader dr = cmd.ExecuteReader();
            if (!dr.Read()) return NotFound();

            return View(new Faculty
            {
                FacultyId = dr.GetInt32(0),
                Name = dr.GetString(1),
                Designation = dr.GetString(2),
                ImagePath = dr.IsDBNull(3) ? null : dr.GetString(3)
            });
        }

        // ---------------- EDIT (POST) ----------------
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditFaculty(Faculty model, IFormFile ImageFile)
        {
            string finalImageName = model.ImagePath;

            if (ImageFile != null && ImageFile.Length > 0)
            {
                string uploadFolder = Path.Combine(
                    _env.WebRootPath, "uploads", "faculty");

                Directory.CreateDirectory(uploadFolder);

                if (!string.IsNullOrEmpty(model.ImagePath))
                {
                    string oldPath = Path.Combine(uploadFolder, model.ImagePath);
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                string safeName = Path.GetFileName(ImageFile.FileName)
                                        .Replace(" ", "_");

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
                using SqlCommand cmd = new SqlCommand(@"
                    UPDATE Faculty
                    SET Name=@Name,
                        Designation=@Designation,
                        ImagePath=@ImagePath
                    WHERE FacultyId=@Id
                ", con, tran);

                cmd.Parameters.AddWithValue("@Id", model.FacultyId);
                cmd.Parameters.AddWithValue("@Name", model.Name);
                cmd.Parameters.AddWithValue("@Designation", model.Designation);
                cmd.Parameters.AddWithValue("@ImagePath",
                    (object?)finalImageName ?? DBNull.Value);

                cmd.ExecuteNonQuery();
                tran.Commit();

                TempData["SuccessMessage"] = "Faculty updated successfully.";
            }
            catch
            {
                tran.Rollback();
                TempData["Error"] = "Faculty update failed.";
                return View(model);
            }

            return RedirectToAction("Faculty");
        }
    }
}
