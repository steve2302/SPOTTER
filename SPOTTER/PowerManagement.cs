using System;
using System.Runtime.InteropServices;

namespace SPOTTER.Controllers
{
    public static class PowerManagement
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern uint SetThreadExecutionState(uint esFlags);

        private const uint ES_CONTINUOUS = 0x80000000;
        private const uint ES_SYSTEM_REQUIRED = 0x00000001;
        private const uint ES_DISPLAY_REQUIRED = 0x00000002;

        /// <summary>
        /// Prevents the system from entering sleep mode
        /// </summary>
        public static void PreventSleep()
        {
            SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED);
        }

        /// <summary>
        /// Allows the system to enter sleep mode normally
        /// </summary>
        public static void AllowSleep()
        {
            SetThreadExecutionState(ES_CONTINUOUS);
        }
    }
}