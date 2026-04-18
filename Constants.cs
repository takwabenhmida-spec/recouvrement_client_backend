namespace RecouvrementAPI
{
    public static class AppConstants
    {
        public const string SystemActor = "systeme";
        public const string ClientActor = "client";
        public const string TokenInvalid = "Token invalide";
        public const string DossierNotFound = "Dossier introuvable";
        
        public static class RelanceStatut
        {
            public const string Sent = "envoye";
            public const string NoResponse = "sans_reponse";
            public const string Replied = "repondu";
        }

        public static class RelanceMoyen
        {
            public const string Sms = "sms";
            public const string Email = "email";
            public const string Appel = "appel";
        }

        public static class UserStatut
        {
            public const string Active = "Actif";
            public const string Inactive = "Inactif";
        }

        public static class DossierStatut
        {
            public const string Amiable = "aimable";
            public const string Contentieux = "contentieux";
            public const string Regularise = "regularise";
        }

        public static class EcheanceStatut
        {
            public const string Impaye = "impaye";
            public const string Paye = "paye";
        }

        public static class ClientStatut
        {
            public const string Actif = "Actif";
            public const string Archive = "Archivé";
        }
    }
}
