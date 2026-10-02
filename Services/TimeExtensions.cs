namespace Recipes.Services;

public static class TimeExtensions
{
    extension(TimeProvider time)
    {
        /// <summary>Today's date in the local time zone.</summary>
        public DateOnly Today => DateOnly.FromDateTime(time.GetLocalNow().DateTime);

        /// <summary>The current local date and time.</summary>
        public DateTime Now => time.GetLocalNow().DateTime;
    }
}
