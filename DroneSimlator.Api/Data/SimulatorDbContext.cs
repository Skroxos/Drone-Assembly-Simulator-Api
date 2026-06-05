using DroneSimlator.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DroneSimlator.Api.Data
{
    public class SimulatorDbContext : DbContext
    {
        public SimulatorDbContext(DbContextOptions options) : base(options)
        {
        }

        public DbSet<DroneAssemblySession> AssemblySessions => Set<DroneAssemblySession>();
    }
}
