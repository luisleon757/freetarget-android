using System;

namespace cronografo.Models
{
    public class ShotModel
    {
        public int Number { get; set; }
        public float Velocity { get; set; }
        public float Energy { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public string DisplayTime => Timestamp.ToString("HH:mm:ss");
        public string DisplayVelocity => $"{Velocity:F1} m/s";
        public string DisplayEnergy => $"{Energy:F1} J";
    }
}
