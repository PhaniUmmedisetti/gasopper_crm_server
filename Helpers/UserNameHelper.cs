// Helpers/UserNameHelper.cs
using gasopper_crm_server.Models;

namespace gasopper_crm_server.Helpers
{
    public static class UserNameHelper
    {
        public static string FormatUserName(User? user, string defaultValue = "Unknown User")
        {
            if (user == null) return defaultValue;
            
            var fullName = $"{user.first_name ?? ""} {user.last_name ?? ""}".Trim();
            return string.IsNullOrEmpty(fullName) ? defaultValue : fullName;
        }

        public static string FormatAssignedUserName(User? user)
        {
            return FormatUserName(user, "Unassigned");
        }

        public static string FormatCreatedByUserName(User? user)
        {
            return FormatUserName(user, "Unknown User");
        }

        public static string FormatManagerName(User? user)
        {
            return FormatUserName(user, "No Manager");
        }
    }
}