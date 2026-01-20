
using HojaDeRuta.Models.DAO;
using Microsoft.EntityFrameworkCore;

namespace HojaDeRuta.DBContext
{
    public class HojasDbContext : DbContext
    {
        public HojasDbContext(DbContextOptions<HojasDbContext> options)
            : base(options)
        {
        }

        public DbSet<Hoja> Hojas { get; set; }
        public DbSet<TipoDocumento> TIPOS_DOCUMENTOS { get; set; }
        public DbSet<Sector> SECTORES { get; set; }
        public DbSet<SubArea> SUBAREA { get; set; }
        public DbSet<Clientes> Clientes_Creatio { get; set; }
        public DbSet<Revisores> REVISORES { get; set; }
        public DbSet<Socios> SOCIOS { get; set; }
        public DbSet<Contratos> CONTRATOS_COMPLETO { get; set; }
        public DbSet<SyncControl> SyncControl { get; set; }
        public DbSet<Auditoria> AUDITORIAS { get; set; }
        public DbSet<HojaEstado> Hoja_Estado { get; set; }
        public DbSet<Jurisdiccion> Jurisdiccion { get; set; }



        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var configuration = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json").Build();
                optionsBuilder.UseSqlServer(configuration.GetConnectionString("hojaDB"));
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TipoDocumento>().HasNoKey();
            modelBuilder.Entity<Sector>().HasNoKey();
            modelBuilder.Entity<SubArea>().HasNoKey();
            modelBuilder.Entity<Revisores>().HasNoKey();
            modelBuilder.Entity<Socios>().HasNoKey();
            modelBuilder.Entity<Jurisdiccion>().HasNoKey();
        }
    }

}
