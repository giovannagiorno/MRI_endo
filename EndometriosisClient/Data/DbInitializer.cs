using System.IO;

namespace EndometriosisClient.Data
{
    public static class DbInitializer
    {
        public static void Initialize()
        {
            string databaseFolder = Path.Combine(Directory.GetCurrentDirectory(), "Database");
            Directory.CreateDirectory(databaseFolder);

            using var db = new AppDbContext();
            db.Database.EnsureCreated();
        }
    }
}