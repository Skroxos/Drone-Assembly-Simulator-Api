namespace DroneSimlator.Api.Models
{
    public class DroneAssemblySession
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public float FinishTime { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
