namespace SecureSync_Server
{
    public class FileDbContext : DbContext
    {
        public DbSet<UploadedFile> UploadedFiles { get; set; }

        public FileDbContext(DbContextOptions<FileDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UploadedFile>().HasKey(f => f.Id);
        }
    }

    public class UploadedFile
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
    }
}
