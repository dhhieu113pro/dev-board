using System;

namespace DevBoard.Models
{
    public static class ThemeSchedule
    {
        public readonly record struct SolarTimes(DateTime Sunrise, DateTime Sunset);

        public static string Resolve(
            string mode,
            DateTime now,
            TimeSpan lightStart,
            TimeSpan darkStart,
            double? latitude,
            double? longitude,
            TimeSpan? utcOffset = null)
        {
            if (string.Equals(mode, "Light", StringComparison.OrdinalIgnoreCase))
                return "Light";
            if (string.Equals(mode, "Dark", StringComparison.OrdinalIgnoreCase))
                return "Dark";
            if (string.Equals(mode, "Default", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mode, "System", StringComparison.OrdinalIgnoreCase))
                return "System";

            if (string.Equals(mode, "Custom", StringComparison.OrdinalIgnoreCase))
                return IsInRange(now.TimeOfDay, lightStart, darkStart) ? "Light" : "Dark";

            if (string.Equals(mode, "Sunset", StringComparison.OrdinalIgnoreCase))
            {
                if (latitude is null || longitude is null)
                    return "System";

                var offset = utcOffset ?? TimeZoneInfo.Local.GetUtcOffset(now);
                var solar = GetSunriseSunset(now, latitude.Value, longitude.Value, offset);
                if (solar is null)
                    return "System";

                return IsInRange(now.TimeOfDay, solar.Value.Sunrise.TimeOfDay, solar.Value.Sunset.TimeOfDay)
                    ? "Light"
                    : "Dark";
            }

            return "System";
        }

        public static SolarTimes? GetSunriseSunset(DateTime date, double latitude, double longitude, TimeSpan utcOffset)
        {
            if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
                return null;

            var sunrise = CalculateSolarTime(date, latitude, longitude, utcOffset, true);
            var sunset = CalculateSolarTime(date, latitude, longitude, utcOffset, false);
            return sunrise is null || sunset is null ? null : new SolarTimes(sunrise.Value, sunset.Value);
        }

        private static bool IsInRange(TimeSpan value, TimeSpan start, TimeSpan end)
        {
            if (start == end)
                return true;

            return start < end
                ? value >= start && value < end
                : value >= start || value < end;
        }

        private static DateTime? CalculateSolarTime(
            DateTime date,
            double latitude,
            double longitude,
            TimeSpan utcOffset,
            bool sunrise)
        {
            const double zenith = 90.833;
            var day = date.DayOfYear;
            var longitudeHour = longitude / 15.0;
            var approximateTime = day + ((sunrise ? 6.0 : 18.0) - longitudeHour) / 24.0;

            var meanAnomaly = 0.9856 * approximateTime - 3.289;
            var trueLongitude = NormalizeDegrees(
                meanAnomaly +
                1.916 * SinDegrees(meanAnomaly) +
                0.020 * SinDegrees(2 * meanAnomaly) +
                282.634);

            var rightAscension = NormalizeDegrees(RadiansToDegrees(Math.Atan(0.91764 * Math.Tan(DegreesToRadians(trueLongitude)))));
            var longitudeQuadrant = Math.Floor(trueLongitude / 90.0) * 90.0;
            var rightAscensionQuadrant = Math.Floor(rightAscension / 90.0) * 90.0;
            rightAscension = (rightAscension + longitudeQuadrant - rightAscensionQuadrant) / 15.0;

            var sinDeclination = 0.39782 * SinDegrees(trueLongitude);
            var cosDeclination = Math.Cos(Math.Asin(sinDeclination));
            var cosHourAngle =
                (CosDegrees(zenith) - sinDeclination * SinDegrees(latitude)) /
                (cosDeclination * CosDegrees(latitude));

            if (cosHourAngle is > 1 or < -1)
                return null;

            var hourAngle = sunrise
                ? 360.0 - RadiansToDegrees(Math.Acos(cosHourAngle))
                : RadiansToDegrees(Math.Acos(cosHourAngle));
            hourAngle /= 15.0;

            var localMeanTime = hourAngle + rightAscension - 0.06571 * approximateTime - 6.622;
            var utcHour = NormalizeHours(localMeanTime - longitudeHour);
            var localHour = NormalizeHours(utcHour + utcOffset.TotalHours);

            return date.Date.AddHours(localHour);
        }

        private static double NormalizeDegrees(double value)
        {
            value %= 360.0;
            return value < 0 ? value + 360.0 : value;
        }

        private static double NormalizeHours(double value)
        {
            value %= 24.0;
            return value < 0 ? value + 24.0 : value;
        }

        private static double SinDegrees(double value) => Math.Sin(DegreesToRadians(value));
        private static double CosDegrees(double value) => Math.Cos(DegreesToRadians(value));
        private static double DegreesToRadians(double value) => value * Math.PI / 180.0;
        private static double RadiansToDegrees(double value) => value * 180.0 / Math.PI;
    }
}
