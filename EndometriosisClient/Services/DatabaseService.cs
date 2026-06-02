using System.Collections.Generic;
using System.Linq;
using EndometriosisClient.Data;
using EndometriosisClient.Models;
using Microsoft.EntityFrameworkCore;

namespace EndometriosisClient.Services
{
    public class DatabaseService
    {
        // добавляет пациента
        public Patient AddPatient(string fullName, int age, string notes, int doctorId)
        {
            using var db = new AppDbContext();

            var patient = new Patient
            {
                FullName = fullName,
                Age = age,
                Notes = notes,
                DoctorId = doctorId
            };

            db.Patients.Add(patient);
            db.SaveChanges();

            return patient;
        }

        public AppUser AddDoctor(string fullName, string login, string password)
        {
            using var db = new AppDbContext();

            var doctor = new AppUser
            {
                FullName = fullName,
                Login = login,
                PasswordHash = PasswordHasher.HashPassword(password),
                Role = UserRoles.Doctor
            };

            db.Users.Add(doctor);
            db.SaveChanges();

            return doctor;
        }

        // добавляет МРТ исследование
        public MRIStudy AddMRIStudy(int patientId, string filePath, string fileName, string fileType)
        {
            using var db = new AppDbContext();

            var study = new MRIStudy
            {
                PatientId = patientId,
                FilePath = filePath,
                FileName = fileName,
                FileType = fileType
            };

            db.MRIStudies.Add(study);
            db.SaveChanges();

            return study;
        }

        // добавляет результат сегментации
        public SegmentationResult AddSegmentationResult(
            int studyId,
            string resultPath,
            string status,
            string conclusion,
            bool isStub = true,
            string previewImagePath = "")
        {
            using var db = new AppDbContext();

            var result = new SegmentationResult
            {
                MRIStudyId = studyId,
                PreviewImagePath = previewImagePath,
                ResultPath = resultPath,
                Status = status,
                Conclusion = conclusion,
                IsStub = isStub
            };

            db.SegmentationResults.Add(result);
            db.SaveChanges();

            return result;
        }

        // получает всех пациентов
        public List<Patient> GetAllPatients()
        {
            using var db = new AppDbContext();
            return db.Patients.ToList();
        }

        // получает все исследования
        public List<MRIStudy> GetAllStudies()
        {
            using var db = new AppDbContext();
            return db.MRIStudies.ToList();
        }

        // получает результаты по исследованию
        public List<SegmentationResult> GetResultsByStudy(int studyId)
        {
            using var db = new AppDbContext();
            return db.SegmentationResults
                     .Where(r => r.MRIStudyId == studyId)
                     .ToList();
        }

        // получает исследования с пациентами и результатами
        public List<StudyListItem> GetStudyListItems(AppUser currentUser)
        {
            using var db = new AppDbContext();

            var query = db.MRIStudies
                .Include(s => s.Patient)
                .ThenInclude(p => p.Doctor)
                .AsQueryable();

            if (currentUser.Role == UserRoles.Doctor)
            {
                query = query.Where(s => s.Patient.DoctorId == currentUser.Id);
            }

            var studies = query.ToList();
            var results = db.SegmentationResults.ToList();

            var items = studies.Select(study =>
            {
                var result = results
                    .Where(r => r.MRIStudyId == study.Id)
                    .OrderByDescending(r => r.CreatedAt)
                    .FirstOrDefault();

                return new StudyListItem
                {
                    StudyId = study.Id,
                    PatientId = study.PatientId,
                    PatientName = study.Patient?.FullName ?? "Неизвестно",
                    Age = study.Patient?.Age ?? 0,
                    DoctorName = study.Patient?.Doctor?.FullName ?? "Неизвестно",
                    FileName = study.FileName,
                    UploadDate = study.UploadDate,
                    Status = result?.Status ?? "нет результата",
                    Conclusion = result?.Conclusion ?? "Результат отсутствует",
                    PreviewImagePath = result?.PreviewImagePath ?? string.Empty,
                    ResultPath = result?.ResultPath ?? string.Empty
                };
            }).ToList();

            return items;
        }
    }

    public class StudyListItem
    {
        public int StudyId { get; set; }
        public int PatientId { get; set; }
        public string DoctorName { get; set; }
        public string PatientName { get; set; }
        public int Age { get; set; }
        public string FileName { get; set; }
        public System.DateTime UploadDate { get; set; }
        public string Status { get; set; }
        public string Conclusion { get; set; }
        public string PreviewImagePath { get; set; }
        public string ResultPath { get; set; }
    }
}