﻿namespace Umbraco.Configuration.Models
{
    public class SecuritySettings
    {
        public bool KeepUserLoggedIn { get; set; } = false;

        public bool HideDisabledUsersInBackoffice { get; set; } = false;

        public bool AllowPasswordReset { get; set; } = true;

        public string AuthCookieName { get; set; } = "UMB_UCONTEXT";

        public string AuthCookieDomain { get; set; }

        public bool UsernameIsEmail { get; set; } = true;
    }
}
