using System;

namespace Smop.OdorDisplay
{
    public static class DeviceTubes
    {
        public enum From
        {
            Chamber,
            Valve1
        }

        public enum To
        {
            User,
            Mixer
        }

        /// <summary>
        /// Calculated the time in seconds for the odor to flow from 
        /// 1 - the bottle
        /// 2 - the valve #1
        /// to 
        /// 1 - the user
        /// 2 - the mixer
        /// given the current odor and fresh air speeds
        /// </summary>
        /// <param name="startPoint">the starting point</param>
        /// <param name="endPoint">the point for odor to reach</param>
        /// <param name="speed">Odor speed (the current one if omitted)</param>
        /// <returns>Time the odor reaches a user in seconds</returns>
        public static double EstimateFlowDuration(From startPoint, To endPoint, double speed = 0)
        {
            double result = 0;

            var odorSpeed = (speed <= 0 ? _mfc.OdorSpeed : speed) / 60;      // ml/s

            if (startPoint == From.Chamber)
            {
                var odorTubeVolume = Math.PI * TUBE_R * TUBE_R * ODOR_TUBE_LENGTH / 1000;           // ml

                result += odorTubeVolume / odorSpeed;
            }

            var vmTubeVolume = Math.PI * TUBE_R * TUBE_R * VALVE_MIXER_TUBE_LENGTH / 1000;           // ml
            result += vmTubeVolume / odorSpeed;

            if (endPoint == To.User)
            {
                var mixedTubeVolume = Math.PI * TUBE_R * TUBE_R * MIXED_TUBE_LENGTH / 1000;   // ml
                var mixedSpeed = 1000 * _mfc.FreshAirSpeed / 60;             // ml/s

                result += mixedTubeVolume / mixedSpeed;
            }

            return result;
        }

        /// <summary>
        /// Calculates the speed in ml/min for the MFC-B (odor tube) that is required to 
        /// fill the tube between the bottle and the mixer with the odor
        /// </summary>
        /// <param name="time">The time to fill the tube in seconds</param>
        /// <returns>The speed in ml/min</returns>
        public static double PredictFlowSpeed(double time)
        {
            var odorTubeVolume = Math.PI * TUBE_R * TUBE_R * ODOR_TUBE_LENGTH / 1000;       // ml
            return odorTubeVolume / time * 60;
        }

        /// <summary>
        /// Converts PPM to the corresponding odor speed
        /// </summary>
        /// <param name="ppm">Odor concentration in ppm</param>
        /// <returns>Odor speed</returns>
        public static double PPM2Speed(double ppm)
        {
            return 1.0 * ppm;   // TODO: implement ppm to speed conversion
        }


        // Internal

        const double ODOR_TUBE_LENGTH = 600;       // mm
        const double VALVE_MIXER_TUBE_LENGTH = 27; // mm
        const double MIXED_TUBE_LENGTH = 1200;     // mm

        const double TUBE_R = 2;                   // mm

        static readonly MFC _mfc = MFC.Instance;
    }
}
