using System;

namespace SPOTTER.Controllers
{
    /// <summary>
    /// Event arguments for satellite information updates.
    /// </summary>
    public class SatelliteInfoEventArgs : EventArgs
    {
        public int SatellitesInView { get; set; }
        public int SatellitesUsed { get; set; }
    }
}
